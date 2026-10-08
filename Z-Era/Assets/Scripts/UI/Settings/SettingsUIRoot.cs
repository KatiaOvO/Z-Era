using System.Collections.Generic;
using Migration.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 设置面板管理单例：启动时自动创建，配合 SettingsPanelSession
/// （挂在 SettingPanelRoot 上）工作。
/// 职责划分：
/// - 面板显隐：交给现有的统一组件——主菜单按钮走
///   MainMenuPanelOpener（含溶解动画），关闭按钮走
///   MainMenuPanelCloser，其他场景的 Esc 键由本类直接
///   激活/禁用设置面板；
/// - Esc 行为按场景分流：主菜单中 ESC 只关闭 PanelContainer
///   下已打开的面板（无打开面板则无反应，打开面板只能靠按钮），
///   其他场景中 ESC 开关设置面板；
/// - 打开面板的副作用（锁定玩家、释放鼠标、暂停世界）：
///   由 SettingsPanelSession 的 OnEnable/OnDisable 触发
///   BeginSession/EndSession，随面板激活状态自动跟随，
///   无论面板被哪种方式开关都成立。
/// 面板按名字 SettingPanelRoot 懒查找（能找到未激活物体）。
/// 把面板迁移到其他场景时无需改动本类。
/// </summary>
// 先于 InventoryInput 等 gameplay 键盘脚本执行：背包打开的
// 当帧按 ESC 时，本类先看到 IsOpen=true 而不打开设置面板，
// ESC 随后由 InventoryInput 关闭背包；若顺序相反，背包会先
// 关闭导致守卫在同一帧被穿透
[DefaultExecutionOrder(-50)]
public class SettingsUIRoot : MonoBehaviour
{
    public static SettingsUIRoot Instance { get; private set; }

    // 设置面板是否打开：库存、NPC 交互等键盘入口在打开前检查
    // 此标志，设置面板打开期间除 ESC 外的所有键盘操作一律忽略
    public static bool IsOpen => Instance != null && Instance.isOpen;

    // 面板物体在场景中的名字
    private const string PanelName = "SettingPanelRoot";

    // 面板最上层遮罩的名字（挂 UIDissolveImage）：
    // ESC 打开面板时播放溶解消失，与按钮打开的表现一致
    private const string MaskImageName = "SettingPanelMaskImage";

    // 遮罩溶解时长（秒）。游戏场景 ESC 打开时的溶解节奏；
    // 如需调整改这个常量即可
    private const float MaskDissolveDuration = 1f;

    // 主菜单场景名与面板容器（ESC 行为按场景分流用）
    private const string MainMenuSceneName = "MainMenu";
    private const string PanelContainerName = "PanelContainer";

    // 面板容器下以此后缀命名的子物体视为"可被 ESC 关闭的面板"
    private const string PanelRootSuffix = "PanelRoot";

    private GameObject panel;
    private GameObject panelContainer;
    private UIDissolveImage maskDissolve;
    private bool isOpen;

    // 打开前的鼠标状态，关闭时恢复
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool hasSavedCursorState;

    // 世界暂停状态：仅游戏场景（存在 Player）打开时暂停
    private bool worldPaused;
    private float previousTimeScale = 1f;
    private bool previousAudioListenerPause;

    // 打开期间被禁用的玩家控制组件，关闭时按原样恢复
    // （组件清单与 DialogueRunner / TrainingGroundControlLock
    // 的控制锁存模式一致）
    private readonly List<Behaviour> disabledControls =
        new List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
        {
            new GameObject("SettingsUIRoot")
                .AddComponent<SettingsUIRoot>();
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
        // 面板随旧场景销毁时 OnDisable 已正常收尾；
        // 这里兜底处理引用失效但状态未收尾的极端情况
        if (isOpen && panel == null)
        {
            EndSession();
        }
    }

    // 懒查找当前场景中的面板。面板复用上一次的缓存引用；
    // 首次查找不能用 GameObject.Find——它找不到未激活的物体，
    // 而设置面板在场景里默认就是禁用状态（打开时才激活），
    // 因此用全量搜索并过滤出场景内的物体（排除预制体资产）
    private bool EnsurePanel()
    {
        if (panel != null)
        {
            return true;
        }

        Transform[] candidates =
            Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform candidate in candidates)
        {
            if (candidate.name != PanelName)
            {
                continue;
            }

            // scene.IsValid() 排除未实例化的预制体资产
            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            panel = candidate.gameObject;
            return true;
        }

