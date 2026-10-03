using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 训练场退出确认界面的确认退出按钮：点击后关闭 ConfirmQuit
/// Canvas、恢复鼠标锁定与玩家控制，并通过全局场景过渡服务
/// （DontDestroyOnLoad 的黑屏 + 进度条）返回主菜单场景。
/// 挂在确认界面的确认退出按钮上；按钮的 onClick 无需手动接线，
/// Awake 中会自动监听本物体的 Button。
/// </summary>
[RequireComponent(typeof(Button))]
public class TrainingGroundQuitConfirmButton : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("要关闭的 ConfirmQuit Canvas，留空则自动从父级查找")]
    [SerializeField]
    private GameObject confirmCanvas;

    [Tooltip("退出确认管理器（QuitRoot 上），留空则自动查找")]
    [SerializeField]
    private TrainingGroundQuitConfirm quitConfirm;

    [Header("跳转")]

    [Tooltip("确认退出后返回的场景名，场景需加入 Build Settings")]
    [SerializeField]
    private string menuSceneName = "MainMenu";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(ConfirmQuit);
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

    // 确认退出：关闭确认界面、恢复鼠标与玩家控制，
    // 经全局黑屏过渡返回主菜单；
    // 也可在检查器中手动接到其它事件上
    public void ConfirmQuit()
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

        // RestoreCursor 会恢复玩家控制（关闭确认界面的流程需要），
        // 但退出的目标是主菜单：过渡的黑屏期间不允许任何操作，
        // 重新锁定玩家控制，直到场景切换完成（旧场景销毁，锁定自然失效）
        if (!string.IsNullOrEmpty(menuSceneName))
        {
            TrainingGroundControlLock.Lock();
        }

        // 退出目标是主菜单，鼠标终态应为解锁可见：
        // RestoreCursor 恢复的是训练场的锁定状态，这里显式改回；
        // 主菜单场景没有 PlayerController，不会自己解锁
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneTransitionController.TransitionTo(menuSceneName);
    }
}
