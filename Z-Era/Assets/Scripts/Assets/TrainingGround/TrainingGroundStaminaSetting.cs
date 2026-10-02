using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 训练场体力设置管理器：挂在 StaminaSettingRoot 上（或选项牌的任意
/// 父物体），负责两件事：
/// 1. 识别子弹命中的选项牌。子弹命中当帧即被对象池回收，物理触发器
///    事件不一定触发，因此这里订阅 BulletHandle 的 BulletLaunched/
///    BulletSettled 静态事件，用"发射点 → 落点"还原飞行方向，再从
///    落点向后退一点打一根短射线，找出子弹实际命中的碰撞体。
/// 2. 无限体力开启时，逐帧监视 PlayerStamina：体力只通过
///    TryConsumeKnifeAttackStamina 减少且 currentStamina 无公开赋值
///    接口，因此检测到减少时调用公开的 ResetStamina() 立即回满，
///    效果等同"消耗体力立即补满"，体力永远打不空。
/// 只订阅事件、只调用 PlayerStamina 的公开方法，不修改任何根代码；
/// 场景卸载时反订阅事件，不影响其他场景。
/// </summary>
public class TrainingGroundStaminaSetting : MonoBehaviour
{
    [Header("扫描设置")]

    [Tooltip("重新扫描 PlayerStamina 的间隔（秒）")]
    [SerializeField, Min(0.5f)]
    private float staminaScanInterval = 2f;

    private readonly Dictionary<BulletHandle, Vector3> launchPositions =
        new Dictionary<BulletHandle, Vector3>();

    private StaminaSettingSign selectedSign;
    private bool infiniteStamina;
    private float nextStaminaScanTime;

    private PlayerStamina playerStamina;
    private float lastStamina;

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

        ScanStamina();
    }

    private void OnDisable()
    {
        BulletHandle.BulletLaunched -= OnBulletLaunched;
        BulletHandle.BulletSettled -= OnBulletSettled;

        launchPositions.Clear();
    }

    // 选中某个选项牌；重复命中同一选项是幂等的
    public void SelectSign(StaminaSettingSign sign)
    {
        if (sign == null || sign == selectedSign)
        {
            return;
        }

        selectedSign?.SetSelected(false);
        selectedSign = sign;
        selectedSign.SetSelected(true);

        infiniteStamina = sign.EnableInfiniteStamina;

        // 开启无限体力时立即把当前体力回满：
        // 之前攻击扣掉的体力不应等到下一次攻击才被补回
        if (infiniteStamina)
        {
            if (playerStamina == null)
            {
                ScanStamina();
            }

            if (playerStamina != null)
            {
                playerStamina.ResetStamina();
                lastStamina = playerStamina.CurrentStamina;
            }
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextStaminaScanTime)
        {
            ScanStamina();
        }

        RestoreStamina();
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
            StaminaSettingSign sign =
                hit.collider.GetComponentInParent<StaminaSettingSign>();

            if (sign != null)
            {
                SelectSign(sign);
            }
        }
    }

    // 逐帧监视体力：无限体力时，把因攻击造成的减少立即回满；
    // 自然恢复造成的增加则直接跟随基准值
    private void RestoreStamina()
    {
        if (playerStamina == null)
        {
            return;
        }

        float currentStamina = playerStamina.CurrentStamina;

        if (currentStamina > lastStamina)
        {
            lastStamina = currentStamina;
        }
        else if (currentStamina < lastStamina)
        {
            if (infiniteStamina)
            {
                // currentStamina 没有公开赋值接口，只能用公开的
                // ResetStamina 回满；顺带清除的恢复暂停/计时状态
                // 对"体力常满"的表现没有影响
                playerStamina.ResetStamina();
            }

            lastStamina = playerStamina.CurrentStamina;
        }
    }

    private void ScanStamina()
    {
        nextStaminaScanTime = Time.unscaledTime + staminaScanInterval;

        if (playerStamina != null)
        {
            lastStamina = playerStamina.CurrentStamina;
            return;
        }

        playerStamina = FindObjectOfType<PlayerStamina>();

        if (playerStamina != null)
        {
            lastStamina = playerStamina.CurrentStamina;
        }
    }
}
