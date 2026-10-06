using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 帧率限制下拉框：挂在 SceneSettingPanel 下的 FPSDropdown 上
/// （需有 TMP_Dropdown 组件）。
/// 选项在 Awake 中由 SettingsManager.FrameRateValues 自动生成
/// （60 / 90 / 120 / 无限制），会覆盖在检查器里手动配置的选项，
/// 因此选项文字和数值永远保持一致，无需手动维护。
/// 切换时转发给 SettingsManager 全局应用并持久化；
/// 面板每次打开同步到当前保存的帧率。
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class FrameRateDropdown : MonoBehaviour
{
    private TMP_Dropdown dropdown;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();

        var options = new List<string>();

        foreach (int fps in SettingsManager.FrameRateValues)
        {
            options.Add(fps == -1 ? "无限制" : fps + " FPS");
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
    }

    private void OnEnable()
    {
        // 用 SetValueWithoutNotify 避免同步初值时触发一次多余的保存
        dropdown.SetValueWithoutNotify(
            FrameRateToIndex(SettingsManager.TargetFrameRate));

        dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable()
    {
        dropdown.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(int index)
    {
        if (index < 0 || index >= SettingsManager.FrameRateValues.Length)
        {
            return;
        }

        SettingsManager.Instance.SetFrameRate(
            SettingsManager.FrameRateValues[index]);
    }

    // 保存的帧率数值 → 下拉框选项序号；未知数值回落到"无限制"
    private static int FrameRateToIndex(int fps)
    {
        for (int i = 0; i < SettingsManager.FrameRateValues.Length; i++)
        {
            if (SettingsManager.FrameRateValues[i] == fps)
            {
                return i;
            }
        }

        return SettingsManager.FrameRateValues.Length - 1;
    }
}
