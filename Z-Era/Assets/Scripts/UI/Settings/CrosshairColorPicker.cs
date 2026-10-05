using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 取色器（Unity 编辑器风格）：
/// 色相环 + 环内饱和度/明度方块 + 下方 RGB 三滑条与输入字段。
/// 同一脚本通过 Picker Target 服务两个页签：
/// Crosshair 写准星外观配置，HUD 写 HUD 整体颜色；
/// 写入后由配置版本号驱动预览与游戏内元素自动刷新。
/// 子物体按名字自动收集（缺失的跳过并警告）：
/// - HueRing：RawImage，色相环（含透明中心孔的纹理运行时生成），
///   点击/拖拽环上取色相；
/// - RingCursor：HueRing 下的 Image，色相指示光标；
/// - SVBox：RawImage，饱和度/明度渐变方块（纹理随色相重建），
///   点击/拖拽取饱和度与明度；
/// - SVCursor：SVBox 下的 Image，方块取色光标；
/// - RedSlider/RedInput、GreenSlider/GreenInput、
///   BlueSlider/BlueInput：RGB 三通道，滑条 0~1，
///   输入字段显示 0~255 整数，双向按比例同步。
/// 取色结果写入 Picker Target 对应的全局配置并持久化，
/// 游戏内元素与面板预览通过配置版本号自动刷新。
/// 颜色只取 RGB（alpha 恒为 1）：
/// 准星透明度由独立滑条控制，HUD 元素保留各自的 alpha。
/// 指针事件挂在本容器上：点击/拖拽 HueRing 或 SVBox 时事件沿
/// 层级冒泡到容器，按下时按射线命中目标区分拖的是环还是方块。
/// </summary>
public class CrosshairColorPicker : MonoBehaviour,
    IPointerDownHandler, IDragHandler
{
    public enum PickerTarget
    {
        Crosshair,
        HUD
    }

    [Header("目标")]

    [Tooltip("本取色器写入的配置：准星外观或 HUD 整体颜色")]
    [SerializeField]
    private PickerTarget pickerTarget = PickerTarget.Crosshair;

    private const string HueRingName = "HueRing";
    private const string RingCursorName = "RingCursor";
    private const string SVBoxName = "SVBox";
    private const string SVCursorName = "SVCursor";

    // 色相环纹理边长；环外缘贴住 RawImage 边缘，
    // 内孔半径占外缘的比例（SV 方块要放进内孔里）
    private const int RingTextureSize = 256;
    private const float RingInnerRatio = 0.68f;
    private const int SVTextureSize = 128;

    // RGB 输入字段显示 0~255
    private const float RGBInputScale = 255f;

    private RawImage hueRing;
    private RectTransform ringCursor;
    private RawImage svBox;
    private RectTransform svCursor;
    private Slider redSlider;
    private TMP_InputField redInput;
    private Slider greenSlider;
    private TMP_InputField greenInput;
    private Slider blueSlider;
    private TMP_InputField blueInput;

    private Texture2D hueRingTexture;
    private Texture2D svTexture;

    // 当前 HSV 状态：环给色相，方块给饱和度/明度
    private float hue;
    private float saturation = 1f;
    private float brightness = 1f;

    private bool syncing;

    // 本次拖拽的目标：环（色相）还是方块（饱和度/明度）
    private enum PickMode
    {
        None,
        Hue,
        SV
    }

    private PickMode pickMode = PickMode.None;

    private void Awake()
    {
        hueRing = FindDescendant<RawImage>(HueRingName);
        svBox = FindDescendant<RawImage>(SVBoxName);

        if (hueRing != null)
        {
            ringCursor = FindDescendant<RectTransform>(RingCursorName);
            CenterAnchors(ringCursor);
        }

        if (svBox != null)
        {
            svCursor = FindDescendant<RectTransform>(SVCursorName);
            CenterAnchors(svCursor);

            if (!svBox.raycastTarget)
            {
                Debug.LogWarning(
                    "CrosshairColorPicker：SVBox 的 Raycast Target " +
                    "未勾选，方块取色不会生效。",
                    this
                );
            }
        }

        redSlider = FindDescendant<Slider>("RedSlider");
        redInput = FindDescendant<TMP_InputField>("RedInput");
        greenSlider = FindDescendant<Slider>("GreenSlider");
        greenInput = FindDescendant<TMP_InputField>("GreenInput");
        blueSlider = FindDescendant<Slider>("BlueSlider");
        blueInput = FindDescendant<TMP_InputField>("BlueInput");

        GenerateRingTexture();
        GenerateSVTexture();

        BindRGBControls();
    }

    private void OnEnable()
    {
        // 面板每次打开都从当前保存的颜色反推 HSV 并同步 RGB 控件，
        // 保证取色器状态与上次选的颜色一致
        Color.RGBToHSV(
            CurrentColor,
            out hue,
            out saturation,
            out brightness);

        RegenerateSVTexture();
        UpdateRingCursor();
        UpdateSVCursor();
        SyncRGBControls();
    }

    private void OnDestroy()
    {
        if (hueRingTexture != null)
        {
            Destroy(hueRingTexture);
        }

        if (svTexture != null)
        {
            Destroy(svTexture);
        }
    }

    // 在整棵子树中按名字查找（含未激活物体）：
    // 光标是环/方块的孙层级，直接 Find 只查直接子物体，
    // 会永远找不到——这是光标不动的根源
    private T FindDescendant<T>(string name) where T : Component
    {
        Transform[] children =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name != name)
            {
                continue;
            }

            T component = child.GetComponent<T>();

            if (component != null)
            {
                return component;
            }

            Debug.LogWarning(
                $"CrosshairColorPicker：物体 {name} 上缺少 " +
                $"{typeof(T).Name} 组件。",
                this
            );
            return null;
        }

        Debug.LogWarning(
            $"CrosshairColorPicker：找不到子物体 {name}，" +
            "对应部分不可用。",
            this
        );
        return null;
    }

    // 光标锚点强制居中：之后 anchoredPosition 就是相对圆心/方心的偏移
    private static void CenterAnchors(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.one * 0.5f;
        rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
    }

    // ===== 纹理生成 =====

    // 色相环：外缘贴住 RawImage 边缘，内孔按 RingInnerRatio 挖空，
    // 孔外像素全透明（色相 = 相对圆心的角度）
    private void GenerateRingTexture()
    {
        if (hueRing == null)
        {
            return;
        }

        hueRingTexture = new Texture2D(
            RingTextureSize,
            RingTextureSize,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[
            RingTextureSize * RingTextureSize];

        float half = RingTextureSize * 0.5f;

        for (int y = 0; y < RingTextureSize; y++)
        {
            for (int x = 0; x < RingTextureSize; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;

                float radius = Mathf.Sqrt(
                    dx * dx + dy * dy) / half;

                if (radius > 1f || radius < RingInnerRatio)
                {
                    pixels[y * RingTextureSize + x] = Color.clear;
                    continue;
                }

                // 角度换算成 0~1 色相（0 在正右方，逆时针为正）
                float angle = Mathf.Atan2(dy, dx) / (2f * Mathf.PI);

                if (angle < 0f)
                {
                    angle += 1f;
                }

                Color color = Color.HSVToRGB(angle, 1f, 1f);
                color.a = 1f;
                pixels[y * RingTextureSize + x] = color;
            }
        }

        hueRingTexture.SetPixels(pixels);
        hueRingTexture.Apply();
        hueRing.texture = hueRingTexture;
    }

    // SV 渐变方块：横向饱和度 0→1，纵向明度 1（顶）→0（底），
    // 色相变化时重建
    private void GenerateSVTexture()
    {
        if (svBox == null)
        {
            return;
        }

        if (svTexture == null)
        {
            svTexture = new Texture2D(
                SVTextureSize,
                SVTextureSize,
                TextureFormat.RGB24,
                false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            svBox.texture = svTexture;
        }

        Color[] pixels = new Color[SVTextureSize * SVTextureSize];

        for (int y = 0; y < SVTextureSize; y++)
        {
            float v = 1f - y / (SVTextureSize - 1f);

            for (int x = 0; x < SVTextureSize; x++)
            {
                float s = x / (SVTextureSize - 1f);
                pixels[y * SVTextureSize + x] =
                    Color.HSVToRGB(hue, s, v);
            }
        }

        svTexture.SetPixels(pixels);
        svTexture.Apply();
    }

    private void RegenerateSVTexture()
    {
        GenerateSVTexture();
    }

    // ===== 指针交互（事件从环/方块冒泡到本容器） =====

    public void OnPointerDown(PointerEventData eventData)
    {
        pickMode = DetectPickMode(eventData);
        HandlePick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        HandlePick(eventData);
    }

    // 按按下时射线命中的目标区分拖的是环还是方块
    private PickMode DetectPickMode(PointerEventData eventData)
    {
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;

        if (hit == null)
        {
            return PickMode.None;
        }

        if (hueRing != null &&
            (hit.gameObject == hueRing.gameObject ||
             hit.transform.IsChildOf(hueRing.transform)))
        {
            return PickMode.Hue;
        }

        if (svBox != null &&
            (hit.gameObject == svBox.gameObject ||
             hit.transform.IsChildOf(svBox.transform)))
        {
            return PickMode.SV;
        }

        return PickMode.None;
    }

    private void HandlePick(PointerEventData eventData)
    {
        switch (pickMode)
        {
            case PickMode.Hue:
                PickHue(eventData);
                break;

            case PickMode.SV:
                PickSV(eventData);
                break;
        }
    }

    // 环上取色相：指针相对环中心的角度
    private void PickHue(PointerEventData eventData)
    {
        if (hueRing == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            hueRing.rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            return;
        }

        Vector2 offset = localPoint - hueRing.rectTransform.rect.center;

        float angle = Mathf.Atan2(offset.y, offset.x) /
            (2f * Mathf.PI);

        if (angle < 0f)
        {
            angle += 1f;
        }

        hue = angle;

        RegenerateSVTexture();
        UpdateRingCursor();
        ApplyColor();
    }

    // 方块上取饱和度/明度
    private void PickSV(PointerEventData eventData)
    {
        if (svBox == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            svBox.rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            return;
        }

        Rect rect = svBox.rectTransform.rect;

        saturation = Mathf.Clamp01(
            (localPoint.x - rect.x) / rect.width);
        brightness = Mathf.Clamp01(
            1f - (localPoint.y - rect.y) / rect.height);

        UpdateSVCursor();
        ApplyColor();
    }

    // ===== 光标与预览 =====

    private void UpdateRingCursor()
    {
        if (ringCursor == null || hueRing == null)
        {
            return;
        }

        float angleRad = hue * 2f * Mathf.PI;
        Rect rect = hueRing.rectTransform.rect;

        // 光标钉在环带的正中间
        float midRadius =
            (RingInnerRatio + 1f) * 0.5f *
            Mathf.Min(rect.width, rect.height) * 0.5f;

        ringCursor.anchoredPosition = new Vector2(
            Mathf.Cos(angleRad) * midRadius,
            Mathf.Sin(angleRad) * midRadius);
    }

    private void UpdateSVCursor()
    {
        if (svCursor == null || svBox == null)
        {
            return;
        }

        Rect rect = svBox.rectTransform.rect;

        svCursor.anchoredPosition = new Vector2(
            (saturation - 0.5f) * rect.width,
            (0.5f - brightness) * rect.height);
    }

    // ===== 应用与 RGB 控件 =====

    // HSV 状态变化后：写配置并同步 RGB 控件
    // 当前管理的颜色（读侧，按取色器目标路由）
    private Color CurrentColor =>
        pickerTarget == PickerTarget.Crosshair
            ? SettingsManager.Crosshair.color
            : SettingsManager.HUDColor;

    // 把取到的颜色写到对应目标（只取 RGB，alpha 固定 1：
    // 准星透明度由独立滑条控制，HUD 元素保留各自的 alpha）
    private void ApplyNewColor(Color rgb)
    {
        rgb.a = 1f;

        if (pickerTarget == PickerTarget.Crosshair)
        {
            CrosshairConfig config = SettingsManager.Crosshair.Clone();
            config.color = rgb;
            SettingsManager.Instance.SetCrosshairConfig(config);
        }
        else
        {
            SettingsManager.Instance.SetHUDColor(rgb);
        }
    }

    private void ApplyColor()
    {
        Color rgb = Color.HSVToRGB(hue, saturation, brightness);

        ApplyNewColor(rgb);

        SyncRGBControls();
    }

    // RGB 控件 ← 当前配置（滑条 0~1 = 通道值，
    // 输入字段 0~255 整数）
    private void SyncRGBControls()
    {
        // 读侧必须按目标路由：HUD 取色器若读成准星配置，
        // 会把滑条/字段同步回准星的颜色而不是刚选的颜色
        Color color = CurrentColor;

        syncing = true;

        if (redSlider != null)
        {
            redSlider.SetValueWithoutNotify(color.r);
        }

        if (greenSlider != null)
        {
            greenSlider.SetValueWithoutNotify(color.g);
        }

        if (blueSlider != null)
        {
            blueSlider.SetValueWithoutNotify(color.b);
        }

        if (redInput != null)
        {
            redInput.SetTextWithoutNotify(
                Mathf.RoundToInt(color.r * RGBInputScale)
                    .ToString());
        }

        if (greenInput != null)
        {
            greenInput.SetTextWithoutNotify(
                Mathf.RoundToInt(color.g * RGBInputScale)
                    .ToString());
        }

        if (blueInput != null)
        {
            blueInput.SetTextWithoutNotify(
                Mathf.RoundToInt(color.b * RGBInputScale)
                    .ToString());
        }

        syncing = false;
    }

    private void BindRGBControls()
    {
        BindChannel(redSlider, redInput, ColorChannel.Red);
        BindChannel(greenSlider, greenInput, ColorChannel.Green);
        BindChannel(blueSlider, blueInput, ColorChannel.Blue);
    }

    private enum ColorChannel
    {
        Red,
        Green,
        Blue
    }

    // 绑定一个通道的滑条 + 输入字段：
    // - 滑条 0~1 = 通道值，字段显示 0~255 整数，双向按比例同步；
    // - 输入过程中能解析就实时应用，解析失败（删空等中间状态）
    //   不打断打字；
    // - 编辑结束（回车/失焦）：越界钳到 0~255，解析失败恢复
    //   当前值，把控件同步为实际生效值
    private void BindChannel(
        Slider slider,
        TMP_InputField input,
        ColorChannel channel)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.AddListener(value =>
        {
            if (syncing)
            {
                return;
            }

            syncing = true;

            if (input != null)
            {
                input.SetTextWithoutNotify(
                    Mathf.RoundToInt(value * RGBInputScale)
                        .ToString());
            }

            syncing = false;

            ModifyColor(channel, value);
        });

        if (input == null)
        {
            return;
        }

        input.onValueChanged.AddListener(text =>
        {
            if (syncing || !TryParseByte(text, out int parsed))
            {
                return;
            }

            float value = Mathf.Clamp01(parsed / RGBInputScale);

            syncing = true;
            slider.SetValueWithoutNotify(value);
            syncing = false;

            ModifyColor(channel, value);
        });

        input.onEndEdit.AddListener(text =>
        {
            float applied;

            if (TryParseByte(text, out int parsed))
            {
                applied = Mathf.Clamp01(parsed / RGBInputScale);
            }
            else
            {
                // 解析失败恢复当前值
                applied = GetCurrentChannel(channel);
            }

            ModifyColor(channel, applied);

            // 读回实际生效值并归一化显示
            float actual = GetCurrentChannel(channel);

            syncing = true;
            slider.SetValueWithoutNotify(actual);
            input.SetTextWithoutNotify(
                Mathf.RoundToInt(actual * RGBInputScale)
                    .ToString());
            syncing = false;
        });
    }

    private float GetCurrentChannel(ColorChannel channel)
    {
        Color color = SettingsManager.Crosshair.color;

        switch (channel)
        {
            case ColorChannel.Red:
                return color.r;
            case ColorChannel.Green:
                return color.g;
            default:
                return color.b;
        }
    }

    // RGB 通道变化后：写入对应目标，并从结果颜色反推 HSV 状态
    // （色相变化会连带重建 SV 方块纹理与光标位置）
    private void ModifyColor(ColorChannel channel, float value)
    {
        Color color = CurrentColor;
        color.a = 1f;

        switch (channel)
        {
            case ColorChannel.Red:
                color.r = value;
                break;
            case ColorChannel.Green:
                color.g = value;
                break;
            default:
                color.b = value;
                break;
        }

        ApplyNewColor(color);

        Color.RGBToHSV(
            color,
            out hue,
            out saturation,
            out brightness);

        RegenerateSVTexture();
        UpdateRingCursor();
        UpdateSVCursor();
    }

    private static bool TryParseByte(string text, out int value)
    {
        return int.TryParse(
            text?.Trim(),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }
}
