using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 悬停说明提示：挂在"？"等小图标物体上（物体需有接收射线
/// 检测的图形），鼠标移入显示说明面板，移出隐藏。
/// 说明文本写在检查器里，进入时写入面板的 TMP 文本——
/// 复制"？"物体到其他设置项时只需改这一个字符串。
/// 两个注意点：
/// 1. 提示面板下的所有图形必须关闭 Raycast Target，否则面板
///    弹出后挡在指针下方，反复触发移入/移出导致闪烁；
/// 2. OnEnable 强制隐藏一次：悬停中途页签/面板被关闭时
///    OnPointerExit 不会再触发，避免显隐状态残留到下次打开。
/// </summary>
public class HoverTooltip : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("引用")]

    [Tooltip("悬停时显示的说明面板（默认隐藏）")]
    [SerializeField]
    private GameObject tooltipPanel;

    [Header("内容")]

    [Tooltip("说明文本，进入时写入面板内的 TMP 文本；" +
        "留空则保留面板文本的现有内容")]
    [SerializeField, TextArea(2, 6)]
    private string tooltipText;

    private void OnEnable()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipPanel == null)
        {
            return;
        }

        TMP_Text label =
            tooltipPanel.GetComponentInChildren<TMP_Text>(true);

        if (label != null && !string.IsNullOrEmpty(tooltipText))
        {
            label.text = tooltipText;
        }

        tooltipPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
}
