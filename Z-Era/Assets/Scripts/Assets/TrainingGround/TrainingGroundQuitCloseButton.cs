using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 训练场退出确认界面的关闭按钮：点击后关闭 ConfirmQuit Canvas，
/// 并调用 TrainingGroundQuitConfirm.RestoreCursor 恢复射击确认前
/// 的鼠标锁定状态与玩家控制。
/// 挂在确认界面的关闭按钮上；按钮的 onClick 无需手动接线，
/// Awake 中会自动监听本物体的 Button。
/// </summary>
[RequireComponent(typeof(Button))]
public class TrainingGroundQuitCloseButton : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("要关闭的 ConfirmQuit Canvas，留空则自动从父级查找")]
    [SerializeField]
    private GameObject confirmCanvas;

    [Tooltip("退出确认管理器（QuitRoot 上），留空则自动查找")]
    [SerializeField]
    private TrainingGroundQuitConfirm quitConfirm;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(CloseConfirm);
    }

    private void Start()
    {
        // 留空时的自动查找：按钮可能处于未激活的画布下，
        // 父级画布在激活后才能被 GetComponentInParent 读到
        if (confirmCanvas == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas != null)
            {
                confirmCanvas = parentCanvas.gameObject;
            }
        }

        if (quitConfirm == null)
        {
            quitConfirm = FindObjectOfType<TrainingGroundQuitConfirm>();
        }
    }

    // 关闭确认界面并恢复鼠标锁定与玩家控制；
    // 也可在检查器中手动接到其它事件上
    public void CloseConfirm()
    {
        // 画布关闭时按钮收不到 PointerExit，悬停填充会停留在
        // 铺满状态带进下一轮：关闭前先复位悬停表现
        GetComponent<MainMenuButtonHoverFill>()?.ResetVisualState();

        if (confirmCanvas != null)
        {
            confirmCanvas.SetActive(false);
        }

        if (quitConfirm != null)
        {
            quitConfirm.RestoreCursor();
        }
    }
}
