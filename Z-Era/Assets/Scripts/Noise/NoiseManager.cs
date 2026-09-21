using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局玩法噪声管理器。
/// 
/// 职责：
/// 1. 接收 NoiseEmitter 发出的噪声。
/// 2. 在噪声半径内查找 ZombieHearing 图层上的听觉触发器。
/// 3. 获取 INoiseListener。
/// 4. 调用 HearNoise()。
/// 
/// 它不直接依赖 ZombieAI，
/// 而是通过 INoiseListener 接口处理所有可听噪声的目标。
/// </summary>
[DisallowMultipleComponent]
public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance { get; private set; }

    [Header("Detection")]
    [Tooltip("用于查找能够听到噪声的 ZombieHearing 图层")]
    [SerializeField]
    private LayerMask noiseListenerLayer;

    [Tooltip("一次噪声查询缓冲区的初始容量，结果填满时会自动扩容")]
    [SerializeField, Min(1)]
    private int maxNoiseResults = 32;

    [Header("Debug")]
    [Tooltip("开启后在 Console 中输出每次噪声通知")]
    [SerializeField]
    private bool logNoiseEvents;

    // 复用查询数组，避免每次发射噪声都产生新的数组分配。
    // 如果初始容量不足以容纳全部碰撞体，会在查询时自动扩容。
    private Collider[] overlapBuffer;

    // 同一个 Zombie 可能有多个碰撞体。
    // 使用 HashSet 防止同一个 INoiseListener 被重复通知。
    private readonly HashSet<INoiseListener> listenerBuffer =
        new HashSet<INoiseListener>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "NoiseManager：场景中存在多个 NoiseManager。",
                this
            );
            return;
        }

        Instance = this;

        overlapBuffer = new Collider[Mathf.Max(1, maxNoiseResults)];

        // 没有手动配置时，按名称自动查找 ZombieHearing 图层。
        if (noiseListenerLayer.value == 0)
        {
            noiseListenerLayer =
                LayerMask.GetMask("ZombieHearing");
        }

        if (noiseListenerLayer.value == 0)
        {
            Debug.LogWarning(
                "NoiseManager：没有配置 ZombieHearing 图层。",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 发出一个玩法噪声。
    /// </summary>
    public void Emit(NoiseEvent noiseEvent)
    {
        float radius = Mathf.Max(0f, noiseEvent.radius);

        if (radius <= 0f || overlapBuffer == null)
        {
            return;
        }

        // 只查询 ZombieHearing 图层上的专用听觉触发器。
        // 这样每只 Zombie 在查询结果中只会占一个 Collider，
        // 不会再把头部、躯干、四肢等命中碰撞体全部计算一次。
        int hitCount = QueryNoiseColliders(
            noiseEvent.position,
            radius
        );

        // 每次发射噪声前清空去重集合。
        listenerBuffer.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapBuffer[i];

            if (hitCollider == null)
            {
                continue;
            }

            // 接口挂在听觉触发器的父物体上，
            // 因此必须使用 GetComponentInParent。
            INoiseListener listener =
                hitCollider.GetComponentInParent<INoiseListener>();

            if (listener == null)
            {
                continue;
            }

            // 听觉触发器通常已经做到每个 Zombie 只有一个，
            // 这里保留去重逻辑，防止特殊情况下的重复通知。
            if (!listenerBuffer.Add(listener))
            {
                continue;
            }

            listener.HearNoise(noiseEvent);
        }

        if (logNoiseEvents)
        {
            Debug.Log(
                $"NoiseManager：发出 {noiseEvent.type}，" +
                $"位置 {noiseEvent.position}，" +
                $"范围 {radius}，命中对象 {listenerBuffer.Count}。",
                this
            );
        }
    }

    /// <summary>
    /// 查询噪声范围内的听觉触发器。
    /// 
    /// 如果结果数量等于当前缓冲区长度，说明结果可能已经被截断。
    /// 此时将缓冲区容量翻倍并重新查询，
    /// 直到本次结果可以完整放入缓冲区为止。
    /// </summary>
    private int QueryNoiseColliders(
        Vector3 position,
        float radius
    )
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            position,
            radius,
            overlapBuffer,
            noiseListenerLayer,
            QueryTriggerInteraction.Collide
        );

        // 结果填满缓冲区时继续扩容重查，
        // 避免大量 Zombie 占满固定容量后只通知前几只 Zombie。
        while (hitCount == overlapBuffer.Length)
        {
            int newBufferSize = overlapBuffer.Length * 2;
            overlapBuffer = new Collider[newBufferSize];

            hitCount = Physics.OverlapSphereNonAlloc(
                position,
                radius,
                overlapBuffer,
                noiseListenerLayer,
                QueryTriggerInteraction.Collide
            );
        }

        return hitCount;
    }

    private void OnValidate()
    {
        maxNoiseResults = Mathf.Max(1, maxNoiseResults);
    }
}