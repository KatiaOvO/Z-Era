using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 训练场 HUD 设置管理器：挂在 HUDSettingRoot 上（或选项牌的任意
/// 父物体），识别子弹命中的选项牌并切换 HUD 显隐。
/// 子弹命中当帧即被对象池回收，物理触发器事件不一定触发，因此这里
/// 订阅 BulletHandle 的 BulletLaunched/BulletSettled 静态事件，用
/// "发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短射线，
/// 找出子弹实际命中的碰撞体。
/// 只订阅事件、只开关检查器中拖入的 UI 容器，不修改任何根代码；
/// 场景卸载时反订阅事件，不影响其他场景。
/// </summary>
public class TrainingGroundHUDSetting : MonoBehaviour
{
    [Header("HUD 容器")]

    [Tooltip("射击“是”时禁用、射击“否”时启用的 UI 容器物体")]
    [SerializeField]
    private GameObject[] hudContainers;

    private readonly Dictionary<BulletHandle, Vector3> launchPositions =
        new Dictionary<BulletHandle, Vector3>();

    private HUDSettingSign selectedSign;

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
    }

    private void OnDisable()
    {
        BulletHandle.BulletLaunched -= OnBulletLaunched;
        BulletHandle.BulletSettled -= OnBulletSettled;

        launchPositions.Clear();
    }

    // 选中某个选项牌；重复命中同一选项是幂等的
    public void SelectSign(HUDSettingSign sign)
    {
        if (sign == null || sign == selectedSign)
        {
            return;
        }

        selectedSign?.SetSelected(false);
        selectedSign = sign;
        selectedSign.SetSelected(true);

        ApplyHudVisibility(sign.HideHud);
    }

    // 射击"是"禁用所有 HUD 容器，射击"否"重新启用
    private void ApplyHudVisibility(bool hide)
    {
        foreach (GameObject container in hudContainers)
        {
            if (container != null)
            {
                container.SetActive(!hide);
            }
        }
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
            HUDSettingSign sign =
                hit.collider.GetComponentInParent<HUDSettingSign>();

            if (sign != null)
            {
                SelectSign(sign);
            }
        }
    }
}
