using UnityEngine;

/// <summary>
/// 一段玩家自白的数据资产，在 Project 窗口右键 Create > Dialogue >
/// Monologue Asset 创建。由 MonologueTrigger 监听对话标记
/// （DialogueFlags）触发，条件通过时交给 HUD 上的
/// MonologueUIController 以打字机形式显示。
/// </summary>
[CreateAssetMenu(
    menuName = "Dialogue/Monologue Asset",
    fileName = "Monologue_"
)]
public class MonologueAsset : ScriptableObject
{
    [Header("自白内容")]

    [TextArea(3, 10)]
    [Tooltip("自白正文，支持 TMP 富文本")]
    [SerializeField]
    private string text;

    [Tooltip("打字速度（每秒字符数）")]
    [Min(1f)]
    [SerializeField]
    private float charactersPerSecond = 20f;

    [Tooltip("正文打印完毕后的停留时长（秒），随后开始淡出")]
    [Min(0f)]
    [SerializeField]
    private float holdDuration = 2f;

    [Tooltip("淡出时长（秒），0 表示停留结束后立即消失")]
    [Min(0f)]
    [SerializeField]
    private float fadeDuration = 1f;

    [Header("屏幕位置")]

    [Tooltip("锚点最小值，与最大值相等即点锚，不等即拉伸")]
    [SerializeField]
    private Vector2 anchorMin = new Vector2(0.5f, 0.5f);

    [Tooltip("锚点最大值")]
    [SerializeField]
    private Vector2 anchorMax = new Vector2(0.5f, 0.5f);

    [Tooltip("轴心")]
    [SerializeField]
    private Vector2 pivot = new Vector2(0.5f, 0.5f);

    [Tooltip("相对锚点的位置偏移")]
    [SerializeField]
    private Vector2 anchoredPosition = new Vector2(0f, -180f);

    [Tooltip("尺寸（点锚下为宽高；拉伸锚下为边距）")]
    [SerializeField]
    private Vector2 sizeDelta = new Vector2(900f, 200f);

    [Header("触发条件（全部满足才触发）")]

    [Tooltip("必须已设置的全部对话标记（DialogueFlags），留空则不限制")]
    [SerializeField]
    private string[] requiredFlags;

    [Tooltip("只要设置了其中任一标记就不触发")]
    [SerializeField]
    private string[] forbiddenFlags;

    [Header("Flag 桥接（与对话/任务系统联动）")]

    [Tooltip("自白开始显示时自动设置的对话标记，例如用于连锁下一段自白或对话")]
    [SerializeField]
    private string[] flagsToSetOnShown;

    public string Text => text;
    public float CharactersPerSecond => charactersPerSecond;
    public float HoldDuration => holdDuration;
    public float FadeDuration => fadeDuration;
    public Vector2 AnchorMin => anchorMin;
    public Vector2 AnchorMax => anchorMax;
    public Vector2 Pivot => pivot;
    public Vector2 AnchoredPosition => anchoredPosition;
    public Vector2 SizeDelta => sizeDelta;
    public string[] RequiredFlags => requiredFlags;
    public string[] ForbiddenFlags => forbiddenFlags;
    public string[] FlagsToSetOnShown => flagsToSetOnShown;
}
