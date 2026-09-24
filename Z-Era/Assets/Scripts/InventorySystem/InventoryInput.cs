using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class InventoryInput : MonoBehaviour
{
    [Header("背包")]

    [FormerlySerializedAs("inventorySystem")]
    [Tooltip("背包内容根物体，初始应为禁用状态")]
    public GameObject inventoryContainer;

    [Tooltip("打开或关闭背包的按键，例如 B")]
    public KeyCode toggleKey = KeyCode.B;

    [Header("背包背景")]

    [Tooltip("打开背包时显示的全屏背景 Canvas 或 Image")]
    public GameObject inventoryBackground;

    [Tooltip("打开背包时需要隐藏的 HUD Canvas")]
    public GameObject hudCanvas;

    [Tooltip("背景 Canvas 的最小平面距离，需大于道具圆盘深度")]
    [Min(0.1f)]
    public float backgroundPlaneDistance = 3f;

    [Header("暂停游戏")]

    [Tooltip("打开背包时写入的 Time.timeScale，0 表示暂停")]
    [Min(0f)]
    public float pauseTimeScale = 0f;

    [Tooltip("打开背包时是否暂停游戏声音")]
    public bool pauseAudioWhileInventoryOpen = true;

    [Tooltip("打开背包时需要暂停的玩家控制器；留空时自动查找")]
    [SerializeField]
    private PlayerController playerController;

    private Camera inventoryMainCamera;
    private int previousMainCameraCullingMask;
    private CameraClearFlags previousMainCameraClearFlags;
    private Color previousMainCameraBackgroundColor;
    private bool hasPreviousMainCameraState;

    private class InventoryLayerState
    {
        public Transform Target;
        public int Layer;
    }

    private readonly List<InventoryLayerState>
        inventoryLayerStates =
            new List<InventoryLayerState>();

    private bool isOpen;

    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool hasPreviousCursorState;

    private float previousTimeScale;
    private bool previousAudioListenerPause;
    private bool hasPreviousPauseState;

    private bool hasPreviousBackgroundState;
    private bool previousBackgroundActiveState;

    private bool hasPreviousHudCanvasState;
    private bool previousHudCanvasActiveState;

    private readonly List<Behaviour>
        temporarilyDisabledBehaviours =
            new List<Behaviour>();

    private void Update()
    {
        // InventoryInput 始终保持启用，负责监听开关和恢复异常关闭状态。
        if (Input.GetKeyDown(toggleKey))
        {
            SetInventoryOpen(!isOpen);
        }

        // 如果背包被其他脚本关闭，恢复游戏状态。
        if (isOpen &&
            inventoryContainer != null &&
            !inventoryContainer.activeSelf)
        {
            RestoreSession();
        }
    }

    private void OnDisable()
    {
        // 脚本或玩家被禁用时，也要恢复相机、HUD、时间和控制状态。
        if (isOpen)
        {
            if (inventoryContainer != null)
            {
                inventoryContainer.SetActive(false);
            }

            RestoreSession();
        }
    }

    private void SetInventoryOpen(bool open)
    {
        // 统一入口，避免重复打开或重复恢复状态。
        if (inventoryContainer == null)
        {
            Debug.LogError(
                "InventoryInput has no InventoryContainer!",
                this
            );

            return;
        }

        if (open == isOpen)
        {
            return;
        }

        if (open)
        {
            OpenInventory();
        }
        else
        {
            CloseInventory();
        }
    }

    private void OpenInventory()
    {
        // 顺序很重要：先保存状态，再暂停游戏、显示背景和启用容器。
        StorePauseState();
        StoreBackgroundState();
        StoreHudCanvasState();

        Time.timeScale = pauseTimeScale;

        if (pauseAudioWhileInventoryOpen)
        {
            AudioListener.pause = true;
        }

        DisableGameplayControls();
        StoreAndUnlockCursor();

        inventoryContainer.SetActive(true);

        if (inventoryBackground != null)
        {
            inventoryBackground.SetActive(true);
            ConfigureBackgroundLayout();
            Canvas.ForceUpdateCanvases();
        }

        if (hudCanvas != null)
        {
            hudCanvas.SetActive(false);
        }

        PrepareMainCameraForInventory();
        SetInventoryLayer(true);

        isOpen = true;
    }

    private void CloseInventory()
    {
        // 先关闭容器，再统一恢复所有临时状态。
        if (inventoryContainer != null)
        {
            inventoryContainer.SetActive(false);
        }

        RestoreSession();
    }

    private void RestoreSession()
    {
        // 所有恢复操作集中在这里，保证异常关闭也能回到正常状态。
        if (!isOpen)
        {
            return;
        }

        isOpen = false;

        if (inventoryContainer != null)
        {
            inventoryContainer.SetActive(false);
        }

        SetInventoryLayer(false);
        RestoreBackground();
        RestoreHudCanvas();
        RestorePauseState();
        RestoreMainCameraRendering();
        RestoreGameplayControls();
        RestoreCursor();
    }

    private void StorePauseState()
    {
        // 只记录第一次打开前的时间缩放和声音状态。
        if (hasPreviousPauseState)
        {
            return;
        }

        previousTimeScale =
            Time.timeScale;

        previousAudioListenerPause =
            AudioListener.pause;

        hasPreviousPauseState = true;
    }

    private void RestorePauseState()
    {
        // 恢复打开背包之前的时间缩放和 AudioListener 状态。
        if (!hasPreviousPauseState)
        {
            return;
        }

        Time.timeScale =
            previousTimeScale;

        AudioListener.pause =
            previousAudioListenerPause;

        hasPreviousPauseState = false;
    }

    private void StoreBackgroundState()
    {
        // 保存背景原始启用状态，避免关闭后覆盖场景配置。
        if (inventoryBackground == null)
        {
            hasPreviousBackgroundState = false;
            return;
        }

        previousBackgroundActiveState =
            inventoryBackground.activeSelf;

        hasPreviousBackgroundState = true;
    }

    private void RestoreBackground()
    {
        // 将背景恢复到打开背包前的启用状态。
        if (!hasPreviousBackgroundState ||
            inventoryBackground == null)
        {
            return;
        }

        inventoryBackground.SetActive(
            previousBackgroundActiveState
        );

        hasPreviousBackgroundState = false;
    }

    private void StoreHudCanvasState()
    {
        // 保存 HUD 原始启用状态，关闭背包时恢复。
        if (hudCanvas == null)
        {
            hasPreviousHudCanvasState = false;
            return;
        }

        previousHudCanvasActiveState =
            hudCanvas.activeSelf;

        hasPreviousHudCanvasState = true;
    }

    private void RestoreHudCanvas()
    {
        // 背包关闭后恢复 HUD Canvas。
        if (!hasPreviousHudCanvasState ||
            hudCanvas == null)
        {
            return;
        }

        hudCanvas.SetActive(
            previousHudCanvasActiveState
        );

        hasPreviousHudCanvasState = false;
    }

    private void PrepareMainCameraForInventory()
    {
        // 临时只渲染 UI 和背包层，避免场景物体遮挡背景。
        inventoryMainCamera = Camera.main;

        if (inventoryMainCamera == null)
        {
            Debug.LogWarning(
                "InventoryInput could not find MainCamera.",
                this
            );

            return;
        }

        if (!hasPreviousMainCameraState)
        {
            previousMainCameraCullingMask =
                inventoryMainCamera.cullingMask;

            previousMainCameraClearFlags =
                inventoryMainCamera.clearFlags;

            previousMainCameraBackgroundColor =
                inventoryMainCamera.backgroundColor;

            hasPreviousMainCameraState = true;
        }

        int uiLayer =
            LayerMask.NameToLayer("UI");

        if (uiLayer < 0)
        {
            Debug.LogError(
                "InventoryInput requires a UI layer.",
                this
            );

            return;
        }

        // 只渲染UI和背包道具，避免近处场景遮挡背景。
        inventoryMainCamera.cullingMask =
            1 << uiLayer;

        inventoryMainCamera.clearFlags =
            CameraClearFlags.SolidColor;

        inventoryMainCamera.backgroundColor =
            Color.black;
    }

    private void RestoreMainCameraRendering()
    {
        // 恢复 Main Camera 原始裁剪、清屏和背景颜色。
        if (!hasPreviousMainCameraState)
        {
            return;
        }

        if (inventoryMainCamera != null)
        {
            inventoryMainCamera.cullingMask =
                previousMainCameraCullingMask;

            inventoryMainCamera.clearFlags =
                previousMainCameraClearFlags;

            inventoryMainCamera.backgroundColor =
                previousMainCameraBackgroundColor;
        }

        hasPreviousMainCameraState = false;
        inventoryMainCamera = null;
    }

    private void SetInventoryLayer(bool useInventoryLayer)
    {
        // 打开时把所有背包物体切到 UI 层，关闭时恢复原层。
        if (inventoryContainer == null)
        {
            return;
        }

        if (useInventoryLayer)
        {
            int uiLayer =
                LayerMask.NameToLayer("UI");

            if (uiLayer < 0)
            {
                return;
            }

            inventoryLayerStates.Clear();

            foreach (
                Transform target in
                inventoryContainer
                    .GetComponentsInChildren<
                        Transform
                    >(true)
            )
            {
                if (target == null)
                {
                    continue;
                }

                inventoryLayerStates.Add(
                    new InventoryLayerState
                    {
                        Target = target,
                        Layer = target.gameObject.layer
                    }
                );

                target.gameObject.layer = uiLayer;
            }

            return;
        }

        foreach (
            InventoryLayerState state in
            inventoryLayerStates
        )
        {
            if (state != null &&
                state.Target != null)
            {
                state.Target.gameObject.layer =
                    state.Layer;
            }
        }

        inventoryLayerStates.Clear();
    }

    private void DisableGameplayControls()
    {
        // 记录并禁用当前已启用的玩家、武器和相机控制组件。
        temporarilyDisabledBehaviours.Clear();

        if (playerController == null)
        {
            playerController =
                FindObjectOfType<PlayerController>();
        }

        AddControlIfEnabled(
            playerController
        );

        Transform controlRoot =
            playerController != null
                ? playerController.transform
                : null;

        if (controlRoot == null)
        {
            return;
        }

        foreach (
            WeaponController controller in
            controlRoot.GetComponentsInChildren<
                WeaponController
            >(true)
        )
        {
            if (controller.gameObject
                    .activeInHierarchy)
            {
                AddControlIfEnabled(controller);
            }
        }

        foreach (
            WeaponEffects effects in
            controlRoot.GetComponentsInChildren<
                WeaponEffects
            >(true)
        )
        {
            if (effects.gameObject
                    .activeInHierarchy)
            {
                AddControlIfEnabled(effects);
            }
        }

        Transform weaponCameraTransform =
            controlRoot.Find("WeaponCamera");

        if (weaponCameraTransform != null)
        {
            Camera weaponCamera =
                weaponCameraTransform.GetComponent<Camera>();

            AddControlIfEnabled(weaponCamera);
        }

        CameraRecoil cameraRecoil =
            controlRoot.GetComponent<CameraRecoil>();

        AddControlIfEnabled(cameraRecoil);

        foreach (
            Behaviour behaviour in
            temporarilyDisabledBehaviours
        )
        {
            behaviour.enabled = false;
        }
    }

    private void ConfigureBackgroundLayout()
    {
        // 背景使用 Screen Space - Camera，并强制 Rect 覆盖屏幕。
        if (inventoryBackground == null)
        {
            return;
        }

        Canvas canvas =
            inventoryBackground.GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas =
                inventoryBackground
                    .GetComponentInParent<Canvas>();
        }

        if (canvas != null)
        {
            RectTransform canvasRect =
                canvas.transform as RectTransform;

            // 3D道具必须绘制在背景前方，使用摄像机空间Canvas。
            canvas.renderMode =
                RenderMode.ScreenSpaceCamera;

            Camera inventoryCamera =
                Camera.main;

            if (inventoryCamera != null)
            {
                canvas.worldCamera =
                    inventoryCamera;
            }

            InventoryRadialView radialView =
                inventoryContainer != null
                    ? inventoryContainer
                        .GetComponent<
                            InventoryRadialView
                        >()
                    : null;

            float configuredPlaneDistance =
                backgroundPlaneDistance > 0.01f
                    ? backgroundPlaneDistance
                    : 3f;

            float requiredPlaneDistance =
                Mathf.Max(
                    0.1f,
                    configuredPlaneDistance
                );

            if (radialView != null)
            {
                requiredPlaneDistance =
                    Mathf.Max(
                        requiredPlaneDistance,
                        radialView.viewDistance + 0.1f
                    );
            }

            canvas.planeDistance =
                requiredPlaneDistance;

            ConfigureFullscreenRect(canvasRect);
        }

        RectTransform backgroundRect =
            inventoryBackground
                .GetComponent<RectTransform>();

        if (backgroundRect != null)
        {
            ConfigureFullscreenRect(backgroundRect);
        }

        if (canvas != null &&
            canvas.gameObject == inventoryBackground)
        {
            if (canvas.transform.childCount > 0)
            {
                Transform child =
                    canvas.transform.GetChild(0);
                RectTransform childRect =
                    child as RectTransform;

                ConfigureFullscreenRect(childRect);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private void ConfigureFullscreenRect(
        RectTransform rect
    )
    {
        // 将 Canvas 或背景 Image 设置为相对父节点的全屏 Rect。
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
    }

    private void AddControlIfEnabled(
        Behaviour behaviour
    )
    {
        // 只记录原本启用的组件，恢复时避免错误启用已禁用组件。
        if (behaviour == null ||
            !behaviour.enabled)
        {
            return;
        }

        if (!temporarilyDisabledBehaviours
                .Contains(behaviour))
        {
            temporarilyDisabledBehaviours
                .Add(behaviour);
        }
    }

    private void RestoreGameplayControls()
    {
        // 恢复打开背包前启用的控制组件。
        foreach (
            Behaviour behaviour in
            temporarilyDisabledBehaviours
        )
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        temporarilyDisabledBehaviours.Clear();
    }

    private void StoreAndUnlockCursor()
    {
        // 保存并解锁鼠标，背包内需要自由移动指针。
        previousCursorLockState =
            Cursor.lockState;

        previousCursorVisible =
            Cursor.visible;

        hasPreviousCursorState = true;

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        // 恢复打开背包前的鼠标锁定和可见状态。
        if (!hasPreviousCursorState)
        {
            return;
        }

        Cursor.lockState =
            previousCursorLockState;

        Cursor.visible =
            previousCursorVisible;

        hasPreviousCursorState = false;
    }
}
