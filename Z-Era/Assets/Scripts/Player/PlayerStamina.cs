using System;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("体力值")]
    [Tooltip("玩家最大体力值")]
    [SerializeField, Min(1f)]
    private float maxStamina = 100f;

    [Header("匕首攻击")]
    [Tooltip("每次匕首攻击消耗的体力值")]
    [SerializeField, Min(0f)]
    private float knifeAttackStaminaCost = 20f;

    [Header("体力恢复")]
    [Tooltip("匕首攻击结束后，经过多久开始恢复体力（秒）")]
    [SerializeField, Min(0f)]
    private float staminaRecoveryDelay = 2f;

    [Tooltip("体力恢复速度（每秒恢复多少点）")]
    [SerializeField, Min(0f)]
    private float staminaRecoveryPerSecond = 20f;

    private float currentStamina;
    private bool isRecoveryPaused;
    private bool hasRecoverySchedule;
    private float recoveryStartTime;

    public event Action<float, float> StaminaChanged;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float KnifeAttackStaminaCost => knifeAttackStaminaCost;

    public float NormalizedStamina
    {
        get
        {
            if (maxStamina <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(currentStamina / maxStamina);
        }
    }

    private void Awake()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        currentStamina = maxStamina;
    }

    private void Update()
    {
        if (currentStamina >= maxStamina)
        {
            if (Mathf.Approximately(currentStamina, maxStamina))
            {
                currentStamina = maxStamina;
                hasRecoverySchedule = false;
            }

            return;
        }

        if (isRecoveryPaused || !hasRecoverySchedule)
        {
            return;
        }

        if (Time.time < recoveryStartTime)
        {
            return;
        }

        float previousStamina = currentStamina;

        currentStamina = Mathf.MoveTowards(
            currentStamina,
            maxStamina,
            staminaRecoveryPerSecond * Time.deltaTime
        );

        if (!Mathf.Approximately(
            previousStamina,
            currentStamina
        ))
        {
            RaiseStaminaChanged();
        }
    }

    /// <summary>
    /// 尝试消耗一次匕首攻击体力。
    /// 只有体力足够时才会扣除，并暂停恢复计时。
    /// </summary>
    public bool TryConsumeKnifeAttackStamina()
    {
        if (currentStamina + 0.001f < knifeAttackStaminaCost)
        {
            return false;
        }

        currentStamina = Mathf.Max(
            0f,
            currentStamina - knifeAttackStaminaCost
        );

        // 匕首攻击进行期间不恢复体力。
        isRecoveryPaused = true;
        hasRecoverySchedule = false;

        RaiseStaminaChanged();
        return true;
    }

    /// <summary>
    /// 匕首攻击动画退出后调用。
    /// 从这里开始计算体力恢复延迟。
    /// </summary>
    public void NotifyKnifeAttackEnded()
    {
        isRecoveryPaused = false;

        if (currentStamina >= maxStamina)
        {
            currentStamina = maxStamina;
            hasRecoverySchedule = false;
            return;
        }

        hasRecoverySchedule = true;
        recoveryStartTime =
            Time.time + Mathf.Max(0f, staminaRecoveryDelay);
    }

    /// <summary>
    /// 如果武器在匕首动画结束前被禁用或切换，强制开始恢复计时。
    /// </summary>
    public void CancelKnifeAttackRecoveryPause()
    {
        if (!isRecoveryPaused)
        {
            return;
        }

        NotifyKnifeAttackEnded();
    }

    public void ResetStamina()
    {
        currentStamina = maxStamina;
        isRecoveryPaused = false;
        hasRecoverySchedule = false;

        RaiseStaminaChanged();
    }

    private void RaiseStaminaChanged()
    {
        StaminaChanged?.Invoke(
            currentStamina,
            maxStamina
        );
    }

    private void OnValidate()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        knifeAttackStaminaCost =
            Mathf.Max(0f, knifeAttackStaminaCost);
        staminaRecoveryDelay =
            Mathf.Max(0f, staminaRecoveryDelay);
        staminaRecoveryPerSecond =
            Mathf.Max(0f, staminaRecoveryPerSecond);

        if (Application.isPlaying &&
            currentStamina > maxStamina)
        {
            currentStamina = maxStamina;
        }
    }
}