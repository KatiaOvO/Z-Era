using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 训练场武器选择：挂在 WeaponChooseRoot 上（其下为 9 把带触发器的
/// 武器展示模型，命名与 GunType 枚举及 WeaponHolder 下的手持武器
/// 完全一致）。子弹命中哪把展示模型，就启用 WeaponHolder 下同名
/// 的手持武器，并停用其余武器子物体（WeaponController 靠
/// gameObject.name 解析枪型，多把同时激活会互相抢输入）。
/// 子弹命中当帧即被对象池回收，物理触发器事件不一定触发，因此这里
/// 订阅 BulletHandle 的 BulletLaunched/BulletSettled 静态事件，用
/// "发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短射线，
/// 找出子弹实际命中的碰撞体。
/// 切枪时旧武器立即整体停用（保证 WeaponHUD 下帧即读到新武器，
/// 图标与弹药数即时同步）；正在播放的声音先"过户"到独立的回声
/// 物体上继续播完，听感不中断。
/// 只订阅事件、只开关 WeaponHolder 下的武器子物体，不修改任何根代码；
/// 场景卸载时反订阅事件，不影响其他场景。
/// </summary>
public class TrainingGroundWeaponChoose : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("玩家手持武器模型容器（Player 下），留空则自动从场景中的武器查找")]
    [SerializeField]
    private Transform weaponHolder;

    private readonly Dictionary<BulletHandle, Vector3> launchPositions =
        new Dictionary<BulletHandle, Vector3>();

    // 延续旧武器声音的回声物体的公共父级
    private Transform audioEchoRoot;

    // 落点反查射线的参数：子弹停在命中点表面，稍微沿反方向退一点
    // 再沿飞行方向打短射线即可命中原表面
    private const float SettleRayBackOffset = 0.05f;
    private const float SettleRayDistance = 0.15f;

    // 落点反查时排除的图层名，与 BulletHandle 默认排除的图层保持一致，
    // 避免反查射线命中子弹本身被忽略的玩家/武器碰撞体；
    // 图层号在 OnEnable 中查询（Unity API 不允许在字段初始化器中调用）
    private static readonly string[] SettleRayIgnoredLayerNames =
    {
        "Bullet",
        "Player",
        "Weapon",
        "Ignore Raycast"
    };

    private int settleRayIgnoreMask;

    private void OnEnable()
    {
        settleRayIgnoreMask = 0;
        foreach (string layerName in SettleRayIgnoredLayerNames)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer >= 0)
            {
                settleRayIgnoreMask |= 1 << layer;
            }
        }

        // 未拖拽赋值时兜底：任意一把武器的父物体就是 WeaponHolder
        if (weaponHolder == null)
        {
            WeaponController anyWeapon =
                FindObjectOfType<WeaponController>();

            if (anyWeapon != null)
            {
                weaponHolder = anyWeapon.transform.parent;
            }
        }

        BulletHandle.BulletLaunched += OnBulletLaunched;
        BulletHandle.BulletSettled += OnBulletSettled;
    }

    private void OnDisable()
    {
        BulletHandle.BulletLaunched -= OnBulletLaunched;
        BulletHandle.BulletSettled -= OnBulletSettled;

        launchPositions.Clear();
    }

    private void OnBulletLaunched(BulletHandle bullet)
    {
        if (bullet == null)
        {
            return;
        }

        launchPositions[bullet] = bullet.transform.position;
    }

    private void OnBulletSettled(BulletHandle bullet)
    {
        if (bullet == null ||
            !launchPositions.TryGetValue(
                bullet,
                out Vector3 startPosition))
        {
            return;
        }

        launchPositions.Remove(bullet);

        // 超时回收的子弹没有命中任何表面，落点反查打不到东西时会
        // 自然忽略，这里无需额外区分
        TrySelectWeaponAtHit(
            bullet.transform.position,
            startPosition);
    }

    // 用"发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短
    // 射线，找出子弹实际命中的碰撞体；命中展示模型时切换手持武器
    private void TrySelectWeaponAtHit(
        Vector3 settlePosition,
        Vector3 launchPosition)
    {
        if (weaponHolder == null)
        {
            return;
        }

        Vector3 direction = settlePosition - launchPosition;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        if (!Physics.Raycast(
            settlePosition - direction * SettleRayBackOffset,
            direction,
            out RaycastHit hit,
            SettleRayDistance,
            ~settleRayIgnoreMask,
            QueryTriggerInteraction.Collide))
        {
            return;
        }

        Transform display =
            GetDirectChildUnderRoot(hit.collider.transform);

        if (display == null)
        {
            return;
        }

        // 展示模型与手持武器同名，直接按名字查找；
        // Transform.Find 能找到未激活的子物体
        Transform weapon = weaponHolder.Find(display.name);

        if (weapon != null)
        {
            SelectWeapon(weapon);
        }
    }

    // 从命中的碰撞体向上找到 WeaponChooseRoot 的直接子物体
    // （即展示模型本体，命中可能发生在模型的子碰撞体上）
    private Transform GetDirectChildUnderRoot(Transform current)
    {
        while (current.parent != null &&
               current.parent != transform)
        {
            current = current.parent;
        }

        return current.parent == transform ? current : null;
    }

    // 启用目标武器，停用其余武器子物体：
    // 不带 WeaponController 的子物体（辅助节点等）保持原状。
    // 切回来的武器按满弹状态重新启用，不继承上次切走时
    // 残留的弹药（WeaponController 实例只是被停用，字段值一直在）
    private void SelectWeapon(Transform target)
    {
        WeaponController activatedController = null;

        foreach (Transform child in weaponHolder)
        {
            WeaponController controller =
                child.GetComponentInChildren<WeaponController>(true);

            if (controller == null)
            {
                continue;
            }

            if (child == target)
            {
                ActivateWeapon(child, controller);
                activatedController = controller;
            }
            else if (child.gameObject.activeSelf)
            {
                DeactivateWeapon(child, controller);
            }
            // 本来就未启用的武器保持未启用
        }

        if (activatedController != null)
        {
            UpdateWeaponIconOnHud(activatedController);
        }
    }

    private void ActivateWeapon(
        Transform weapon,
        WeaponController controller)
    {
        controller.RefillAmmo();

        // 整体停用过的武器重新激活时 Animator 会重置到初始状态，
        // 每次切换都从掏枪动画开始
        weapon.gameObject.SetActive(true);
        controller.enabled = true;
    }

    private void DeactivateWeapon(
        Transform weapon,
        WeaponController controller)
    {
        // 把正在播放的声音先过户到回声物体上继续播完，
        // 再整体停用武器：WeaponHUD 下一帧即可重新读到新武器，
        // 图标、弹匣数、备弹数全部即时同步
        CarryOverPlayingAudio(weapon);

        weapon.gameObject.SetActive(false);
    }

    // 把武器上正在播放的 AudioSource 复制到独立的回声物体上，
    // 从当前进度继续播放；回声物体播完后自行销毁
    private void CarryOverPlayingAudio(Transform weapon)
    {
        if (audioEchoRoot == null)
        {
            audioEchoRoot = new GameObject(
                "TrainingGroundAudioEcho"
            ).transform;
        }

        AudioSource[] sources =
            weapon.GetComponentsInChildren<AudioSource>();

        foreach (AudioSource source in sources)
        {
            if (source == null ||
                !source.isPlaying ||
                source.clip == null)
            {
                continue;
            }

            GameObject echoObject = new GameObject(
                "WeaponAudioEcho_" + source.clip.name
            );

            echoObject.transform.SetParent(
                audioEchoRoot,
                false
            );

            // 空间音效需要在原位置继续发声
            echoObject.transform.position =
                source.transform.position;

            AudioSource echo =
                echoObject.AddComponent<AudioSource>();

            echo.clip = source.clip;
            echo.time = source.time;
            echo.volume = source.volume;
            echo.pitch = source.pitch;
            echo.loop = false;
            echo.playOnAwake = false;
            echo.spatialBlend = source.spatialBlend;
            echo.rolloffMode = source.rolloffMode;
            echo.minDistance = source.minDistance;
            echo.maxDistance = source.maxDistance;
            echo.dopplerLevel = source.dopplerLevel;
            echo.outputAudioMixerGroup =
                source.outputAudioMixerGroup;

            echo.Play();

            float remainingTime =
                (source.clip.length - source.time) /
                Mathf.Max(0.01f, Mathf.Abs(source.pitch));

            Destroy(echoObject, remainingTime + 0.1f);
        }
    }

    // 立即刷新 HUD 武器图标：切换后旧武器当帧停用，WeaponHUD
    // 下一帧的重新查找本就会得到一致结果，这里直接同步是为了
    // 不出现一帧的旧图标
    private void UpdateWeaponIconOnHud(WeaponController controller)
    {
        WeaponHUD hud = FindObjectOfType<WeaponHUD>();

        if (hud == null || controller == null)
        {
            return;
        }

        GunData gunData = controller.CurrentGunData;

        if (gunData == null || hud.weaponSprites == null)
        {
            return;
        }

        int index = (int)gunData.gunType;

        if (index < 0 ||
            index >= hud.weaponSprites.Length ||
            hud.weaponSprites[index] == null)
        {
            return;
        }

        Sprite targetSprite = hud.weaponSprites[index];

        if (hud.weaponIcon != null)
        {
            hud.weaponIcon.sprite = targetSprite;
        }

        if (hud.weaponRedFill != null)
        {
            hud.weaponRedFill.sprite = targetSprite;
        }
    }
}
