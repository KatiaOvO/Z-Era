/// <summary>
/// 能够接收玩法噪声的接口。
/// 
/// NoiseManager 只依赖这个接口，
/// 不需要知道目标是不是 ZombieAI。
/// </summary>
public interface INoiseListener
{
    /// <summary>
    /// 接收到一个噪声事件。
    /// </summary>
    void HearNoise(NoiseEvent noiseEvent);
}