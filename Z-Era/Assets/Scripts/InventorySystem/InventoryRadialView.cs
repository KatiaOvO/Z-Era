using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryRadialView : MonoBehaviour
{
    // 道具临时使用的渲染队列，需要排在 Canvas(3000) 之后，
    // 保证道具绘制在溶解背景之上；关闭背包时还原原队列。
    private const int ItemOverlayRenderQueue = 3200;

    // 只有一个道具时圆盘允许左右摆动的最大角度，不暴露到 Inspector。
    private const float SingleItemSwingAngle = 15f;

    // 背包展示期间使用的无光照 Shader，避免场景灯光影响道具显示。
    private static Shader itemUnlitShader;

    private static Shader ItemUnlitShader
    {
        get
        {
            if (itemUnlitShader == null)
            {
                itemUnlitShader =
                    Shader.Find(
                        "Universal Render Pipeline/Unlit"
                    );
            }

            return itemUnlitShader;
        }
    }

    [Serializable]
    public class InventoryItem
    {
        [Tooltip("背包中的道具根节点")]
        public Transform item;

        [Tooltip("道具显示时的额外旋转角度")]
        public Vector3 displayRotationOffset = Vector3.zero;

        [Tooltip("道具在背包中的基础缩放倍数")]
        public float inventoryScaleMultiplier = 1f;
    }

    private class ItemRuntimeState
    {
        public InventoryItem Entry;
        public Transform Item;
        public Renderer Renderer;
        public Vector3 OriginalLocalPosition;
        public Quaternion OriginalLocalRotation;
        public Vector3 OriginalLocalScale;
        // 不含 displayRotationOffset 的摄像机相对旋转基线。
        public Quaternion BaseCameraRelativeRotation;
        public Vector3 DisplayLocalScale;
        public Quaternion LastAppliedLocalRotation;
        public Vector3 LastAppliedLocalScale;
        public bool OriginalActiveSelf;
        public Renderer[] Renderers;
        public Material[][] OriginalMaterials;
        public Collider[] Colliders;
        public bool[] ColliderEnabled;
        public Rigidbody[] Rigidbodies;
        public bool[] RigidbodyKinematic;
        public bool[] RigidbodyUseGravity;
        public Vector3[] RigidbodyVelocity;
        public Vector3[] RigidbodyAngularVelocity;
    }

    [Header("摄像机")]
    [Tooltip("主摄像机，用于固定圆盘位置并计算道具朝向")]
    [SerializeField] private Camera mainCamera;

    [Header("发现道具")]
    [Tooltip("自动将 InventoryContainer 的直接子物体识别为道具")]
    public bool includeDirectChildren = true;

    [Tooltip("可单独配置旋转和缩放的道具列表")]
    public InventoryItem[] configuredItems = new InventoryItem[0];

    [Header("圆盘布局")]
    [Tooltip("圆盘中心距离摄像机的距离")]
    [Min(0.5f)] public float viewDistance = 1.5f;

    [Tooltip("圆盘相对摄像机视线的垂直偏移")]
    public float diskHeightOffset = 0f;

    [Tooltip("圆盘相对摄像机的倾斜角度")]
    [Range(-180f, 180f)] public float diskTiltAngle = 20f;

    [Tooltip("道具圆环半径")]
    [Min(0.1f)] public float ringRadius = 0.65f;

    [Tooltip("选中道具向摄像机靠近的距离")]
    [Min(0f)] public float selectedBringForward = 0.35f;

    [Tooltip("道具进入选中槽位的角度：0右、90上、180左、270下")]
    [Range(0f, 360f)] public float selectionSlotAngle = 90f;

    [Tooltip("选中道具的放大倍数")]
    [Min(0.01f)] public float selectedScaleMultiplier = 1.2f;

    [Header("鼠标操作")]
    [Tooltip("鼠标水平拖动时圆环每像素旋转的角度")]
    [Min(0f)] public float dragRotationSpeed = 0.35f;

    [Tooltip("拖动过程中圆环跟随鼠标的速度")]
    [Min(0.1f)] public float dragFollowSpeed = 30f;

    [Tooltip("松开鼠标后圆环吸附到道具角度的速度")]
    [Min(0.1f)] public float snapSpeed = 12f;

    [Tooltip("松开鼠标后的惯性减速速度")]
    [Min(0f)] public float inertiaDeceleration = 900f;

    [Tooltip("低于该角速度时停止惯性并吸附")]
    [Min(0f)] public float inertiaMinimumSpeed = 30f;

    [Tooltip("拖动时允许的最大角速度")]
    [Min(1f)] public float maximumDragSpeed = 1200f;

    [Header("吸附手感")]
    [Tooltip("旋转过程中选中道具的吸附强度")]
    [Range(0f, 1f)] public float movingSelectionAttraction = 0.2f;

    [Tooltip("角速度低于该值后开始增强吸附")]
    [Min(0f)] public float snapAttractionStartSpeed = 180f;

    [Tooltip("吸附强度向目标值平滑变化的速度")]
    [Min(0.1f)] public float attractionBlendSpeed = 8f;

    [Tooltip("道具移动到选中位置的过渡速度")]
    [Min(0.1f)] public float selectionBlendSpeed = 8f;

    [Tooltip("点击道具时允许的屏幕距离")]
    [Min(1f)] public float clickSelectionRadiusPixels = 60f;

    [Tooltip("移动距离小于该值时视为点击，否则视为拖动")]
    [Min(0f)] public float dragClickThresholdPixels = 8f;

    [Header("开场动画")]
    [Tooltip("开启后道具起始位置自动放到画面下方之外，距离按摄像机视场计算；关闭则使用下方手动距离")]
    public bool introStartOffScreen = true;

    [Tooltip("introStartOffScreen 关闭时，道具从下方移入的距离")]
    [Min(0f)] public float introDropDistance = 0.35f;

    [Tooltip("道具移入动画的时长，单位为秒，使用先快后慢的缓出曲线")]
    [Min(0f)] public float introDuration = 0.5f;

    [Header("转动音效")]
    [Tooltip("选中项每移动一个道具时播放的齿轮声，留空则不播放")]
    [SerializeField]
    private AudioClip rotationTickSound;

    [Tooltip("齿轮声音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float rotationTickVolume = 1f;

    [Tooltip("齿轮声音调随机浮动幅度（0~0.5），轻微变化让连续转动更自然")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float rotationTickPitchVariation = 0.05f;

    private InventoryItem[] runtimeItems;
    private ItemRuntimeState[] itemStates;

    private bool isIntroActive;
    private float introElapsedTime;
    private float introDropDistanceRuntime;

    // 选中道具变化时触发：参数为道具索引与道具根节点，
    // 供描述文本等外部展示系统订阅。
    public event Action<int, Transform> SelectionChanged;

    private bool isDragging;
    private bool isInertiaActive;
    private Vector3 mouseDownPosition;
    private float previousMouseX;
    private float dragAngularVelocity;

    private float currentDiskRotation;
    private float targetDiskRotation;

    private int targetSelectionIndex;
    private int currentSelectionIndex;
    private int previousSelectionIndex;
    private float selectionBlend = 1f;
    private float currentSelectionAttraction = 1f;

    private bool hasOriginalContainerTransform;
    private Vector3 originalContainerLocalPosition;
    private Quaternion originalContainerLocalRotation;
    private Vector3 originalContainerLocalScale;

    private AudioSource tickAudioSource;

    private void Awake()
    {
        if (rotationTickSound == null)
        {
            return;
        }

        // 专属 AudioSource，代码自动创建，无需在场景中手动添加。
        // 打开背包时 AudioListener.pause = true，转动发生在暂停期间，
        // 必须忽略全局暂停才能发声。
        tickAudioSource = gameObject.AddComponent<AudioSource>();
        tickAudioSource.playOnAwake = false;
        tickAudioSource.spatialBlend = 0f;
        tickAudioSource.ignoreListenerPause = true;
    }

    private void PlayRotationTick()
    {
        if (rotationTickSound == null || tickAudioSource == null)
        {
            return;
        }

        // 每次轻微随机音调，连续转动时听起来更像真实齿轮而非复读。
        // 文件顶部 using System 与 UnityEngine 的 Random 重名，需全限定。
        tickAudioSource.pitch =
            1f + UnityEngine.Random.Range(
                -rotationTickPitchVariation,
                rotationTickPitchVariation
            );

        tickAudioSource.PlayOneShot(
            rotationTickSound,
            rotationTickVolume
        );
    }

    private void OnEnable()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (!hasOriginalContainerTransform)
        {
            originalContainerLocalPosition =
                transform.localPosition;

            originalContainerLocalRotation =
                transform.localRotation;

            originalContainerLocalScale =
                transform.localScale;

            hasOriginalContainerTransform = true;
        }

        // 必须先对齐当前摄像机，再保存道具相对摄像机的旋转。
        // 否则上一次背包留下的Container朝向会污染道具朝向。
        if (mainCamera != null)
        {
            AnchorToCamera();
        }

        // 先刷新子物体列表，再保存和停用道具物理状态。
        RefreshRuntimeItems();
        CaptureAndPrepareItems();
        ResetDiskState();
        StartIntroAnimation();

        // 通知外部当前选中项，打开背包时描述文本才有初始内容。
        NotifySelectionChanged();
    }

    private void OnDisable()
    {
        // 关闭背包时恢复道具原始 Transform、碰撞体和刚体状态。
        RestoreItemState();

        if (hasOriginalContainerTransform)
        {
            transform.localPosition =
                originalContainerLocalPosition;

            transform.localRotation =
                originalContainerLocalRotation;

            transform.localScale =
                originalContainerLocalScale;
        }
    }

    private void Update()
    {
        // 使用 unscaledDeltaTime，保证 Time.timeScale = 0 时仍能操作背包。
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        // 移入动画播放期间禁止旋转和点击，动画结束后才允许交互。
        if (isIntroActive)
        {
            introElapsedTime += Time.unscaledDeltaTime;

            if (introElapsedTime >= introDuration)
            {
                isIntroActive = false;
            }

            return;
        }

        HandleMouseInput();
        UpdateRotation(Time.unscaledDeltaTime);
    }

    private void StartIntroAnimation()
    {
        // 默认根据摄像机视场计算移入距离，保证道具起始点完全在画面下缘之外：
        // 圆盘距离处的半屏高 + 圆环半径，再加少量余量。
        float dropDistance = introDropDistance;

        if (introStartOffScreen && mainCamera != null)
        {
            float halfFovRadians =
                mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;

            float halfScreenHeight =
                Mathf.Tan(halfFovRadians) * viewDistance;

            dropDistance =
                halfScreenHeight + ringRadius + 0.25f;
        }

        // 每次打开背包都重新播移入动画；没有道具或时长为 0 时直接跳过。
        isIntroActive =
            dropDistance > 0f &&
            introDuration > 0f &&
            runtimeItems != null &&
            runtimeItems.Length > 0;

        introDropDistanceRuntime = dropDistance;
        introElapsedTime = 0f;
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            return;
        }

        // 每帧最后再对齐摄像机，避免玩家视角变化造成布局偏差。
        AnchorToCamera();
        UpdateItemLayout();
    }

    private void RefreshRuntimeItems()
    {
        // 合并手动配置的道具和自动发现的直接子物体。
        List<InventoryItem> result = new List<InventoryItem>();
        HashSet<Transform> addedItems = new HashSet<Transform>();

        if (configuredItems != null)
        {
            foreach (InventoryItem entry in configuredItems)
            {
                if (entry == null || entry.item == null)
                {
                    continue;
                }

                if (entry.item.parent != transform)
                {
                    continue;
                }

                if (addedItems.Add(entry.item))
                {
                    result.Add(entry);
                }
            }
        }

        if (includeDirectChildren)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (addedItems.Contains(child))
                {
                    continue;
                }

                bool hasRenderer = child.GetComponentInChildren<Renderer>(true) != null;
                if (hasRenderer)
                {
                    addedItems.Add(child);
                    result.Add(new InventoryItem
                    {
                        item = child,
                        displayRotationOffset = Vector3.zero,
                        inventoryScaleMultiplier = 1f
                    });
                }
            }
        }

        runtimeItems = result.ToArray();
    }

    private void CaptureAndPrepareItems()
    {
        if (runtimeItems == null)
        {
            itemStates = null;
            return;
        }

        itemStates = new ItemRuntimeState[runtimeItems.Length];

        for (int i = 0; i < runtimeItems.Length; i++)
        {
            InventoryItem entry = runtimeItems[i];
            if (entry == null || entry.item == null)
            {
                continue;
            }

            Transform item = entry.item;
            // 保存原始 Transform 和物理状态，关闭背包时可完整恢复。
            ItemRuntimeState state = new ItemRuntimeState
            {
                Entry = entry,
                Item = item,
                Renderer = item.GetComponentInChildren<Renderer>(true),
                OriginalLocalPosition = item.localPosition,
                OriginalLocalRotation = item.localRotation,
                OriginalLocalScale = item.localScale,
                OriginalActiveSelf = item.gameObject.activeSelf
            };

            // 实例化材质并把渲染队列提到 Canvas 之后，
            // 道具才能绘制在溶解背景之上；关闭时还原。
            state.Renderers =
                item.GetComponentsInChildren<Renderer>(true);

            state.OriginalMaterials =
                new Material[state.Renderers.Length][];

            for (int r = 0; r < state.Renderers.Length; r++)
            {
                Renderer itemRenderer = state.Renderers[r];

                if (itemRenderer == null)
                {
                    continue;
                }

                state.OriginalMaterials[r] =
                    itemRenderer.sharedMaterials;

                Material[] instanceMaterials =
                    itemRenderer.materials;

                foreach (
                    Material itemMaterial in
                        instanceMaterials
                )
                {
                    if (itemMaterial == null)
                    {
                        continue;
                    }

                    // 先保存贴图与颜色再换 Unlit Shader：
                    // URP Lit 与 URP Unlit 属性同名会自动保留，
                    // 其他 Shader 则手动搬运，避免道具变成纯白。
                    if (ItemUnlitShader != null)
                    {
                        Texture baseMap =
                            itemMaterial.HasProperty("_BaseMap")
                                ? itemMaterial.GetTexture("_BaseMap")
                                : null;

                        if (baseMap == null &&
                            itemMaterial.HasProperty("_MainTex"))
                        {
                            baseMap =
                                itemMaterial.GetTexture("_MainTex");
                        }

                        Color baseColor =
                            itemMaterial.HasProperty("_BaseColor")
                                ? itemMaterial.GetColor("_BaseColor")
                                : Color.white;

                        itemMaterial.shader = ItemUnlitShader;

                        if (baseMap != null &&
                            itemMaterial.HasProperty("_BaseMap"))
                        {
                            itemMaterial.SetTexture(
                                "_BaseMap",
                                baseMap
                            );
                        }

                        if (itemMaterial.HasProperty("_BaseColor"))
                        {
                            itemMaterial.SetColor(
                                "_BaseColor",
                                baseColor
                            );
                        }
                    }

                    itemMaterial.renderQueue =
                        ItemOverlayRenderQueue;
                }

                itemRenderer.materials = instanceMaterials;
            }

            state.Colliders = item.GetComponentsInChildren<Collider>(true);
            state.ColliderEnabled = new bool[state.Colliders.Length];
            for (int c = 0; c < state.Colliders.Length; c++)
            {
                if (state.Colliders[c] != null)
                {
                    state.ColliderEnabled[c] = state.Colliders[c].enabled;
                }
            }

            state.Rigidbodies = item.GetComponentsInChildren<Rigidbody>(true);
            state.RigidbodyKinematic = new bool[state.Rigidbodies.Length];
            state.RigidbodyUseGravity = new bool[state.Rigidbodies.Length];
            state.RigidbodyVelocity = new Vector3[state.Rigidbodies.Length];
            state.RigidbodyAngularVelocity = new Vector3[state.Rigidbodies.Length];

            for (int r = 0; r < state.Rigidbodies.Length; r++)
            {
                Rigidbody body = state.Rigidbodies[r];
                if (body == null)
                {
                    continue;
                }

                state.RigidbodyKinematic[r] = body.isKinematic;
                state.RigidbodyUseGravity[r] = body.useGravity;
                state.RigidbodyVelocity[r] = body.velocity;
                state.RigidbodyAngularVelocity[r] = body.angularVelocity;
            }

            Quaternion cameraRotationAtCapture =
                mainCamera != null
                    ? mainCamera.transform.rotation
                    : Quaternion.identity;

            state.BaseCameraRelativeRotation =
                Quaternion.Inverse(
                    cameraRotationAtCapture
                ) *
                item.rotation;

            state.DisplayLocalScale =
                item.localScale;

            itemStates[i] = state;
            item.gameObject.SetActive(true);
            item.rotation =
                cameraRotationAtCapture *
                state.BaseCameraRelativeRotation *
                Quaternion.Euler(
                    entry.displayRotationOffset
                );

            state.LastAppliedLocalRotation =
                item.localRotation;

            state.LastAppliedLocalScale =
                item.localScale;

            DisableItemPhysics(state);
        }
    }

    private void DisableItemPhysics(ItemRuntimeState state)
    {
        // 背包展示期间禁止道具碰撞和重力，避免影响场景。
        if (state.Colliders != null)
        {
            foreach (Collider collider in state.Colliders)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        if (state.Rigidbodies == null)
        {
            return;
        }

        foreach (Rigidbody body in state.Rigidbodies)
        {
            if (body == null)
            {
                continue;
            }

            bool wasKinematic = body.isKinematic;
            body.isKinematic = true;
            body.useGravity = false;

            if (!wasKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }

    private void RestoreItemState()
    {
        if (itemStates == null)
        {
            return;
        }

        foreach (ItemRuntimeState state in itemStates)
        {
            if (state == null || state.Item == null)
            {
                continue;
            }

            RestoreItemMaterials(state);
            RestoreItemPhysics(state);
            state.Item.localPosition = state.OriginalLocalPosition;
            state.Item.localRotation = state.OriginalLocalRotation;
            state.Item.localScale = state.OriginalLocalScale;
            state.Item.gameObject.SetActive(state.OriginalActiveSelf);
        }
    }

    private void RestoreItemMaterials(ItemRuntimeState state)
    {
        // 还原原始共享材质，丢弃背包期间实例化的高队列副本。
        if (state.Renderers == null ||
            state.OriginalMaterials == null)
        {
            return;
        }

        for (int r = 0; r < state.Renderers.Length; r++)
        {
            Renderer itemRenderer = state.Renderers[r];

            if (itemRenderer != null &&
                state.OriginalMaterials[r] != null)
            {
                itemRenderer.sharedMaterials =
                    state.OriginalMaterials[r];
            }
        }
    }

    private void RestoreItemPhysics(ItemRuntimeState state)
    {
        if (state.Colliders != null)
        {
            for (int i = 0; i < state.Colliders.Length; i++)
            {
                Collider collider = state.Colliders[i];
                if (collider != null)
                {
                    collider.enabled = state.ColliderEnabled[i];
                }
            }
        }

        if (state.Rigidbodies == null)
        {
            return;
        }

        for (int i = 0; i < state.Rigidbodies.Length; i++)
        {
            Rigidbody body = state.Rigidbodies[i];
            if (body == null)
            {
                continue;
            }

            bool wasKinematic = state.RigidbodyKinematic[i];
            body.isKinematic = wasKinematic;
            body.useGravity = state.RigidbodyUseGravity[i];

            if (!wasKinematic)
            {
                body.velocity = state.RigidbodyVelocity[i];
                body.angularVelocity = state.RigidbodyAngularVelocity[i];
            }
        }
    }

    private void ResetDiskState()
    {
        isDragging = false;
        isInertiaActive = false;
        dragAngularVelocity = 0f;

        targetSelectionIndex = 0;
        currentSelectionIndex = 0;
        previousSelectionIndex = 0;
        selectionBlend = 1f;
        currentSelectionAttraction = 1f;

        targetDiskRotation = GetDesiredRotation(0);
        currentDiskRotation = targetDiskRotation;
    }

    private void AnchorToCamera()
    {
        // 圆盘使用摄像机局部坐标，保证抬头或低头时位置不变。
        transform.position =
            mainCamera.transform.TransformPoint(
                new Vector3(
                    0f,
                    diskHeightOffset,
                    viewDistance
                )
            );

        transform.rotation =
            mainCamera.transform.rotation *
            Quaternion.Euler(
                diskTiltAngle,
                0f,
                0f
            );
    }

    private void HandleMouseInput()
    {
        // 区分点击和拖动：短距离抬起视为点击，否则进入惯性拖动。
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            isInertiaActive = false;
            dragAngularVelocity = 0f;
            mouseDownPosition = Input.mousePosition;
            previousMouseX = Input.mousePosition.x;
            return;
        }

        if (!isDragging)
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            float currentMouseX = Input.mousePosition.x;
            float horizontalDelta = currentMouseX - previousMouseX;
            float deltaTime =
                Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

            float rotationDelta =
                -horizontalDelta * dragRotationSpeed;

            targetDiskRotation += rotationDelta;

            // 只有一个道具时不允许绕圈，仅在基础角度左右小幅度摆动。
            if (runtimeItems != null && runtimeItems.Length == 1)
            {
                float baseRotation = GetDesiredRotation(0);

                targetDiskRotation = Mathf.Clamp(
                    targetDiskRotation,
                    baseRotation - SingleItemSwingAngle,
                    baseRotation + SingleItemSwingAngle
                );
            }

            float instantaneousSpeed =
                rotationDelta / deltaTime;

            dragAngularVelocity =
                Mathf.Lerp(
                    dragAngularVelocity,
                    instantaneousSpeed,
                    0.35f
                );

            dragAngularVelocity =
                Mathf.Clamp(
                    dragAngularVelocity,
                    -maximumDragSpeed,
                    maximumDragSpeed
                );

            previousMouseX = currentMouseX;
            UpdateTargetSelection();
            return;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;

            float dragDistance =
                Vector3.Distance(
                    Input.mousePosition,
                    mouseDownPosition
                );

            if (dragDistance <= dragClickThresholdPixels)
            {
                int clickedIndex = FindClickedItem();
                if (clickedIndex >= 0)
                {
                    SelectItem(clickedIndex);
                    return;
                }
            }

            if (runtimeItems != null && runtimeItems.Length == 1)
            {
                // 单个道具时禁用惯性，松手直接回到基础角度。
                SnapToNearestSelection();
            }
            else if (Mathf.Abs(dragAngularVelocity) >= inertiaMinimumSpeed)
            {
                isInertiaActive = true;
            }
            else
            {
                SnapToNearestSelection();
            }
        }
    }

    private void UpdateTargetSelection()
    {
        // 根据目标圆环角度计算当前最接近选中槽位的道具。
        int nearestIndex =
            FindSelectionIndex(targetDiskRotation);
        SetTargetSelectionIndex(nearestIndex);
    }

    private void SelectItem(int itemIndex)
    {
        // 点击道具时使用最短角度路径把圆环转到该道具。
        isInertiaActive = false;
        dragAngularVelocity = 0f;
        SetTargetSelectionIndex(itemIndex);

        float desiredRotation =
            GetDesiredRotation(itemIndex);

        targetDiskRotation +=
            Mathf.DeltaAngle(
                targetDiskRotation,
                desiredRotation
            );
    }

    private void SnapToNearestSelection()
    {
        isInertiaActive = false;
        dragAngularVelocity = 0f;

        int nearestIndex =
            FindSelectionIndex(targetDiskRotation);
        SetTargetSelectionIndex(nearestIndex);

        float desiredRotation =
            GetDesiredRotation(nearestIndex);

        targetDiskRotation +=
            Mathf.DeltaAngle(
                targetDiskRotation,
                desiredRotation
            );
    }

    private float GetDesiredRotation(int itemIndex)
    {
        if (runtimeItems == null || runtimeItems.Length == 0)
        {
            return selectionSlotAngle;
        }

        float step = 360f / runtimeItems.Length;
        return selectionSlotAngle - itemIndex * step;
    }

    private int FindSelectionIndex(float diskRotation)
    {
        if (runtimeItems == null || runtimeItems.Length == 0)
        {
            return 0;
        }

        float step = 360f / runtimeItems.Length;
        int bestIndex = 0;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < runtimeItems.Length; i++)
        {
            float itemAngle = i * step + diskRotation;
            float distance =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        itemAngle,
                        selectionSlotAngle
                    )
                );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void SetTargetSelectionIndex(int newIndex)
    {
        if (newIndex == targetSelectionIndex)
        {
            return;
        }

        targetSelectionIndex = newIndex;

        if (newIndex == currentSelectionIndex)
        {
            return;
        }

        previousSelectionIndex = currentSelectionIndex;
        currentSelectionIndex = newIndex;
        selectionBlend = 0f;

        // 选中项每前进一个道具响一声齿轮，拖动、惯性、点击共用此入口。
        PlayRotationTick();

        NotifySelectionChanged();
    }

    public Transform GetSelectedItemTransform()
    {
        if (runtimeItems == null ||
            runtimeItems.Length == 0 ||
            currentSelectionIndex < 0 ||
            currentSelectionIndex >= runtimeItems.Length)
        {
            return null;
        }

        InventoryItem entry =
            runtimeItems[currentSelectionIndex];

        return entry != null ? entry.item : null;
    }

    private void NotifySelectionChanged()
    {
        SelectionChanged?.Invoke(
            currentSelectionIndex,
            GetSelectedItemTransform()
        );
    }

    private void UpdateRotation(float deltaTime)
    {
        // 拖动时快速跟随，松开后根据惯性和吸附参数平滑停下。
        if (isInertiaActive)
        {
            targetDiskRotation +=
                dragAngularVelocity * deltaTime;

            dragAngularVelocity =
                Mathf.MoveTowards(
                    dragAngularVelocity,
                    0f,
                    inertiaDeceleration * deltaTime
                );

            UpdateTargetSelection();

            if (Mathf.Abs(dragAngularVelocity) < inertiaMinimumSpeed)
            {
                SnapToNearestSelection();
            }
        }

        float followSpeed =
            isDragging || isInertiaActive
                ? dragFollowSpeed
                : snapSpeed;

        float lerpAmount =
            1f -
            Mathf.Exp(
                -followSpeed *
                Mathf.Max(0f, deltaTime)
            );

        currentDiskRotation =
            Mathf.LerpAngle(
                currentDiskRotation,
                targetDiskRotation,
                lerpAmount
            );

        // 旋转速度越快，选中吸附越弱；接近停止时再平滑恢复到完整吸附。
        float rotationSpeed =
            isDragging || isInertiaActive
                ? Mathf.Abs(dragAngularVelocity)
                : 0f;

        float speedRatio =
            Mathf.Clamp01(
                rotationSpeed /
                Mathf.Max(0.001f, snapAttractionStartSpeed)
            );

        float targetAttraction =
            Mathf.Lerp(
                1f,
                movingSelectionAttraction,
                speedRatio
            );

        float attractionLerp =
            1f -
            Mathf.Exp(
                -attractionBlendSpeed *
                Mathf.Max(0f, deltaTime)
            );

        currentSelectionAttraction =
            Mathf.Lerp(
                currentSelectionAttraction,
                targetAttraction,
                attractionLerp
            );

        if (selectionBlend < 1f)
        {
            selectionBlend =
                Mathf.MoveTowards(
                    selectionBlend,
                    1f,
                    selectionBlendSpeed *
                    Mathf.Max(0f, deltaTime)
                );
        }
    }

    private int FindClickedItem()
    {
        // 使用屏幕空间距离寻找鼠标最接近的道具。
        if (mainCamera == null || itemStates == null)
        {
            return -1;
        }

        Vector3 mousePosition = Input.mousePosition;
        int bestIndex = -1;
        float bestDistanceSquared =
            clickSelectionRadiusPixels *
            clickSelectionRadiusPixels;

        for (int i = 0; i < itemStates.Length; i++)
        {
            ItemRuntimeState state = itemStates[i];
            if (state == null || state.Item == null)
            {
                continue;
            }

            Vector3 worldPoint =
                state.Renderer != null
                    ? state.Renderer.bounds.center
                    : state.Item.position;

            Vector3 screenPoint =
                mainCamera.WorldToScreenPoint(worldPoint);

            if (screenPoint.z <= 0f)
            {
                continue;
            }

            float dx = screenPoint.x - mousePosition.x;
            float dy = screenPoint.y - mousePosition.y;
            float distanceSquared = dx * dx + dy * dy;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void UpdateItemLayout()
    {
        // 根据当前圆环角度计算每个道具的屏幕内环形位置。
        if (itemStates == null ||
            itemStates.Length == 0 ||
            mainCamera == null)
        {
            return;
        }

        int itemCount = itemStates.Length;
        float step = 360f / itemCount;

        // 移入动画：沿摄像机屏幕正下方生成偏移，随缓出曲线衰减到 0。
        // 使用摄像机 up 的反方向而不是容器局部 Y，避免倾斜容器把
        // 偏移带进深度分量，看起来像道具由远及近。
        Vector3 introOffset = Vector3.zero;

        if (isIntroActive)
        {
            float introProgress =
                Mathf.Clamp01(
                    introElapsedTime /
                    Mathf.Max(0.0001f, introDuration)
                );

            // easeOutCubic：起步流畅、结尾平滑减速，避免急起急停的生硬感。
            float easedProgress =
                1f - Mathf.Pow(1f - introProgress, 3f);

            Vector3 worldOffset =
                -mainCamera.transform.up *
                (introDropDistanceRuntime * (1f - easedProgress));

            introOffset =
                transform.InverseTransformDirection(worldOffset);
        }

        Vector3 towardCameraLocal =
            transform.InverseTransformDirection(
                -mainCamera.transform.forward
            );

        Vector3 selectedPosition =
            towardCameraLocal * selectedBringForward;

        for (int i = 0; i < itemCount; i++)
        {
            ItemRuntimeState state = itemStates[i];
            if (state == null ||
                state.Item == null ||
                state.Entry == null)
            {
                continue;
            }

            // 如果策划或美术在运行时直接修改了道具 Transform，
            // 将这次修改作为新的显示基线，而不是下一帧覆盖它。
            if (Quaternion.Angle(
                state.Item.localRotation,
                state.LastAppliedLocalRotation
            ) > 0.01f)
            {
                state.BaseCameraRelativeRotation =
                    Quaternion.Inverse(
                        mainCamera.transform.rotation
                    ) *
                    state.Item.rotation;
            }

            if (Vector3.Distance(
                state.Item.localScale,
                state.LastAppliedLocalScale
            ) > 0.0001f)
            {
                state.DisplayLocalScale =
                    state.Item.localScale;
            }

            float itemBaseAngle = i * step;
            float displayedAngle =
                itemBaseAngle + currentDiskRotation;
            float angleRadians =
                displayedAngle * Mathf.Deg2Rad;

            Vector3 ringPosition =
                new Vector3(
                    Mathf.Cos(angleRadians) * ringRadius,
                    Mathf.Sin(angleRadians) * ringRadius,
                    0f
                );

            float selectionWeight =
                GetSelectionWeight(i);

            state.Item.localPosition =
                Vector3.Lerp(
                    ringPosition,
                    selectedPosition,
                    selectionWeight
                ) + introOffset;

            // 保持道具相对摄像机的朝向，同时允许单独配置旋转偏移。
            state.Item.rotation =
                mainCamera.transform.rotation *
                state.BaseCameraRelativeRotation *
                Quaternion.Euler(
                    state.Entry.displayRotationOffset
                );

            state.LastAppliedLocalRotation =
                state.Item.localRotation;

            float scaleMultiplier =
                state.Entry.inventoryScaleMultiplier *
                Mathf.Lerp(
                    1f,
                    selectedScaleMultiplier,
                    selectionWeight
                );

            state.Item.localScale =
                state.DisplayLocalScale *
                scaleMultiplier;

            state.LastAppliedLocalScale =
                state.Item.localScale;
        }
    }

    private float GetSelectionWeight(int itemIndex)
    {
        if (itemIndex == currentSelectionIndex &&
            itemIndex == previousSelectionIndex)
        {
            return currentSelectionAttraction;
        }

        float weight = 0f;

        if (itemIndex == currentSelectionIndex)
        {
            weight += selectionBlend;
        }

        if (itemIndex == previousSelectionIndex)
        {
            weight += 1f - selectionBlend;
        }

        return Mathf.Clamp01(weight) *
               currentSelectionAttraction;
    }
}
