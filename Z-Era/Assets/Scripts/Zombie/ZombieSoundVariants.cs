using UnityEngine;

/// <summary>
/// 僵尸音效系统，与 ZombieAnimationVariants 配套使用。
/// 每次启用时从四类音效组中各随机选定一个，
/// 之后根据 Animator 当前状态决定何时播放：
/// - Idle / Walk：按最小到最大之间的随机间隔循环播放 NormalState 音效；
/// - Attack / Run / Die：每次进入对应状态只播放一次。
/// 音频使用根物体上已挂载的 AudioSource，空间化设置由该组件控制。
/// </summary>
[RequireComponent(typeof(Animator))]
public class ZombieSoundVariants : MonoBehaviour
{
    [Header("音效组（每次启用随机各选一个）")]

    [Tooltip("Idle/Walk 状态下按随机间隔播放的音效")]
    [SerializeField]
    private AudioClip[] normalStateClips;

    [Tooltip("进入 Attack 状态时播放一次的音效")]
    [SerializeField]
    private AudioClip[] attackClips;

    [Tooltip("进入 Run 状态时播放一次的音效")]
    [SerializeField]
    private AudioClip[] runClips;

    [Tooltip("进入 Die 状态时播放一次的音效")]
    [SerializeField]
    private AudioClip[] dieClips;

    [Header("NormalState 播放间隔")]

    [Tooltip("所有僵尸音效的音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float soundVolume = 1f;

    [Tooltip("两次 NormalState 音效之间的最小间隔（秒）")]
    [SerializeField, Min(0.1f)]
    private float ambientIntervalMin = 4f;

    [Tooltip("两次 NormalState 音效之间的最大间隔（秒）")]
    [SerializeField, Min(0.1f)]
    private float ambientIntervalMax = 10f;

    [Header("触发状态名")]

    [Tooltip("处于这些状态时按随机间隔播放 NormalState 音效")]
    [SerializeField]
    private string[] ambientStateNames = { "Idle", "Walk" };

    [Tooltip("进入这些状态时播放 Attack 音效，一次动画只播一次")]
    [SerializeField]
    private string[] attackStateNames = { "Attack", "Attack_" };

    [Tooltip("进入这些状态时播放 Run 音效")]
    [SerializeField]
    private string[] runStateNames = { "Run" };

    [Tooltip("进入这些状态时播放 Die 音效，一次动画只播一次")]
    [SerializeField]
    private string[] dieStateNames = { "Die" };

    [Tooltip("每次对象启用时重新随机。使用对象池复用 Zombie 时建议开启")]
    [SerializeField]
    private bool randomizeOnEnable = true;

    private Animator animator;
    private AudioSource audioSource;

    // 本次启用随机选定的四类音效，
    // 选中后不再变化，直到下一次启用重新随机。
    private AudioClip selectedNormal;
    private AudioClip selectedAttack;
    private AudioClip selectedRun;
    private AudioClip selectedDie;

    // 上一帧的状态哈希，用于检测"进入新状态"这一事件。
    private int lastStateHash;

    // 上一帧是否处于 Idle/Walk 状态。
    private bool lastInAmbientState;

    // 下一次 NormalState 音效的播放时间。
    private float nextAmbientTime;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // AudioSource 由使用者手动挂载在根物体上，
        // 空间化、音量等参数以该组件的检查器配置为准。
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogError(
                "ZombieSoundVariants：根物体上没有 AudioSource，无法播放僵尸音效。",
                this
            );

            return;
        }

        // 僵尸音效必须随距离衰减，强制为 3D 空间音源。
        // 衰减范围用该 AudioSource 检查器上的
        // Min Distance / Max Distance 调节：Min 内全音量，
        // Max 之外基本听不见，中间按曲线衰减。
        audioSource.spatialBlend = 1f;
    }

