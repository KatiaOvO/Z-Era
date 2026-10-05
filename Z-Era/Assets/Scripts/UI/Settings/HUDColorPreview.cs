using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 颜色预览：挂在任意需要跟随 HUD 颜色的图形物体上
/// （Image / TMP_Text 均可，例如 HUDColorTestText、
/// HUDColorTestImage）。
/// HUD 颜色变化（配置版本号自增）时自动把自身的 RGB 同步为
/// 全局 HUD 颜色；元素自身的 alpha 保持不变。
/// 与取色器解耦：无论谁修改了 HUD 颜色都会跟随。
/// </summary>
public class HUDColorPreview : MonoBehaviour
{
    private Graphic graphic;
    private int appliedVersion = -1;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();

        if (graphic == null)
        {
            Debug.LogWarning(
                "HUDColorPreview：所在物体缺少 Graphic 组件" +
                "（需要 Image 或 TMP_Text）。",
                this
            );
        }
    }

    private void Update()
    {
        if (graphic == null ||
            appliedVersion == SettingsManager.HUDColorVersion)
        {
            return;
        }

        appliedVersion = SettingsManager.HUDColorVersion;

        Color color = graphic.color;
        color.r = SettingsManager.HUDColor.r;
        color.g = SettingsManager.HUDColor.g;
        color.b = SettingsManager.HUDColor.b;
        graphic.color = color;
    }
}
