using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局音量滑条：挂在设置面板的 VolumeSlider 物体上（需有 Slider 组件）。
/// 面板每次打开时把滑条同步到当前保存的音量值，
/// 拖动时转发给 SettingsManager 实时应用并保存。
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        // 用 SetValueWithoutNotify 避免同步初值时触发一次多余的保存
        slider.SetValueWithoutNotify(SettingsManager.Instance.GetVolume());
        slider.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(float value)
    {
        SettingsManager.Instance.SetVolume(value);
    }
}
