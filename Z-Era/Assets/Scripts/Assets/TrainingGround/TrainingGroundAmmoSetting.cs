using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 训练场弹药设置管理器：挂在 BulletSeetingRoot 上（或选项牌的任意
/// 父物体），负责两件事：
/// 1. 识别子弹命中的选项牌。子弹命中当帧即被对象池回收，物理触发器
///    事件不一定触发，因此这里订阅 BulletHandle 的 BulletLaunched/
///    BulletSettled 静态事件，用"发射点 → 落点"还原飞行方向，再从
///    落点向后退一点打一根短射线，找出子弹实际命中的碰撞体。
/// 2. 无限子弹开启时，逐帧监视所有武器的弹匣数：只要因为射击减少就
///    立即还原，实现"射击不消耗子弹"。武器列表定期重扫，覆盖中途
///    拾取的新武器。
/// 只订阅事件、只读写运行时弹药数值，不修改任何根代码；
/// 场景卸载时反订阅事件，不影响其他场景。
/// </summary>
public class TrainingGroundAmmoSetting : MonoBehaviour
{
    // 单个武器的弹匣监视记录
    private struct WeaponTracker
    {
        public WeaponController Weapon;
        public int LastMagazineAmmo;
    }

    [Header("扫描设置")]

    [Tooltip("重新扫描武器的间隔（秒），覆盖中途拾取的新武器")]
    [SerializeField, Min(0.5f)]
    private float weaponScanInterval = 2f;

    private readonly Dictionary<BulletHandle, Vector3> launchPositions =
        new Dictionary<BulletHandle, Vector3>();

    private readonly List<WeaponTracker> weaponTrackers =
        new List<WeaponTracker>();

    private AmmoSettingSign selectedSign;
    private bool infiniteAmmo;
    private float nextWeaponScanTime;

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

        BulletHandle.BulletLaunched += OnBulletLaunched;
        BulletHandle.BulletSettled += OnBulletSettled;

        ScanWeapons();
    }

    private void OnDisable()
    {
        BulletHandle.BulletLaunched -= OnBulletLaunched;
        BulletHandle.BulletSettled -= OnBulletSettled;

        launchPositions.Clear();
    }

    // 选中某个选项牌；重复命中同一选项是幂等的
    public void SelectSign(AmmoSettingSign sign)
    {
        if (sign == null || sign == selectedSign)
        {
            return;
        }

        selectedSign?.SetSelected(false);
        selectedSign = sign;
        selectedSign.SetSelected(true);

        infiniteAmmo = sign.EnableInfiniteAmmo;

        // 开启无限子弹时立即把所有武器的弹匣补满：
        // 之前射击消耗的子弹不应等到下一次还原才被补回。
        // 备弹不补，射击本来就不消耗备弹
        if (infiniteAmmo)
        {
            ScanWeapons();

            for (int i = 0; i < weaponTrackers.Count; i++)
            {
                WeaponTracker tracker = weaponTrackers[i];

                if (tracker.Weapon == null)
                {
                    continue;
                }

                tracker.Weapon.currentMagazineAmmo =
                    tracker.Weapon.MagazineSize;

                tracker.LastMagazineAmmo =
                    tracker.Weapon.currentMagazineAmmo;

                weaponTrackers[i] = tracker;
            }
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextWeaponScanTime)
        {
            ScanWeapons();
        }

        RestoreWeaponAmmo();
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
        TrySelectSignAtHit(bullet.transform.position, startPosition);
    }

    // 用"发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短
    // 射线，找出子弹实际命中的碰撞体；命中选项牌时选中它
    private void TrySelectSignAtHit(
        Vector3 settlePosition,
        Vector3 launchPosition)
    {
        Vector3 direction = settlePosition - launchPosition;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        if (Physics.Raycast(
            settlePosition - direction * SettleRayBackOffset,
            direction,
            out RaycastHit hit,
            SettleRayDistance,
            ~settleRayIgnoreMask,
            QueryTriggerInteraction.Collide))
        {
            AmmoSettingSign sign =
                hit.collider.GetComponentInParent<AmmoSettingSign>();

            if (sign != null)
            {
                SelectSign(sign);
            }
        }
    }

    // 逐帧监视弹匣数：无限子弹时，把因射击造成的减少立即还原；
    // 增加（换弹、拾取）则直接跟随基准值
    private void RestoreWeaponAmmo()
    {
        for (int i = weaponTrackers.Count - 1; i >= 0; i--)
        {
            WeaponTracker tracker = weaponTrackers[i];

            if (tracker.Weapon == null)
            {
                weaponTrackers.RemoveAt(i);
                continue;
            }

            int currentAmmo = tracker.Weapon.currentMagazineAmmo;

            if (currentAmmo > tracker.LastMagazineAmmo)
            {
                tracker.LastMagazineAmmo = currentAmmo;
            }
            else if (currentAmmo < tracker.LastMagazineAmmo)
            {
                if (infiniteAmmo)
                {
                    tracker.Weapon.currentMagazineAmmo =
                        tracker.LastMagazineAmmo;
                }

                tracker.LastMagazineAmmo =
                    tracker.Weapon.currentMagazineAmmo;
            }

            weaponTrackers[i] = tracker;
        }
    }

    private void ScanWeapons()
    {
        nextWeaponScanTime = Time.unscaledTime + weaponScanInterval;

        weaponTrackers.RemoveAll(tracker => tracker.Weapon == null);

        PlayerController player = FindObjectOfType<PlayerController>();

        if (player == null)
        {
            return;
        }

        // 包含未激活的武器，覆盖尚未拾取的武器槽
        foreach (WeaponController weapon in
            player.GetComponentsInChildren<WeaponController>(true))
        {
            if (weaponTrackers.Exists(
                tracker => tracker.Weapon == weapon))
            {
                continue;
            }

            weaponTrackers.Add(new WeaponTracker
            {
                Weapon = weapon,
                LastMagazineAmmo = weapon.currentMagazineAmmo
            });
        }
    }
}
