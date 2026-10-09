using System.Collections.Generic;
using Migration.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 提示画布管理单例：按 U 开关当前场景的 Tip Canvas（与 Task Canvas
/// 同级的独立 Overlay 画布）。关闭方式：再按 U / 面板上的关闭按钮
///（CloseButton，Button 的 onClick 由本类按名字自动监听，无需接线）。
///
/// 打开表现与任务面板一致：遮罩 TipPanelBgMaskImage（挂
/// UIDissolveImage）先瞬时拉回完全显示并强制勾选 Raycast Target，
/// 再溶解消失露出提示内容；溶解期间遮罩拦截射线，完全溶解后自动
/// 取消勾选放行点击，关闭再打开时重新勾选，依次往复。
/// 同时暂停世界（时间缩放 + 全局声音）、锁定玩家控制并释放鼠标，
/// 关闭时全部按原样恢复，与设置/任务/背包面板一致。
/// 互斥：本面板打开期间其他全局面板（设置/任务/背包）的键盘入口
/// 均被 IsOpen 挡住；本面板打开前同样检查它们，互不叠加。
///
/// 本类自举为常驻单例（同 TaskPanelController）：Tip Canvas 默认
/// 禁用，挂在画布上的脚本收不到按键，因此由本类常驻监听 U，按名字
/// 懒查找画布、遮罩与关闭按钮——提示画布只放进需要的场景即可；
/// 场景加载后若画布被误留在启用态，由 OnSceneLoaded 兜底收起。
/// 执行序位 -55：在 TaskPanelController（-60）之后、
/// SettingsUIRoot（-50）之前，同帧多键按下时任务面板优先。
/// </summary>
[DefaultExecutionOrder(-55)]
public class TipPanelController : MonoBehaviour
{
    public static TipPanelController Instance { get; private set; }

    // 面板是否打开：其他全局面板的键盘入口在打开前检查此标志
    public static bool IsOpen { get; private set; }

    // 画布上各物体在场景中的名字
    private const string CanvasName = "Tip Canvas";
    private const string MaskImageName = "TipPanelBgMaskImage";
    private const string CloseButtonName = "CloseButton";

    // 开关面板的按键
    [SerializeField]
    private KeyCode toggleKey = KeyCode.U;

    private GameObject panelCanvas;
    private UIDissolveImage maskDissolve;

    // 打开前的鼠标状态，关闭时恢复
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool hasSavedCursorState;

    // 世界暂停状态：仅存在 Player 的场景打开时暂停
    private float previousTimeScale = 1f;
    private bool previousAudioListenerPause;
    private bool worldPaused;

    // 打开期间被禁用的玩家控制组件，关闭时按原样恢复
    // （组件清单与 TaskPanelController / SettingsUIRoot 一致）
    private readonly List<Behaviour> disabledControls =
        new List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // 关闭域重载时静态状态会带进下一次运行，这里统一复位
        IsOpen = false;

        if (Instance == null)
        {
            new GameObject("TipPanelController")
                .AddComponent<TipPanelController>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 画布随旧场景销毁时状态可能未收尾，这里兜底恢复
        if (IsOpen && panelCanvas == null)
        {
            EndSession();
        }

        // 新场景若有提示画布：懒查找并确保它默认处于禁用状态
        //（场景里误把画布留在启用态时由这里兜底收起，不闪画面）
        if (!IsOpen && EnsurePanel() && panelCanvas.activeSelf)
        {
            panelCanvas.SetActive(false);
        }
    }

    private void Update()
    {
        // 遮罩的 Raycast Target 同步（与 TaskPanelController 相同）：
        // 溶解中/完全显示时挡住射线，溶解完毕后放行；画布关闭期间
        // 遮罩保持勾选，重开后依旧是初始勾选状态，依次往复
        if (maskDissolve != null && maskDissolve.graphic != null)
        {
            bool shouldBlockRaycast = !maskDissolve.isHideComplete;

            if (maskDissolve.graphic.raycastTarget != shouldBlockRaycast)
            {
                maskDissolve.graphic.raycastTarget = shouldBlockRaycast;
            }
        }

        // U 开关面板。设置/背包/任务/对话/训练场帮助画布任一打开时
        // 忽略打开（关闭不受限，但那些界面打开时本面板必然关闭）
        bool togglePressed = Input.GetKeyDown(toggleKey) &&
            !SettingsUIRoot.IsOpen &&
            !InventoryInput.IsOpen &&
            !TaskPanelController.BlocksOtherPanels &&
            !DialogueRunner.IsDialogueActive &&
            !TrainingGroundHelpTrigger.IsHelpCanvasOpen;

        if (togglePressed)
        {
            if (IsOpen)
            {
                ClosePanel();
            }
            else
            {
                OpenPanel();
            }
        }
    }

    // U 入口：启用提示画布并开始会话（暂停、锁玩家、释放鼠标）
    private void OpenPanel()
    {
        if (IsOpen)
        {
            return;
        }

        if (!EnsurePanel())
        {
            Debug.LogWarning(
                "当前场景中没有提示画布（Tip Canvas），无法打开提示面板。",
                this
            );
            return;
        }

        IsOpen = true;

        panelCanvas.SetActive(true);
        PrimeMaskDissolve();

        BeginSession();
    }

    // U / 关闭按钮入口：禁用画布并结束会话
    public void ClosePanel()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;

