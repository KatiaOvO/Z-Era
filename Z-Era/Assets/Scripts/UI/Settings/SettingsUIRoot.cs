using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 设置面板管理单例：启动时自动创建，配合 SettingsPanelSession
/// （挂在 SettingPanelRoot 上）工作。
/// 职责划分：
/// - 面板显隐：交给现有的统一组件——主菜单按钮走
///   MainMenuPanelOpener（含溶解动画），关闭按钮走
///   MainMenuPanelCloser，Esc 键由本类直接激活/禁用面板；
/// - 打开面板的副作用（锁定玩家、释放鼠标、暂停世界）：
///   由 SettingsPanelSession 的 OnEnable/OnDisable 触发
///   BeginSession/EndSession，随面板激活状态自动跟随，
///   无论面板被哪种方式开关都成立。
/// 面板按名字 SettingPanelRoot 懒查找（能找到未激活物体）。
/// 把面板迁移到其他场景时无需改动本类。
/// </summary>
public class SettingsUIRoot : MonoBehaviour
{
    public static SettingsUIRoot Instance { get; private set; }

    // 面板物体在场景中的名字
    private const string PanelName = "SettingPanelRoot";

    private GameObject panel;
    private bool isOpen;

    // 打开前的鼠标状态，关闭时恢复
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool hasSavedCursorState;

    // 世界暂停状态：仅游戏场景（存在 Player）打开时暂停
    private bool worldPaused;
    private float previousTimeScale = 1f;

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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }
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

        panel.SetActive(true);
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

        // 游戏场景打开时暂停世界；主菜单（无 Player）不碰时间
        if (FindObjectOfType<PlayerController>() != null)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
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
