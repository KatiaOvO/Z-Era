using Migration.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 序章“返回主菜单”确认框：挂在 BackToMainMenu 根物体上
/// （根物体初始应为未启用，由 PrologueBackButton 点击启用）。
///
/// 启用时：
/// 1. ConfirmTextMaskBgImage 先复位为完全显示，再以溶解消失的
///    方式出场（时长默认 1 秒，可在检查器修改）；
/// 2. 保存当前光标状态并解锁——序章玩法中光标是锁定的，不解锁
///    确认框的按钮无法点击；关闭时恢复打开前的状态。
///
/// 按钮接线：
/// YesButton 点击 → SceneTransitionController.TransitionTo 切回
/// 主菜单（走全局黑屏溶解过渡）；
/// NoButton 点击 → 关闭本确认框。
/// 三个引用留空时按默认名字在子物体中自动查找。
/// </summary>
public class PrologueBackToMainMenu : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("确认框遮罩背景图（挂 UIDissolveImage 的 ConfirmTextMaskBgImage），留空则按名字在子物体中查找")]
    [SerializeField]
    private UIDissolveImage confirmMask;

    [Tooltip("确认按钮（Button 组件），留空则按名字找子物体 YesButton")]
    [SerializeField]
    private Button yesButton;

    [Tooltip("取消按钮（Button 组件），留空则按名字找子物体 NoButton")]
    [SerializeField]
    private Button noButton;

    [Header("参数")]

    [Tooltip("启用时遮罩溶解消失的时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float dissolveDuration = 1f;

    [Tooltip("点击 YesButton 后切换的目标场景名")]
    [SerializeField]
    private string menuSceneName = "MainMenu";

    // 打开前的光标状态，关闭时恢复
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool hasSavedCursorState;

    // 遮罩射线检测是否已按“溶解完成后取消”释放过
    private bool raycastReleased;

    private void OnEnable()
    {
        ResolveReferences();

        if (yesButton != null)
        {
            yesButton.onClick.AddListener(ConfirmYes);
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(ConfirmNo);
        }

        // 打开期间解锁光标（保存原状态，关闭时恢复）：
        // 玩法场景光标锁定时按钮收不到点击
        if (!hasSavedCursorState)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            hasSavedCursorState = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 出场：先把遮罩复位为完全显示、恢复勾选射线检测（挡住
        // 下层点击），再用设定时长溶解消失；复位这步在重复打开
        //（No 关闭后再打开）时保证每次表现一致
        if (confirmMask != null)
        {
            confirmMask.duration = dissolveDuration;
            confirmMask.graphic.raycastTarget = true;
            raycastReleased = false;
            confirmMask.SetVisible(true, true);
            confirmMask.Hide();
        }
    }

    private void OnDisable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(ConfirmYes);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(ConfirmNo);
        }

        // 恢复打开前的光标状态（点 Yes 跳转后本物体随场景卸载
        // 触发这里，恢复的锁定状态会由场景过渡流程收尾处理）
        if (hasSavedCursorState)
        {
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
            hasSavedCursorState = false;
        }
    }

    // 遮罩完全溶解消失后取消其射线检测：图片此时已不可见，
    // 继续勾选会挡住下层点击。UIDissolveImage 的完成回调
    //（m_OnHidden）没有公开接口，这里轮询 isHideComplete
    private void Update()
    {
        if (raycastReleased ||
            confirmMask == null ||
            !confirmMask.isHideComplete)
        {
            return;
        }

        confirmMask.graphic.raycastTarget = false;
        raycastReleased = true;
    }

    // Yes：经全局黑屏溶解过渡返回主菜单
    public void ConfirmYes()
    {
        SceneTransitionController.TransitionTo(menuSceneName);
    }

    // No：关闭确认框（OnDisable 负责移除监听与恢复光标）
    public void ConfirmNo()
    {
        gameObject.SetActive(false);
    }

    // 引用未在检查器赋值时按默认名字在子物体中查找；
    // 查不到的保持为 null，运行时会给出警告
    private void ResolveReferences()
    {
        if (confirmMask == null)
        {
            Transform mask =
                transform.Find("ConfirmTextMaskBgImage");

            if (mask != null)
            {
                confirmMask = mask.GetComponent<UIDissolveImage>();
            }
        }

        if (yesButton == null)
        {
            Transform yes = transform.Find("YesButton");

            if (yes != null)
            {
                yesButton = yes.GetComponent<Button>();
            }
        }

        if (noButton == null)
        {
            Transform no = transform.Find("NoButton");

            if (no != null)
            {
                noButton = no.GetComponent<Button>();
            }
        }

        if (confirmMask == null)
        {
            Debug.LogWarning(
                "PrologueBackToMainMenu：未找到 ConfirmTextMaskBgImage " +
                    "上的 UIDissolveImage，启用时没有溶解出场效果。",
                this
            );
        }

        if (yesButton == null || noButton == null)
        {
            Debug.LogWarning(
                "PrologueBackToMainMenu：YesButton / NoButton " +
                    "未找到或缺少 Button 组件，确认框无法操作。",
                this
            );
        }
    }
}