        if (panelCanvas != null)
        {
            // 关闭前复位按钮填充：整体禁用时按钮收不到 PointerExit，
            // 悬停填充会停留在铺满状态带进下一轮
            foreach (MainMenuButtonHoverFill fill in
                panelCanvas.GetComponentsInChildren<MainMenuButtonHoverFill>(
                    true
                ))
            {
                fill.ResetVisualState();
            }

            panelCanvas.SetActive(false);
        }

        EndSession();
    }

    // 按名字懒查找提示画布（默认禁用，GameObject.Find 找不到，
    // 因此全量搜索并过滤出场景内的物体，排除预制体资产）；
    // 找到后顺带解析遮罩并接上关闭按钮
    private bool EnsurePanel()
    {
        if (panelCanvas != null)
        {
            return true;
        }

        Transform[] candidates =
            Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform candidate in candidates)
        {
            if (candidate.name != CanvasName)
            {
                continue;
            }

            // scene.IsValid() 排除未实例化的预制体资产
            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            panelCanvas = candidate.gameObject;
            FindMaskDissolve();
            WireCloseButton();

            return true;
        }

        return false;
    }

    // EnsurePanel 找到画布后调用：按名字查找溶解遮罩
    //（找不到时给出警告，面板照常打开但没有溶解入场效果）
    private void FindMaskDissolve()
    {
        maskDissolve = null;

        foreach (UIDissolveImage dissolve in
            panelCanvas.GetComponentsInChildren<UIDissolveImage>(true))
        {
            if (dissolve.name == MaskImageName)
            {
                maskDissolve = dissolve;
                return;
            }
        }

        Debug.LogWarning(
            "TipPanelController：画布上未找到挂 UIDissolveImage 的 " +
                MaskImageName + "，打开时没有溶解入场效果。",
            this
        );
    }

    // 关闭按钮：按名字查找并自动监听 onClick（场景无需手动接线）
    private void WireCloseButton()
    {
        foreach (Transform child in
            panelCanvas.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != CloseButtonName)
            {
                continue;
            }

            Button button = child.GetComponent<Button>();

            if (button == null)
            {
                Debug.LogWarning(
                    "TipPanelController：CloseButton 上没有 Button 组件，" +
                        "无法通过按钮关闭（可按 U 关闭）。",
                    this
                );
                return;
            }

            button.onClick.RemoveListener(ClosePanel);
            button.onClick.AddListener(ClosePanel);
            return;
        }

        Debug.LogWarning(
            "TipPanelController：画布上未找到 CloseButton，" +
                "只能按 U 关闭提示面板。",
            this
        );
    }

    // U 打开面板时的溶解入场：与任务面板一致——遮罩瞬时拉回完全
    // 显示并强制勾选射线检测（初始状态），再溶解消失露出提示内容；
    // 溶解时长使用遮罩组件上检查器里配置的值
    private void PrimeMaskDissolve()
    {
        if (maskDissolve == null)
        {
            return;
        }

        // 上一轮溶解完成后遮罩可能被禁用/取消勾选，打开时先启用
        // 回来并恢复“勾选”状态，随后开始溶解消失
        maskDissolve.gameObject.SetActive(true);

        if (maskDissolve.graphic != null)
        {
            maskDissolve.graphic.raycastTarget = true;
        }

        maskDissolve.SetVisible(true, true);
        maskDissolve.Hide();
    }

    // ===== 会话生命周期（打开/关闭面板时调用） =====

    private void BeginSession()
    {
        LockPlayerControl();

        // 释放鼠标（保存原状态，关闭时恢复）
        if (!hasSavedCursorState)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            hasSavedCursorState = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 存在 Player 的场景打开时暂停世界（时间 + 全局声音，
        // 与设置/任务/背包面板一致）；主菜单（无 Player）不碰
        if (FindObjectOfType<PlayerController>() != null)
        {
            previousTimeScale = Time.timeScale;
            previousAudioListenerPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            worldPaused = true;
        }
    }

    private void EndSession()
    {
        RestorePlayerControl();

        if (hasSavedCursorState)
        {
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
            hasSavedCursorState = false;
        }

        if (worldPaused)
        {
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioListenerPause;
            worldPaused = false;
        }
    }

    // 禁用玩家及其武器上的所有控制组件（与 TaskPanelController 的
    // 控制锁存模式一致），会话结束时按原样恢复
    private void LockPlayerControl()
    {
        PlayerController playerController =
            FindObjectOfType<PlayerController>();

        if (playerController == null)
        {
            return;
        }

        AddDisabled(playerController);

        Transform controlRoot = playerController.transform;

        foreach (WeaponController controller in
            controlRoot.GetComponentsInChildren<WeaponController>(true))
        {
            AddDisabled(controller);
        }

        foreach (WeaponEffects effects in
            controlRoot.GetComponentsInChildren<WeaponEffects>(true))
        {
            AddDisabled(effects);
        }

        foreach (PickupController pickup in
            controlRoot.GetComponentsInChildren<PickupController>(true))
        {
            AddDisabled(pickup);
        }

        AddDisabled(controlRoot.GetComponent<CameraRecoil>());
    }

    private void RestorePlayerControl()
    {
        foreach (Behaviour behaviour in disabledControls)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledControls.Clear();
    }

    // 只记录并停用当前处于启用状态的组件，恢复时按记录还原
    private void AddDisabled(Behaviour behaviour)
    {
        if (behaviour != null && behaviour.enabled)
        {
            behaviour.enabled = false;
            disabledControls.Add(behaviour);
        }
    }
}
