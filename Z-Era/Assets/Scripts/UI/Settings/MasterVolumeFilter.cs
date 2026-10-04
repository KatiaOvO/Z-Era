using UnityEngine;

/// <summary>
/// 全局音量增益：由 SettingsManager 自动添加到 AudioListener 所在
/// 物体上（每个场景加载后检查补挂），通过 OnAudioFilterRead 在
/// 最终输出前放大所有声音——AudioListener.volume 最大只能到 1
/// （原生响度），突破上限只能在这一层做软件增益。
/// 当前增益倍数由 SettingsManager.VolumeGain 根据音量滑条提供
/// （滑条 0.5 以内为 1 倍即原生，向最大档线性升到 2 倍）。
/// 低于阈值的采样原样通过，只有增益后超过阈值的峰值进入软限幅
/// 平滑压回，避免硬削波产生的破音。
/// </summary>
public class MasterVolumeFilter : MonoBehaviour
{
    // 软限幅阈值：幅度低于此值的采样不受任何影响
    private const float LimiterThreshold = 0.8f;

    // 仅当本组件与 AudioListener 在同一物体上时才会被调用，
    // 运行在音频线程：循环内不得产生 GC 分配
    private void OnAudioFilterRead(float[] data, int channels)
    {
        float gain = SettingsManager.VolumeGain;

        for (int i = 0; i < data.Length; i++)
        {
            float sample = data[i] * gain;
            float magnitude = Mathf.Abs(sample);

            if (magnitude > LimiterThreshold)
            {
                // 阈值以上用 tanh 平滑压缩回 ±1：
                // 超出量越大压得越狠，过渡连续无爆点
                // （Mathf 没有 Tanh，用 System.Math 的双精度版本）
                float sign = Mathf.Sign(sample);
                float over =
                    (magnitude - LimiterThreshold) /
                    (1f - LimiterThreshold);

                sample = sign *
                    (LimiterThreshold +
                     (1f - LimiterThreshold) *
                     (float)System.Math.Tanh(over));
            }

            data[i] = sample;
        }
    }
}
