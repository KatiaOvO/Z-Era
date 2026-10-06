using System.Collections.Generic;
using Migration.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 任务面板管理单例：按 T 开关当前场景的 Task Canvas（与 HUD Canvas
/// 同级的独立 Overlay 画布）。关闭方式：再按 T / ESC / 面板上的
/// 关闭按钮（挂 TaskPanelCloseButton）。
/// 打开表现与设置面板一致：遮罩 TaskPanelMaskBgImage（挂
/// UIDissolveImage）先瞬时拉回完全显示，再溶解消失露出面板内容；
/// 同时暂停世界（时间缩放 + 全局声音）、锁定玩家控制并释放鼠标，
/// 关闭时全部按原样恢复。
/// 本类自举为常驻单例（同 SettingsUIRoot）：Task Canvas 默认禁用，
/// 挂在画布上的脚本收不到按键，因此由本类常驻监听 T/ESC，按名字
/// 懒查找画布与遮罩——任务画布只放进需要查看任务的场景即可，
/// 无需手动接线。
/// 执行序位在 SettingsUIRoot（-50）之前：ESC 关闭任务面板的当帧
/// 记录帧号（escClosedFrame），设置面板据此跳过这一次 ESC，
/// 不会把同一次按键再拿去打开设置面板。
/// </summary>
[DefaultExecutionOrder(-60)]
public class TaskPanelController : MonoBehaviour
{
    public static TaskPanelController Instance { get; private set; }

    // 面板是否打开：其他全局面板的键盘入口在打开前检查此标志
    public static bool IsOpen { get; private set; }

    // 任务面板是否应压住其他全局面板（设置/背包等）：打开期间
    // 压住；ESC 关闭的当帧也压住，堵住"同帧先关面板再开设置"的竞态
    public static bool BlocksOtherPanels =>
        IsOpen || Time.frameCount == escClosedFrame;

    // 任务画布与遮罩在场景中的名字（遮罩挂 UIDissolveImage）
    private const string CanvasName = "Task Canvas";
    private const string MaskImageName = "TaskPanelMaskBgImage";

    // 开关面板的按键
    [SerializeField]
    private KeyCode toggleKey = KeyCode.T;

    private GameObject panelCanvas;
    private UIDissolveImage maskDissolve;
    private TaskPanelQuestList questList;

    // ESC 关闭面板时的帧号，-1 表示最近一次关闭不是 ESC 触发
    private static int escClosedFrame = -1;

    // 打开前的鼠标状态，关闭时恢复
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool hasSavedCursorState;

    // 世界暂停状态：仅存在 Player 的场景打开时暂停
    private float previousTimeScale = 1f;
    private bool previousAudioListenerPause;
    private bool worldPaused;

    // 打开期间被禁用的玩家控制组件，关闭时按原样恢复
    // （组件清单与 DialogueRunner / SettingsUIRoot 的控制锁存模式一致）
    private readonly List<Behaviour> disabledControls =
        new List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // 关闭域重载时静态状态会带进下一次运行，这里统一复位
        IsOpen = false;
        escClosedFrame = -1;

        if (Instance == null)
        {
            new GameObject("TaskPanelController")
                .AddComponent<TaskPanelController>();
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
        // 任务画布随旧场景销毁时状态可能未收尾，这里兜底恢复
        if (IsOpen && panelCanvas == null)
        {
            EndSession();
        }
    }

    private void Update()
    {
        // 遮罩的 Raycast Target 同步（与 SettingsUIRoot 相同）：
        // 溶解中/完全显示时挡住射线，溶解完毕后放行，
        // 后续加入面板内容的按钮才能被点击
        if (maskDissolve != null && maskDissolve.graphic != null)
        {
            bool shouldBlockRaycast = !maskDissolve.isHideComplete;

            if (maskDissolve.graphic.raycastTarget != shouldBlockRaycast)
            {
                maskDissolve.graphic.raycastTarget = shouldBlockRaycast;
            }
        }

        // T 开关面板。设置面板/背包/对话/训练场帮助画布任一打开时
        // 忽略打开（关闭不受限，但那些界面打开时本面板必然关闭）
        bool togglePressed = Input.GetKeyDown(toggleKey) &&
            !SettingsUIRoot.IsOpen &&
            !InventoryInput.IsOpen &&
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

            return;
        }

        // ESC：任务面板打开时优先关闭任务面板。帧号先于关闭记录，
        // 同帧更晚执行的 SettingsUIRoot 据此跳过这次 ESC
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            escClosedFrame = Time.frameCount;
            ClosePanel();
        }
    }

    // T 入口：启用任务画布并开始会话（暂停、锁玩家、释放鼠标）
    private void OpenPanel()
    {
        if (IsOpen)
        {
            return;
        }

        if (!EnsurePanel())
        {
            Debug.LogWarning(
                "当前场景中没有任务画布（Task Canvas），无法打开任务面板。",
                this
            );
            return;
        }

        IsOpen = true;

        panelCanvas.SetActive(true);
        PrimeMaskDissolve();

        BeginSession();

        // 任务列表随面板打开重建并订阅 QuestManager 事件实时刷新
        if (questList != null)
        {
            questList.OnPanelOpened();
        }
    }

    // T / ESC / 关闭按钮入口：禁用画布并结束会话
    public void ClosePanel()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;

        if (panelCanvas != null)
        {
            panelCanvas.SetActive(false);
        }

        // 先退订任务列表的事件再收尾，列表可能随画布一起销毁
        if (questList != null)
        {
            questList.OnPanelClosed();
        }

        EndSession();
    }

    // 按名字懒查找任务画布（默认禁用，GameObject.Find 找不到，
    // 因此全量搜索并过滤出场景内的物体，排除预制体资产）
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

            // 列表组件是可选的：画布上没挂时面板仍可正常开关
            questList =
                panelCanvas.GetComponentInChildren<TaskPanelQuestList>(
                    true
                );

            return true;
        }

        return false;
    }

    // EnsurePanel 找到画布后调用：按名字查找最上层的溶解遮罩
    //（找不到只是没有溶解入场动画，面板照常打开）
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
    }

    // T 打开面板时的溶解入场：与 SettingsUIRoot 的表现一致——
    // 遮罩先瞬时拉回完全显示，再溶解消失露出面板内容；
    // 溶解时长使用遮罩组件上检查器里配置的值
    private void PrimeMaskDissolve()
    {
        // 面板整体禁用时按钮收不到 PointerExit，悬停填充会停留在
        // 铺满状态带进下一轮：打开时统一复位所有按钮的悬停表现
        // （与 SettingsUIRoot 打开面板时的处理相同）
        foreach (MainMenuButtonHoverFill fill in
            panelCanvas.GetComponentsInChildren<MainMenuButtonHoverFill>(
                true
            ))
        {
            fill.ResetVisualState();
        }

        if (maskDissolve == null)
        {
            return;
        }

        // 上一轮溶解完成后遮罩可能被禁用，打开时先启用回来，
        // 再瞬时拉回完全显示，随后开始溶解消失
        maskDissolve.gameObject.SetActive(true);
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

        // 存在 Player 的场景打开时暂停世界；主菜单（无 Player）不碰
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

    // 禁用玩家及其武器上的所有控制组件（与 SettingsUIRoot 的
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
