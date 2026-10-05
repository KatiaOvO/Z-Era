using UnityEngine;

/// <summary>
/// 准星外观配置：自定义准星面板可调的全部参数。
/// 由 SettingsManager 持有并持久化，CrosshairHUD 与设置面板的
/// 预览准星共同以此为最终外观来源。
/// 四条线始终全部显示，不做独立开关；各滑条的默认值取允许范围
/// 的中点，设置面板滑条的 min/max 应与范围常量一致。
/// </summary>
public class CrosshairConfig
{
    // 各参数的允许范围。面板滑条统一归一化为 0~1，
    // 由此处的范围线性映射到实际数值；输入字段直接显示实际数值
    public const float MinLineWidth = 1f;
    public const float MaxLineWidth = 8f;
    public const float MinLineLength = 5f;
    public const float MaxLineLength = 60f;
    public const float MinGap = 0f;
    public const float MaxGap = 40f;
    public const float MinOpacity = 0f;
    public const float MaxOpacity = 1f;

    // 颜色暂未开放到设置面板，保留字段供以后接入
    public Color color = Color.white;

    public float lineWidth = 2f;
    public float lineLength = 20f;
    public float gap = 5f;
    public float opacity = 0.5f;

    public bool dotEnabled = false;

    // 动态准星：开启时射击会让四条线向外扩散（原 CrosshairHUD
    // 逻辑），关闭时四条线始终停在静态间隙位置
    public bool dynamicCrosshairEnabled = true;

    // 把任意来源的数值钳到允许范围（输入字段、滑条越界兜底）
    public void Clamp()
    {
        lineWidth = Mathf.Clamp(
            lineWidth, MinLineWidth, MaxLineWidth);
        lineLength = Mathf.Clamp(
            lineLength, MinLineLength, MaxLineLength);
        gap = Mathf.Clamp(gap, MinGap, MaxGap);
        opacity = Mathf.Clamp(opacity, MinOpacity, MaxOpacity);
    }

    // 当前配置的副本：面板单项修改不直接动全局对象，
    // 统一走"改副本 → SetCrosshairConfig 钳值生效持久化"
    public CrosshairConfig Clone()
    {
        return new CrosshairConfig
        {
            color = color,
            lineWidth = lineWidth,
            lineLength = lineLength,
            gap = gap,
            opacity = opacity,
            dotEnabled = dotEnabled,
            dynamicCrosshairEnabled = dynamicCrosshairEnabled
        };
    }
}
