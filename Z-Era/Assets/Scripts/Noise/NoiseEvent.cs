using UnityEngine;

/// <summary>
/// 玩法噪声类型。
/// 只影响 AI 行为，不对应具体音频资源。
/// </summary>
public enum NoiseType
{
    Gunshot,
    Reload,
    Footstep,
    WeaponSwitch
}

/// <summary>
/// 传递给 Zombie 的玩法噪声事件。
/// </summary>
public struct NoiseEvent
{
    // 噪声发生的世界坐标。
    public Vector3 position;

    // 发出噪声的对象。
    public GameObject source;

    // AI 能听到该噪声的最大距离。
    public float radius;

    // 噪声调查优先级。
    public float priority;

    // 噪声类型。
    public NoiseType type;

    // 是否允许穿过墙体传播。
    public bool throughWalls;

    // 声音强度，可用于后续扩展。
    public float loudness;

    // 该声音一次能够增加的怒气值。
    public float angerValue;
}