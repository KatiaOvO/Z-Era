using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 自定义准星面板：挂在准星页签的 CrosshairPanel 容器上。
/// 子控件按命名约定自动收集（缺失的控件跳过并警告）：
/// - WidthSlider + WidthInput：线条粗细（滑条与输入字段双向同步）；
/// - LengthSlider + LengthInput：线条长短；
/// - GapSlider + GapInput：线条距中心的间隙（0 = 十字相连）；
/// - OpacitySlider + OpacityInput：线条透明度（0 透明 ~ 1 不透明）；
/// - DotToggle：是否启用中心点（固定在原点）；
/// - DynamicToggle：是否启用动态准星（射击时四条线向外扩散）；
/// - CrosshairPreview：预览准星物体（组件自动添加）。
/// 输入字段显示两位小数，超出范围的输入在编辑结束时钳到最值。
/// 所有修改写入 SettingsManager（实时应用到游戏内准星）并持久化，
/// 面板每次打开把控件同步到当前保存值。
/// 滑条的 min/max 应与 CrosshairConfig 的范围常量一致，
/// 滑条值即配置值本身，无需额外映射。
/// </summary>
public class CrosshairSettingsPanel : MonoBehaviour
{
    private Slider widthSlider;
    private TMP_InputField widthInput;
    private Slider lengthSlider;
    private TMP_InputField lengthInput;
    private Slider gapSlider;
    private TMP_InputField gapInput;
    private Slider opacitySlider;
    private TMP_InputField opacityInput;

    private Toggle dotToggle;
    private Toggle dynamicToggle;

    private CrosshairPreview preview;

    private bool bound;
    private bool syncing;

    private void Awake()
    {
        widthSlider = FindSlider("WidthSlider");
        widthInput = FindInput("WidthInput");
        lengthSlider = FindSlider("LengthSlider");
        lengthInput = FindInput("LengthInput");
        gapSlider = FindSlider("GapSlider");
        gapInput = FindInput("GapInput");
        opacitySlider = FindSlider("OpacitySlider");
        opacityInput = FindInput("OpacityInput");

        dotToggle = FindToggle("DotToggle");
        dynamicToggle = FindToggle("DynamicToggle");

        BindPreview();
    }

    private void OnEnable()
    {
        RefreshControls();
        RefreshPreview();

        if (!bound)
        {
            BindListeners();
            bound = true;
        }
    }

    private Slider FindSlider(string name)
    {
        return FindComponent<Slider>(name);
    }

    private TMP_InputField FindInput(string name)
    {
        return FindComponent<TMP_InputField>(name);
    }

    private Toggle FindToggle(string name)
    {
        return FindComponent<Toggle>(name);
    }

    private T FindComponent<T>(string name) where T : Component
    {
        Transform child = transform.Find(name);

        if (child == null)
        {
            Debug.LogWarning(
                $"CrosshairSettingsPanel：找不到子物体 {name}，" +
                "对应设置项不可用。",
                this
            );
            return null;
        }

        return child.GetComponent<T>();
    }

    private void BindPreview()
    {
        Transform previewTransform = transform.Find("CrosshairPreview");

        if (previewTransform == null)
        {
            Debug.LogWarning(
                "CrosshairSettingsPanel：找不到子物体 " +
                "CrosshairPreview，预览不可用。",
                this
            );
            return;
        }

        preview = previewTransform.GetComponent<CrosshairPreview>();

        if (preview == null)
        {
            preview =
                previewTransform.gameObject
                    .AddComponent<CrosshairPreview>();
        }
    }

    private void BindListeners()
    {
        // 粗细 / 长短 / 间隙 / 透明度：滑条统一 0~1，
        // 按配置范围线性映射到实际数值，与输入字段双向同步
        BindPair(
            widthSlider,
            widthInput,
            CrosshairConfig.MinLineWidth,
            CrosshairConfig.MaxLineWidth,
            value => ModifyConfig(config => config.lineWidth = value),
            () => SettingsManager.Crosshair.lineWidth);

        BindPair(
            lengthSlider,
            lengthInput,
            CrosshairConfig.MinLineLength,
            CrosshairConfig.MaxLineLength,
            value => ModifyConfig(config => config.lineLength = value),
            () => SettingsManager.Crosshair.lineLength);

        BindPair(
            gapSlider,
            gapInput,
            CrosshairConfig.MinGap,
            CrosshairConfig.MaxGap,
            value => ModifyConfig(config => config.gap = value),
            () => SettingsManager.Crosshair.gap);

        BindPair(
            opacitySlider,
            opacityInput,
            CrosshairConfig.MinOpacity,
            CrosshairConfig.MaxOpacity,
            value => ModifyConfig(config => config.opacity = value),
            () => SettingsManager.Crosshair.opacity);

        if (dotToggle != null)
        {
            dotToggle.onValueChanged.AddListener(
                value => ModifyConfig(
                    config => config.dotEnabled = value));
        }

        if (dynamicToggle != null)
        {
            dynamicToggle.onValueChanged.AddListener(
                value => ModifyConfig(
                    config => config.dynamicCrosshairEnabled = value));
        }
    }

