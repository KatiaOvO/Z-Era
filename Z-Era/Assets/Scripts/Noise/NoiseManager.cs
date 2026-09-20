using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局玩法噪声管理器。
/// 
/// 职责：
/// 1. 接收 NoiseEmitter 发出的噪声。
/// 2. 在噪声半径内查找 Zombie 图层上的 Collider。
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
    [Tooltip("用于查找能够听到噪声的 Zombie 图层")]
    [SerializeField]
    private LayerMask zombieLayer;

    [Tooltip("一次噪声查询最多接收多少个 Collider")]
    [SerializeField, Min(1)]
    private int maxNoiseResults = 32;

    [Header("Debug")]
    [Tooltip("开启后在 Console 中输出每次噪声通知")]
    [SerializeField]
    private bool logNoiseEvents;

    // 复用查询数组，避免每次发射噪声都产生新的数组分配。
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

        overlapBuffer = new Collider[maxNoiseResults];

        // 没有手动配置时，按名称自动查找 Zombie 图层。
        if (zombieLayer.value == 0)
        {
            zombieLayer = LayerMask.GetMask("Zombie");
        }

        if (zombieLayer.value == 0)
        {
            Debug.LogWarning(
                "NoiseManager：没有配置 Zombie 图层。",
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

        // 查询噪声范围内所有 Zombie 图层上的 Collider。
        // QueryTriggerInteraction.Collide 允许命中 Zombie 身体部位触发器。
        int hitCount = Physics.OverlapSphereNonAlloc(
            noiseEvent.position,
            radius,
            overlapBuffer,
            zombieLayer,
            QueryTriggerInteraction.Collide
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

            // 接口可能挂在 Collider 的父物体上，
            // 因此必须使用 GetComponentInParent。
            INoiseListener listener =
                hitCollider.GetComponentInParent<INoiseListener>();

            if (listener == null)
            {
                continue;
            }

            // 同一个 Zombie 的多个碰撞体只通知一次。
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

    private void OnValidate()
    {
        maxNoiseResults = Mathf.Max(1, maxNoiseResults);
    }
}