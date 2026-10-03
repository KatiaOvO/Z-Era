using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 训练场帮助界面的关闭按钮：点击后关闭 HelpInfo Canvas，
/// 并调用 TrainingGroundHelpTrigger.RestoreCursor 恢复射击确认前
/// 的鼠标锁定状态与玩家控制。
/// 挂在帮助界面的关闭按钮上；按钮的 onClick 无需手动接线，
/// Awake 中会自动监听本物体的 Button。
/// </summary>
[RequireComponent(typeof(Button))]
public class TrainingGroundHelpCloseButton : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("要关闭的 HelpInfo Canvas，留空则自动从父级查找")]
    [SerializeField]
    private GameObject helpCanvas;

    [Tooltip("帮助界面触发器（HelpRoot 上），留空则自动查找")]
    [SerializeField]
    private TrainingGroundHelpTrigger helpTrigger;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(CloseHelp);
    }

    private void Start()
    {
        // 留空时的自动查找：按钮可能处于未激活的画布下，
        // 父级画布在激活后才能被 GetComponentInParent 读到
        if (helpCanvas == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas != null)
            {
                helpCanvas = parentCanvas.gameObject;
            }
        }

        if (helpTrigger == null)
        {
            helpTrigger = FindObjectOfType<TrainingGroundHelpTrigger>();
        }
    }

    // 关闭帮助界面并恢复鼠标锁定与玩家控制；
    // 也可在检查器中手动接到其它事件上
    public void CloseHelp()
    {
        // 画布关闭时按钮收不到 PointerExit，悬停填充会停留在
        // 铺满状态带进下一轮：关闭前先复位悬停表现
        GetComponent<MainMenuButtonHoverFill>()?.ResetVisualState();

        if (helpCanvas != null)
        {
            helpCanvas.SetActive(false);
        }

        if (helpTrigger != null)
        {
            helpTrigger.RestoreCursor();
        }
    }
}
