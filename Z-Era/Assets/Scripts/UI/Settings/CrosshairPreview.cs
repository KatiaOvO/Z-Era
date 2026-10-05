using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板里的预览准星：挂在预览物体上（由
/// CrosshairSettingsPanel 自动添加），其下子物体按名字
/// TopLine / BottomLine / LeftLine / RightLine / Dot 摆放
/// 四条线与中心点的 Image。
/// Refresh 按当前准星配置摆放外观与位置，数学与 CrosshairHUD
/// 一致（间隙 + 半线长），但不包含扩散动画、换弹/空弹匣逻辑。
/// 四条线始终显示，只有中心点受开关控制。
/// </summary>
public class CrosshairPreview : MonoBehaviour
{
    private Image topLine;
    private Image bottomLine;
    private Image leftLine;
    private Image rightLine;
    private Image dot;

    // 已应用的外观版本号：无论谁（面板、取色器、未来的任何入口）
    // 修改了准星配置，版本号都会自增，Update 据此自动重刷预览，
    // 不需要各修改方互相通知
    private int appliedVersion = -1;

    private void Awake()
    {
        topLine = FindImage("TopLine");
        bottomLine = FindImage("BottomLine");
        leftLine = FindImage("LeftLine");
        rightLine = FindImage("RightLine");
        dot = FindImage("Dot");
    }

    private void Update()
    {
        if (appliedVersion != SettingsManager.CrosshairVersion)
        {
            Refresh(SettingsManager.Crosshair);
        }
    }

    private Image FindImage(string name)
    {
        Transform child = transform.Find(name);

        if (child == null)
        {
            Debug.LogWarning(
                $"CrosshairPreview：子物体中找不到 {name}。",
                this
            );
            return null;
        }

        return child.GetComponent<Image>();
    }

    // 按配置刷新预览外观与位置
    public void Refresh(CrosshairConfig config)
    {
        appliedVersion = SettingsManager.CrosshairVersion;

        float halfLength = config.lineLength * 0.5f;
        Color color = config.color;
        color.a = config.opacity;

        ApplyLine(topLine, color,
            new Vector2(config.lineWidth, config.lineLength),
            Vector2.up * (config.gap + halfLength));

        ApplyLine(bottomLine, color,
            new Vector2(config.lineWidth, config.lineLength),
            Vector2.down * (config.gap + halfLength));

        ApplyLine(leftLine, color,
            new Vector2(config.lineLength, config.lineWidth),
            Vector2.left * (config.gap + halfLength));

        ApplyLine(rightLine, color,
            new Vector2(config.lineLength, config.lineWidth),
            Vector2.right * (config.gap + halfLength));

        if (dot != null)
        {
            dot.enabled = config.dotEnabled;

            if (config.dotEnabled)
            {
                dot.color = color;
                // 中心点大小与线条粗细一致
                dot.rectTransform.sizeDelta =
                    Vector2.one * config.lineWidth;
                dot.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    private void ApplyLine(
        Image line,
        Color color,
        Vector2 size,
        Vector2 position)
    {
        if (line == null)
        {
            return;
        }

        line.color = color;
        line.rectTransform.sizeDelta = size;
        line.rectTransform.anchoredPosition = position;
    }
}
