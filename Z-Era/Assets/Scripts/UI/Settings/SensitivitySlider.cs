using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 灵敏度设置控件：挂在灵敏度滑条物体上（需有 Slider 组件），
/// 并在检查器中把 Input Field 引用指向同面板的 TMP 输入字段。
/// 滑条（进度 0~1）与输入字段（实际灵敏度数值，两位小数）双向
/// 同步：拖动滑条 → 字段同步显示数值；编辑字段 → 滑条同步进度；
/// 字段输入超出允许范围（0.5~5）时自动钳到最值（在编辑结束、
/// 失去焦点或回车时归一化，输入过程中不打断打字）。
/// 修改经 SettingsManager 实时应用并持久化，默认 2 = 原生手感。
/// </summary>
[RequireComponent(typeof(Slider))]
public class SensitivitySlider : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("灵敏度数值输入字段（TextMeshPro），与滑条双向同步")]
    [SerializeField]
    private TMP_InputField inputField;

    private Slider slider;

    // 两个控件互相更新时置位，避免 onValueChanged 回环触发
    private bool syncing;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        // 面板可能被反复开关，每次打开都同步到当前保存值；
        // 用 WithoutNotify 避免同步初值时触发一次多余的保存
        syncing = true;

        slider.SetValueWithoutNotify(
            SettingsManager.SensitivityToSlider(
                SettingsManager.MouseSensitivity));

        if (inputField != null)
        {
            inputField.SetTextWithoutNotify(
                SettingsManager.MouseSensitivity.ToString(
                    "F2",
                    CultureInfo.InvariantCulture));
        }

        syncing = false;

        slider.onValueChanged.AddListener(OnSliderChanged);

        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(
                OnInputFieldChanged);

            inputField.onEndEdit.AddListener(
                OnInputFieldEndEdit);
        }
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnSliderChanged);

        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(
                OnInputFieldChanged);

            inputField.onEndEdit.RemoveListener(
                OnInputFieldEndEdit);
        }
    }

    // 拖动滑条：数值写入字段并应用
    private void OnSliderChanged(float sliderValue)
    {
        if (syncing)
        {
            return;
        }

        syncing = true;

        float sensitivity =
            SettingsManager.SliderToSensitivity(sliderValue);

        if (inputField != null)
        {
            inputField.SetTextWithoutNotify(
                sensitivity.ToString(
                    "F2",
                    CultureInfo.InvariantCulture));
        }

        SettingsManager.Instance.SetSensitivity(sensitivity);

        syncing = false;
    }

    // 编辑字段过程中：能解析出有效数字就实时同步滑条与应用，
    // 解析失败（删空、敲了负号等中间状态）则等待继续输入，
    // 不打断打字也不改写文本
    private void OnInputFieldChanged(string text)
    {
        if (syncing || !TryParseSensitivity(text, out float value))
        {
            return;
        }

        syncing = true;

        slider.SetValueWithoutNotify(
            SettingsManager.SensitivityToSlider(value));

        syncing = false;

        SettingsManager.Instance.SetSensitivity(value);
    }

    // 编辑结束（回车或失去焦点）：解析失败恢复为当前值，
    // 超出范围钳到最值，并把文本归一化为两位小数
    private void OnInputFieldEndEdit(string text)
    {
        if (!TryParseSensitivity(text, out float value))
        {
            value = SettingsManager.MouseSensitivity;
        }

        syncing = true;

        if (inputField != null)
        {
            inputField.SetTextWithoutNotify(
                value.ToString("F2", CultureInfo.InvariantCulture));
        }

        slider.SetValueWithoutNotify(
            SettingsManager.SensitivityToSlider(value));

        syncing = false;

        SettingsManager.Instance.SetSensitivity(value);
    }

    private static bool TryParseSensitivity(
        string text,
        out float value)
    {
        // 用固定区域性格式解析，避免系统区域的小数点差异干扰；
        // 0 与负数视为无效输入（灵敏度必须为正）
        return float.TryParse(
            text?.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value)
            && value > 0f;
    }
}