    private void OnEnable()
    {
        // Zombie 首次生成或从对象池重新启用时，重新随机四类音效。
        if (randomizeOnEnable)
        {
            RandomizeClips();
        }

        // 重置状态跟踪，避免复用时把上一次的状态当作"没有变化"。
        lastStateHash = 0;
        lastInAmbientState = false;

        // 首次 NormalState 音效也在随机延迟之后，
        // 避免一批 Zombie 刷出时同时开口。
        nextAmbientTime = Time.time + GetNextAmbientInterval();
    }

    /// <summary>
    /// 为四类音效重新随机选择一个。
    /// 与 ZombieAnimationVariants 一致：随机结果保存到本实例，
    /// 后续所有播放都使用本次选中的版本，直到再次调用。
    /// </summary>
    public void RandomizeClips()
    {
        selectedNormal = GetRandomClip(normalStateClips);
        selectedAttack = GetRandomClip(attackClips);
        selectedRun = GetRandomClip(runClips);
        selectedDie = GetRandomClip(dieClips);
    }

    private void Update()
    {
        if (animator == null || audioSource == null)
        {
            return;
        }

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        bool inAmbientState =
            IsAnyState(info, ambientStateNames);

        // 通过状态哈希的变化检测"进入新状态"。
        // 状态在内部循环播放时哈希不变，不会重复触发，
        // 这正是 Attack/Die"一次动画只播一次"的实现基础。
        int stateHash = info.shortNameHash;

        if (stateHash != lastStateHash)
        {
            // 只有从非 Idle/Walk 状态进入时才重置随机间隔；
            // Idle 与 Walk 之间来回切换不打断既定的播放节奏。
            if (inAmbientState && !lastInAmbientState)
            {
                nextAmbientTime =
                    Time.time + GetNextAmbientInterval();
            }

            if (IsAnyState(info, attackStateNames))
            {
                PlayOnce(selectedAttack);
            }
            else if (IsAnyState(info, dieStateNames))
            {
                PlayOnce(selectedDie);
            }
            else if (IsAnyState(info, runStateNames))
            {
                PlayOnce(selectedRun);
            }

            lastStateHash = stateHash;
            lastInAmbientState = inAmbientState;
        }

        // Idle/Walk 期间按随机间隔循环播放 NormalState 音效。
        if (inAmbientState && Time.time >= nextAmbientTime)
        {
            PlayOnce(selectedNormal);

            nextAmbientTime =
                Time.time + GetNextAmbientInterval();
        }
    }

    private void PlayOnce(AudioClip clip)
    {
        // 该类音效未配置时静默跳过，不影响其他类别。
        if (clip == null || audioSource == null)
        {
            return;
        }

        // 每只僵尸同一时间只允许一个声音：
        // 播放新音效前先停掉该音源上正在响的旧音效，
        // 例如攻击中被打死时，死亡声会立即取代攻击声，
        // 而不是两种声音叠在一起。
        audioSource.Stop();

        audioSource.PlayOneShot(clip, soundVolume);
    }

    // 在最小值与最大值之间取随机间隔，并容忍检查器中填反了大小。
    private float GetNextAmbientInterval()
    {
        float min =
            Mathf.Min(ambientIntervalMin, ambientIntervalMax);

        float max =
            Mathf.Max(ambientIntervalMin, ambientIntervalMax);

        return Random.Range(min, max);
    }

    /// <summary>
    /// 从音频数组中随机选择一个非空音频。
    /// 不使用简单的 Random.Range(0, length)，
    /// 是因为数组中可能存在没有填写的空引用。
    /// </summary>
    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int validCount = 0;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, validCount);

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (randomIndex == 0)
            {
                return clips[i];
            }

            randomIndex--;
        }

        return null;
    }

    // 判断当前动画状态是否命中任意一个配置的状态名。
    private bool IsAnyState(
        AnimatorStateInfo info,
        string[] stateNames
    )
    {
        if (stateNames == null)
        {
            return false;
        }

        foreach (string stateName in stateNames)
        {
            if (!string.IsNullOrEmpty(stateName) &&
                info.IsName(stateName))
            {
                return true;
            }
        }

        return false;
    }
}