        return false;
    }

    // EnsurePanel 找到面板后调用：查找最上层的溶解遮罩
    // （按名字，可选；找不到只是没有溶解入场动画，面板照常打开）
    private void FindMaskDissolve()
    {
        maskDissolve = null;

        foreach (UIDissolveImage dissolve in
            panel.GetComponentsInChildren<UIDissolveImage>(true))
        {
            if (dissolve.name == MaskImageName)
            {
                maskDissolve = dissolve;
                return;
            }
        }
    }

    // ESC 打开面板时的溶解入场：与 MainMenuPanelOpener 的表现
    // 一致——遮罩先瞬时拉回完全显示，再溶解消失露出面板内容；
    // 面板内所有按钮的悬停表现统一复位（上次关闭可能中断了淡出）
    private void PrimeMaskDissolve()
    {
        if (maskDissolve == null)
        {
            return;
        }

        foreach (MainMenuButtonHoverFill fill in
            panel.GetComponentsInChildren<MainMenuButtonHoverFill>(true))
        {
            fill.ResetVisualState();
        }

        // 上一轮溶解完成后遮罩物体会被禁用，打开时先启用回来，
        // 再瞬时拉回完全显示，随后开始溶解消失
        maskDissolve.gameObject.SetActive(true);
        maskDissolve.SetVisible(true, true);
        maskDissolve.duration = MaskDissolveDuration;
        maskDissolve.Hide();
    }

    private void Update()
    {
        // 遮罩的 Raycast Target 同步（与 MainMenuPanelOpener 相同）：
        // 溶解中/完全显示时挡住射线，溶解完毕后放行，
        // 面板内容的滑条、按钮才能被点击
        if (maskDissolve != null && maskDissolve.graphic != null)
        {
            bool shouldBlockRaycast = !maskDissolve.isHideComplete;

            if (maskDissolve.graphic.raycastTarget != shouldBlockRaycast)
            {
                maskDissolve.graphic.raycastTarget = shouldBlockRaycast;
            }
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        // 主菜单：ESC 只负责关闭已打开的面板（含设置面板），
        // 没有打开的面板时无反应；打开面板只能通过对应按钮
        if (SceneManager.GetActiveScene().name == MainMenuSceneName)
        {
            CloseAnyOpenMenuPanel();
            return;
        }

        // 其他场景：ESC 开关设置面板
        if (isOpen)
        {
            Close();
            return;
        }

        // 对话期间禁止打开设置面板（与库存输入同一守卫）；
        // 训练场帮助画布、任务面板打开期间同理；
        // 背包打开期间 ESC 只作用于背包（由 InventoryInput 关闭）；
        // BlocksOtherPanels 还覆盖任务面板 ESC 关闭的当帧——
        // 其执行序位在本类之前，同帧不能把这次 ESC 拿来开设置
        if (DialogueRunner.IsDialogueActive ||
            TrainingGroundHelpTrigger.IsHelpCanvasOpen ||
            InventoryInput.IsOpen ||
            TaskPanelController.BlocksOtherPanels)
        {
            return;
        }

        Open();
    }

    // 主菜单：关闭 PanelContainer 下所有处于打开状态的 PanelRoot
    // 面板（设置/开始游戏/帮助/退出游戏等）。设置面板被关闭时，
    // 其 OnDisable 会自动触发会话收尾（解锁玩家、恢复时间缩放）
    private void CloseAnyOpenMenuPanel()
    {
        if (!EnsurePanelContainer())
        {
            return;
        }

        foreach (Transform child in panelContainer.transform)
        {
            if (child.name.EndsWith(PanelRootSuffix) &&
                child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    // 懒查找主菜单的面板容器。面板容器复用上一次的缓存引用，
    // 切场景销毁后自动重新查找
    private bool EnsurePanelContainer()
    {
        if (panelContainer != null)
        {
            return true;
        }

        Transform[] candidates =
            Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform candidate in candidates)
        {
            if (candidate.name != PanelContainerName)
            {
                continue;
            }

            // scene.IsValid() 排除未实例化的预制体资产
            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            panelContainer = candidate.gameObject;
            return true;
        }

        return false;
    }

    // Esc 或代码入口：激活面板（会话由面板的 OnEnable 自动开始）
    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        if (!EnsurePanel())
        {
            Debug.LogWarning(
                "当前场景中没有设置面板（SettingPanelRoot），" +
                "无法打开设置。",
                this
            );
            return;
        }

        FindMaskDissolve();
        panel.SetActive(true);
        PrimeMaskDissolve();
    }

    // Esc 或代码入口：禁用面板（会话由面板的 OnDisable 自动收尾）
    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }
        else
        {
            EndSession();
        }
    }

    // ===== 会话生命周期（由 SettingsPanelSession 触发） =====

    public void BeginSession(GameObject panelObject)
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;
        panel = panelObject;

        // 设置面板压住背包：打开面板时先关闭背包，
        // 避免两个界面叠在一起
        FindObjectOfType<InventoryInput>()?.CloseInventory();

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

        // 游戏场景打开时暂停世界（时间流 + 全局声音，与任务面板/
        // 背包一致）；主菜单（无 Player）不碰时间
        if (FindObjectOfType<PlayerController>() != null)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // timeScale = 0 不会停音频，需单独挂起监听器；
            // 面板自身的音效源带 ignoreListenerPause，不受影响
            previousAudioListenerPause = AudioListener.pause;
            AudioListener.pause = true;

            worldPaused = true;
        }
    }

    public void EndSession()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;

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

    // 禁用玩家及其武器上的所有控制组件（与 DialogueRunner 的
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
