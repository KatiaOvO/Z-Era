using UnityEngine;

/// <summary>
/// 管理 Zombie 的怒气值、衰减和激怒状态。
/// 
/// 该组件不负责播放动画，也不负责移动。
/// ZombieAI 根据 IsEnraged 决定移动时使用 Walk 还是 Run。
/// </summary>
[DisallowMultipleComponent]
public class ZombieAnger : MonoBehaviour
{
    [Header("Anger")]
    [Tooltip("最大怒气值，达到后进入激怒状态")]
    [SerializeField, Min(1f)]
    private float maxAnger = 100f;

    [Tooltip("最后一次听到声音后，多久开始降低怒气")]
    [SerializeField, Min(0f)]
    private float angerDecayDelay = 3f;

    [Tooltip("怒气开始下降后，每秒减少多少怒气")]
    [SerializeField, Min(0f)]
    private float angerDecayPerSecond = 8f;

    [Tooltip("进入激怒状态后，至少维持多少秒")]
    [SerializeField, Min(0f)]
    private float minEnragedDuration = 6f;

    [Header("Debug")]
    [Tooltip("开启后在 Console 输出怒气变化。")]
    [SerializeField]
    private bool logAngerChanges;

    private float currentAnger;

    // 最后一次获得怒气的时间。
    private float lastAngerGainTime = float.NegativeInfinity;

    // 在这个时间之前，即使怒气下降，也必须保持激怒状态。
    private float enragedUntilTime;

    public float CurrentAnger => currentAnger;
    public float MaxAnger => maxAnger;

    public float NormalizedAnger
    {
        get
        {
            if (maxAnger <= 0f)
            {
                return 0f;
            }

            return currentAnger / maxAnger;
        }
    }

    /// <summary>
    /// 达到最大怒气，或者仍处于最短激怒时间内，都算激怒。
    /// </summary>
    public bool IsEnraged =>
        currentAnger >= maxAnger ||
        Time.time < enragedUntilTime;

    private void Awake()
    {
        currentAnger = 0f;
    }

    private void Update()
    {
        UpdateAngerDecay();
    }

    /// <summary>
    /// 增加怒气。
    /// 达到最大值时，设置最短激怒持续时间。
    /// </summary>
    public void AddAnger(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        currentAnger = Mathf.Clamp(
            currentAnger + amount,
            0f,
            maxAnger
        );

        lastAngerGainTime = Time.time;

        if (currentAnger >= maxAnger)
        {
            enragedUntilTime = Mathf.Max(
                enragedUntilTime,
                Time.time + minEnragedDuration
            );
        }

        if (logAngerChanges)
        {
            Debug.Log(
                $"ZombieAnger：增加 {amount}，" +
                $"当前怒气 {currentAnger}/{maxAnger}，" +
                $"激怒状态 {IsEnraged}。",
                this
            );
        }
    }

    /// <summary>
    /// 直接根据噪声事件增加怒气。
    /// </summary>
    public void AddNoiseAnger(NoiseEvent noiseEvent)
    {
        AddAnger(noiseEvent.angerValue);
    }

    /// <summary>
    /// 重置怒气与激怒状态。
    /// 可以用于复活或对象池重新初始化。
    /// </summary>
    public void ResetAnger()
    {
        currentAnger = 0f;
        lastAngerGainTime = float.NegativeInfinity;
        enragedUntilTime = 0f;
    }

    private void UpdateAngerDecay()
    {
        if (currentAnger <= 0f)
        {
            currentAnger = 0f;
            return;
        }

        // 最近仍在获得怒气时，不开始衰减。
        if (Time.time - lastAngerGainTime < angerDecayDelay)
        {
            return;
        }

        currentAnger = Mathf.Max(
            0f,
            currentAnger - angerDecayPerSecond * Time.deltaTime
        );
    }

    private void OnValidate()
    {
        maxAnger = Mathf.Max(1f, maxAnger);
        angerDecayDelay = Mathf.Max(0f, angerDecayDelay);
        angerDecayPerSecond = Mathf.Max(0f, angerDecayPerSecond);
        minEnragedDuration = Mathf.Max(0f, minEnragedDuration);
    }
}