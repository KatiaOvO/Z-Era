using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 整体染色：挂在每个游戏场景的 HUD Canvas 上。
/// 子树中按名字收集需要染色的图标与文本（含未激活的），
/// 统一应用 SettingsManager.HUDColor 的 RGB；
/// 各元素自身的 alpha（淡入淡出、闪烁）保持不变。
/// 配置版本号变化（设置面板改色）时自动重新应用。
/// 体力相关的三个元素（StaminaIcon/StaminaText/
/// StaminaOutgoingText）不在此列：StaminaHUD 有自己的颜色
/// 状态机（低体力闪烁到红色），已改为直接读取全局颜色。
/// </summary>
public class HUDColorApplier : MonoBehaviour
{
    private static readonly string[] ElementNames =
    {
        "HealthIcon",
        "HealthText",
        "HealthOutgoingText",
        "WeaponIcon",
        "MagazineText",
        "MagazineOutgoingText",
        "SlashText",
        "CarriedText",
        "CarriedOutgoingText"
    };

    private readonly Dictionary<string, Graphic> elements =
        new Dictionary<string, Graphic>();

    private int appliedVersion = -1;

    private void Awake()
    {
        Graphic[] graphics =
            GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            foreach (string elementName in ElementNames)
            {
                if (graphic.name == elementName &&
                    !elements.ContainsKey(elementName))
                {
                    elements[elementName] = graphic;
                }
            }
        }

        foreach (string elementName in ElementNames)
        {
            if (!elements.ContainsKey(elementName))
            {
                Debug.LogWarning(
                    $"HUDColorApplier：HUD Canvas 下找不到 " +
                    $"{elementName}，该元素不会被染色。",
                    this
                );
            }
        }
    }

    private void Update()
    {
        if (appliedVersion != SettingsManager.HUDColorVersion)
        {
            ApplyColor();
        }
    }

    private void ApplyColor()
    {
        appliedVersion = SettingsManager.HUDColorVersion;

        Color hudColor = SettingsManager.HUDColor;

        foreach (Graphic graphic in elements.Values)
        {
            Color color = graphic.color;
            color.r = hudColor.r;
            color.g = hudColor.g;
            color.b = hudColor.b;
            graphic.color = color;
        }
    }
}