    // 绑定一组滑条 + 输入字段：
    // - 滑条统一 0~1（编辑器无需配置范围），线性映射到
    //   [minValue, maxValue] 的实际数值；
    // - 输入字段直接显示实际数值（两位小数），与滑条按比例同步；
    // - 输入过程中：能解析就实时同步滑条与应用，解析失败
    //   （删空、正在敲小数点等中间状态）不打断打字；
    // - 编辑结束（回车/失焦）：解析失败恢复当前值，越界由
    //   SetCrosshairConfig 钳到最值，然后把滑条和字段都同步为
    //   实际生效值并归一化成两位小数
    private void BindPair(
        Slider slider,
        TMP_InputField input,
        float minValue,
        float maxValue,
        System.Action<float> apply,
        System.Func<float> getCurrent)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.AddListener(t =>
        {
            if (syncing)
            {
                return;
            }

            syncing = true;

            float value = Mathf.Lerp(minValue, maxValue, t);

            if (input != null)
            {
                input.SetTextWithoutNotify(
                    value.ToString(
                        "F2",
                        CultureInfo.InvariantCulture));
            }

            syncing = false;

            apply(value);
        });

        if (input == null)
        {
            return;
        }

        input.onValueChanged.AddListener(text =>
        {
            if (syncing || !TryParseValue(text, out float value))
            {
                return;
            }

            syncing = true;
            slider.SetValueWithoutNotify(
                Mathf.InverseLerp(minValue, maxValue, value));
            syncing = false;

            apply(value);
        });

        input.onEndEdit.AddListener(text =>
        {
            if (!TryParseValue(text, out float value))
            {
                value = getCurrent();
            }

            apply(value);

            // 读回实际生效值（越界输入会被钳到最值）
            float actual = getCurrent();

            syncing = true;
            slider.SetValueWithoutNotify(
                Mathf.InverseLerp(minValue, maxValue, actual));
            input.SetTextWithoutNotify(
                actual.ToString("F2", CultureInfo.InvariantCulture));
            syncing = false;
        });
    }

    private void ModifyConfig(System.Action<CrosshairConfig> apply)
    {
        CrosshairConfig config = CopyCurrent();
        apply(config);
        SettingsManager.Instance.SetCrosshairConfig(config);

        RefreshPreview();
    }

    // 面板打开或配置变化时，把控件同步到当前保存值
    private void RefreshControls()
    {
        CrosshairConfig config = SettingsManager.Crosshair;

        syncing = true;

        if (widthSlider != null)
        {
            widthSlider.SetValueWithoutNotify(
                Mathf.InverseLerp(
                    CrosshairConfig.MinLineWidth,
                    CrosshairConfig.MaxLineWidth,
                    config.lineWidth));
        }

        if (widthInput != null)
        {
            widthInput.SetTextWithoutNotify(
                config.lineWidth.ToString(
                    "F2",
                    CultureInfo.InvariantCulture));
        }

        if (lengthSlider != null)
        {
            lengthSlider.SetValueWithoutNotify(
                Mathf.InverseLerp(
                    CrosshairConfig.MinLineLength,
                    CrosshairConfig.MaxLineLength,
                    config.lineLength));
        }

        if (lengthInput != null)
        {
            lengthInput.SetTextWithoutNotify(
                config.lineLength.ToString(
                    "F2",
                    CultureInfo.InvariantCulture));
        }

        if (gapSlider != null)
        {
            gapSlider.SetValueWithoutNotify(
                Mathf.InverseLerp(
                    CrosshairConfig.MinGap,
                    CrosshairConfig.MaxGap,
                    config.gap));
        }

        if (gapInput != null)
        {
            gapInput.SetTextWithoutNotify(
                config.gap.ToString(
                    "F2",
                    CultureInfo.InvariantCulture));
        }

        if (opacitySlider != null)
        {
            opacitySlider.SetValueWithoutNotify(config.opacity);
        }

        if (opacityInput != null)
        {
            opacityInput.SetTextWithoutNotify(
                config.opacity.ToString(
                    "F2",
                    CultureInfo.InvariantCulture));
        }

        if (dotToggle != null)
        {
            dotToggle.SetIsOnWithoutNotify(config.dotEnabled);
        }

        if (dynamicToggle != null)
        {
            dynamicToggle.SetIsOnWithoutNotify(
                config.dynamicCrosshairEnabled);
        }

        syncing = false;
    }

    private void RefreshPreview()
    {
        if (preview != null)
        {
            preview.Refresh(SettingsManager.Crosshair);
        }
    }

    private static bool TryParseValue(
        string text,
        out float value)
    {
        // 用固定区域性格式解析，避免系统区域的小数点差异干扰
        return float.TryParse(
            text?.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    // 当前配置的副本：面板单项修改不会直接动全局对象，
    // 统一经 SetCrosshairConfig 钳值、生效与持久化
    private static CrosshairConfig CopyCurrent()
    {
        return SettingsManager.Crosshair.Clone();
    }
}
