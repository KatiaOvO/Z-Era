using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 滚动视图复位：挂在 ScrollRect 物体上，每次启用（所在面板被
/// 打开）时把滚动位置恢复到初始状态——水平最左、垂直最上，
/// 并清空惯性速度，避免上次滑动到的位置带进下一轮。
/// 常见用途：章节选择等滑动面板，关闭再打开应从头浏览。
/// 若希望默认停在别的位置，改 OnEnable 里的两个归一化值即可
/// （0 = 最左/最上，1 = 最右/最下）。
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class ScrollRectResetOnEnable : MonoBehaviour
{
    private ScrollRect scrollRect;

    private void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    private void OnEnable()
    {
        // 先强制布局系统完成一帧计算（Content 的总尺寸由
        // Layout Group 决定，首帧可能还没算完），再归位，
        // 否则归位值会被随后的布局修正覆盖
        Canvas.ForceUpdateCanvases();

        scrollRect.velocity = Vector2.zero;
        scrollRect.horizontalNormalizedPosition = 0f;
        scrollRect.verticalNormalizedPosition = 1f;
    }
}
