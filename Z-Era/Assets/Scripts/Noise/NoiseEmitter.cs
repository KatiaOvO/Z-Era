using UnityEngine;

/// <summary>
/// 玩法噪声发射器。
/// 
/// 负责构造 NoiseEvent，并交给 NoiseManager。
/// 不负责播放 AudioSource。
/// </summary>
[DisallowMultipleComponent]
public class NoiseEmitter : MonoBehaviour
{
    [Tooltip("噪声管理器。留空时自动查找场景中的 NoiseManager")]
    [SerializeField]
    private NoiseManager noiseManager;

    private bool hasWarnedMissingManager;

    private void Awake()
    {
        ResolveManager();
    }

    private void OnEnable()
    {
        ResolveManager();
    }

    /// <summary>
    /// 发出通用噪声。
    /// angerValue 表示这次声音基础增加多少怒气。
    /// </summary>
    public bool EmitNoise(
        NoiseType type,
        Vector3 position,
        float radius,
        float priority,
        bool throughWalls,
        float angerValue,
        float loudness = 1f,
        GameObject source = null
    )
    {
        ResolveManager();

        if (noiseManager == null)
        {
            if (!hasWarnedMissingManager)
            {
                Debug.LogWarning(
                    "NoiseEmitter：场景中没有可用的 NoiseManager。",
                    this
                );
                hasWarnedMissingManager = true;
            }

            return false;
        }

        NoiseEvent noiseEvent = new NoiseEvent
        {
            position = position,
            source = source != null ? source : gameObject,
            radius = Mathf.Max(0f, radius),
            priority = priority,
            type = type,
            throughWalls = throughWalls,
            loudness = Mathf.Max(0f, loudness),
            angerValue = Mathf.Max(0f, angerValue)
        };

        noiseManager.Emit(noiseEvent);
        return true;
    }

    /// <summary>
    /// 发出枪声。
    /// </summary>
    public bool EmitGunshot(
        Vector3 position,
        float radius,
        float angerValue,
        float priority = 4f,
        bool throughWalls = true,
        float loudness = 1f
    )
    {
        return EmitNoise(
            NoiseType.Gunshot,
            position,
            radius,
            priority,
            throughWalls,
            angerValue,
            loudness
        );
    }

    /// <summary>
    /// 发出换弹声。
    /// </summary>
    public bool EmitReload(
        Vector3 position,
        float radius,
        float angerValue,
        float priority = 2f,
        bool throughWalls = false,
        float loudness = 0.7f
    )
    {
        return EmitNoise(
            NoiseType.Reload,
            position,
            radius,
            priority,
            throughWalls,
            angerValue,
            loudness
        );
    }

    /// <summary>
    /// 发出脚步噪声。
    /// </summary>
    public bool EmitFootstep(
        Vector3 position,
        float radius,
        float priority,
        float angerValue,
        bool throughWalls = false,
        float loudness = 0.4f
    )
    {
        return EmitNoise(
            NoiseType.Footstep,
            position,
            radius,
            priority,
            throughWalls,
            angerValue,
            loudness
        );
    }

    /// <summary>
    /// 发出取枪、收枪等武器操作噪声。
    /// 当前项目没有调用它。
    /// </summary>
    public bool EmitWeaponSwitch(
        Vector3 position,
        float radius,
        float angerValue = 0f,
        float priority = 1f,
        bool throughWalls = false,
        float loudness = 0.4f
    )
    {
        return EmitNoise(
            NoiseType.WeaponSwitch,
            position,
            radius,
            priority,
            throughWalls,
            angerValue,
            loudness
        );
    }

    private void ResolveManager()
    {
        if (noiseManager == null)
        {
            noiseManager = NoiseManager.Instance;
        }

        if (noiseManager != null)
        {
            hasWarnedMissingManager = false;
        }
    }
}