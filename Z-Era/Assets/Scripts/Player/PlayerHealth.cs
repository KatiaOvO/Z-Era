using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    #region 生命值参数

    [Header("Health")]
    [Tooltip("玩家最大生命值")]
    [SerializeField, Min(1f)]
    private float maxHealth = 100f;

    [Tooltip("两次受伤之间的最短间隔，用于防止攻击触发器每帧重复扣血")]
    [SerializeField, Min(0f)]
    private float damageInvincibilityTime = 0.1f;

    [Header("受伤减速")]
    [Tooltip("玩家受伤后降低的速度百分比")]
    [SerializeField, Range(0f, 100f)]
    private float damageSpeedReductionPercent = 50f;

    [Tooltip("玩家受伤后的减速持续时间（秒）")]
    [SerializeField, Min(0f)]
    private float damageSlowDuration = 1f;

    #endregion

    #region 运行时状态

    // 当前生命值，只允许通过 TakeDamage、Heal 和 ResetHealth 修改。
    private float currentHealth;

    // 下一次允许受到伤害的时间点。
    private float nextDamageTime;

    // 确保死亡逻辑和 Died 事件只执行一次。
    private bool isDead;

    // 受伤减速状态。
    private PlayerController playerController;
    private float originalWalkSpeed;
    private float speedRestoreTime;
    private bool isSpeedReduced;

    #endregion

    #region 对外事件

    // 生命值发生变化时触发，参数依次为当前生命值和最大生命值。
    public event Action<float, float> HealthChanged;

    // 玩家成功受到一次伤害后触发。
    public event Action<DamageInfo> Damaged;

    // 玩家进入死亡状态时触发一次。
    public event Action Died;

    #endregion

    #region 只读属性

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // 返回 0 到 1 之间的生命值比例，可以直接用于血条填充值。
    public float NormalizedHealth
    {
        get
        {
            if (maxHealth <= 0f)
            {
                return 0f;
            }

            return currentHealth / maxHealth;
        }
    }

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        // 游戏开始时将生命值恢复为最大值。
        currentHealth = maxHealth;

        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (isSpeedReduced &&
            Time.time >= speedRestoreTime)
        {
            RestoreMovementSpeed();
        }
    }

    private void OnDisable()
    {
        RestoreMovementSpeed();
    }

    #endregion

    #region 对外方法

    // 实现 IDamageable 接口。
    // 子弹、僵尸攻击、爆炸等伤害来源最终都通过这个方法进入玩家受伤逻辑。
    public void TakeDamage(DamageInfo damageInfo)
    {
        // 已经死亡或伤害值小于等于 0 时不处理。
        if (isDead || damageInfo.damage <= 0f)
        {
            return;
        }

        // 无敌时间内重复触发的伤害会被忽略。
        if (Time.time < nextDamageTime)
        {
            return;
        }

        nextDamageTime = Time.time + damageInvincibilityTime;

        // 统一计算最终伤害。
        // 后续护甲、减伤和难度倍率都集中在这里处理。
        float finalDamage = CalculateFinalDamage(damageInfo);

        currentHealth = Mathf.Max(0f, currentHealth - finalDamage);

        ApplyDamageSlow();

        // 通知外部生命值已变化。
        HealthChanged?.Invoke(currentHealth, maxHealth);

        // 通知外部玩家受到了伤害。
        Damaged?.Invoke(damageInfo);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // 治疗玩家，治疗量不会超过最大生命值。
    public void Heal(float amount)
    {
        // 死亡状态不能直接通过治疗复活。
        if (isDead || amount <= 0f)
        {
            return;
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        // 只有生命值真正变化时才发送事件。
        if (!Mathf.Approximately(previousHealth, currentHealth))
        {
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    // 将玩家恢复到完整生命值，同时清除死亡和无敌状态。
    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        nextDamageTime = 0f;

        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    #endregion

    #region 内部逻辑

    // 受伤后临时降低移动速度，连续受伤只延长减速结束时间。
    private void ApplyDamageSlow()
    {
        if (playerController == null)
        {
            return;
        }

        if (!isSpeedReduced)
        {
            originalWalkSpeed =
                playerController.walkSpeed;
        }

        float speedMultiplier =
            1f - damageSpeedReductionPercent / 100f;

        playerController.walkSpeed =
            originalWalkSpeed *
            Mathf.Clamp01(speedMultiplier);

        isSpeedReduced = true;

        if (damageSlowDuration <= 0f)
        {
            RestoreMovementSpeed();
            return;
        }

        speedRestoreTime =
            Time.time + damageSlowDuration;
    }

    private void RestoreMovementSpeed()
    {
        if (!isSpeedReduced)
        {
            return;
        }

        if (playerController != null)
        {
            playerController.walkSpeed =
                originalWalkSpeed;
        }

        isSpeedReduced = false;
    }

    // 统一计算玩家最终受到的伤害。
    // 以后可以在这里加入护甲、伤害倍率和特定来源抗性。
    private float CalculateFinalDamage(DamageInfo damageInfo)
    {
        return Mathf.Max(0f, damageInfo.damage);
    }

    // 统一处理玩家死亡。
    private void Die()
    {
        // 防止 Died 事件重复触发。
        if (isDead)
        {
            return;
        }

        isDead = true;
        Died?.Invoke();
    }

    #endregion

    #region Inspector 校验

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        damageInvincibilityTime =
            Mathf.Max(0f, damageInvincibilityTime);
        damageSpeedReductionPercent =
            Mathf.Clamp(
                damageSpeedReductionPercent,
                0f,
                100f
            );
        damageSlowDuration =
            Mathf.Max(0f, damageSlowDuration);
    }

    #endregion
}
