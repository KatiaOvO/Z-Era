using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PickupController : MonoBehaviour
{
    [Header("交互设置")]

    [Tooltip("拾取按键")]
    public KeyCode pickupKey = KeyCode.E;

    [Tooltip("开始高亮的距离（米）")]
    [Min(0.1f)]
    public float highlightDistance = 3f;

    [Tooltip("默认交互距离（米），道具可用 PickableItem 的 override 单独覆盖")]
    [Min(0.1f)]
    public float interactDistance = 1.5f;

    [Header("描边外观")]

    [Tooltip("高亮描边的线宽（像素），与观察距离无关")]
    [Range(0f, 20f)]
    public float outlineWidth = 6f;

    [Tooltip("呼吸脉动频率（每秒循环次数），描边透明度在 0 与颜色 Alpha 之间往复")]
    [Min(0.1f)]
    public float pulseSpeed = 1.5f;

    [Header("拾取音效")]

    [Tooltip("拾取成功时播放的音效，留空则不播放")]
    [SerializeField]
    private AudioClip pickupSound;

    [Tooltip("拾取音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float pickupSoundVolume = 1f;

    [Header("引用")]

    [Tooltip("交互提示文本，留空则不显示提示")]
    [SerializeField]
    private TMP_Text promptText;

    [Tooltip("背包圆盘控制器，其所在物体即 InventoryContainer；留空则自动查找")]
    [SerializeField]
    private InventoryRadialView radialView;

    [Tooltip("玩家武器所在根物体，弹药箱/弹匣在其子物体（含未激活）中查找 WeaponController 补充弹药；留空则用自身所在层级根节点")]
    [SerializeField]
    private Transform weaponRoot;

    private Camera cachedCamera;
    private AudioSource pickupAudioSource;

    // 按颜色缓存的描边材质：不同分类的物品可同时高亮，
    // 各自使用独立材质，呼吸脉动时统一改写透明度。
    private readonly Dictionary<Color, Material> outlineMaterials =
        new Dictionary<Color, Material>();

    private void Awake()
    {
        if (pickupSound == null)
        {
            return;
        }

        // 必须用专属 AudioSource，不能复用玩家脚步声的源：
        // PlayerController 在停止移动时会 Stop() 该源，
        // 会把刚通过 PlayOneShot 播出的拾取音效一起掐断。
        pickupAudioSource = gameObject.AddComponent<AudioSource>();
        pickupAudioSource.playOnAwake = false;
        pickupAudioSource.spatialBlend = 0f;
    }

    // 当前处于高亮状态的可拾取道具集合。
    private readonly HashSet<PickableItem> highlightedItems =
        new HashSet<PickableItem>();
    private readonly Dictionary<PickableItem, Renderer[]> highlightStates =
        new Dictionary<PickableItem, Renderer[]>();
    private readonly Dictionary<Renderer, Material[]> originalMaterials =
        new Dictionary<Renderer, Material[]>();

    private void Update()
    {
        if (radialView == null)
        {
            // InventoryContainer 初始为禁用状态，必须包含未激活物体才能找到。
            radialView =
                FindObjectOfType<InventoryRadialView>(true);

            if (radialView == null)
            {
                return;
            }
        }

        // 背包打开或相机不可用时不做拾取检测。
        if (radialView.gameObject.activeInHierarchy)
        {
            ClearHighlight();
            UpdatePrompt(false, null);
            return;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;

            if (cachedCamera == null)
            {
                return;
            }
        }

        UpdateHighlights();

        // 有道具处于高亮状态时，各分类描边材质的透明度按呼吸频率
        // 在 0 与各自颜色 Alpha 之间往复。
        if (highlightedItems.Count > 0)
        {
            float pulse =
                Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;

            foreach (
                KeyValuePair<Color, Material> outline in
                    outlineMaterials
            )
            {
                Color pulsingColor = outline.Key;
                pulsingColor.a *= pulse;

                outline.Value.SetColor("_OutlineColor", pulsingColor);
                outline.Value.SetFloat("_OutlineWidth", outlineWidth);
            }
        }

        if (CanPickItem(out PickableItem pickable))
        {
            UpdatePrompt(true, pickable);

            if (Input.GetKeyDown(pickupKey))
            {
                PickupItem(pickable);
            }
        }
        else
        {
            UpdatePrompt(false, null);
        }
    }

    private void UpdateHighlights()
    {
        Vector3 origin = cachedCamera.transform.position;
        float maxSqrDistance = highlightDistance * highlightDistance;

        // 给新进入范围的道具添加描边。
        foreach (
            PickableItem item in PickableItem.RegisteredItems
        )
        {
            if (item == null || highlightedItems.Contains(item))
            {
                continue;
            }

            float sqrDistance =
                (item.transform.position - origin).sqrMagnitude;

            if (sqrDistance <= maxSqrDistance)
            {
                ApplyOutline(item);
                highlightedItems.Add(item);
            }
        }

        // 撤销离开范围或已失效道具的描边。
        List<PickableItem> expiredItems = null;

        foreach (
            PickableItem item in highlightedItems
        )
        {
            if (item == null ||
                !PickableItem.IsRegistered(item) ||
                (item.transform.position - origin).sqrMagnitude >
                    maxSqrDistance)
            {
                (expiredItems ??= new List<PickableItem>())
                    .Add(item);
            }
        }

        if (expiredItems != null)
        {
            foreach (
                PickableItem item in expiredItems
            )
            {
                RemoveOutline(item);
            }
        }
    }

    private bool CanPickItem(
        out PickableItem pickable
    )
    {
        pickable = null;

        // 鼠标锁定状态下以屏幕中心为准（FPS 准星）。
        Ray ray = cachedCamera.ScreenPointToRay(
            new Vector3(
                Screen.width * 0.5f,
                Screen.height * 0.5f,
                0f
            )
        );

        // 射线最远打到高亮距离，命中后再按道具的交互距离判定。
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                highlightDistance
            ))
        {
            return false;
        }

        pickable = PickableItem.FromCollider(hit.collider);

        // GetComponentInParent 只看 GameObject 的激活状态，
        // 组件被单独禁用时仍会返回，必须再查注册表：
        // 禁用的组件不在注册表中，从而禁止拾取（与高亮行为一致）。
        if (pickable == null || !PickableItem.IsRegistered(pickable))
        {
            return false;
        }

        float interactDistance =
            pickable.interactionDistanceOverride > 0f
                ? pickable.interactionDistanceOverride
                : this.interactDistance;

        return hit.distance <= interactDistance;
    }

    private void PickupItem(PickableItem item)
    {
        // 先还原描边材质，避免描边跟随道具进入背包。
        RemoveOutline(item);

        // 拾取副作用（设置对话标记、上报任务进度等）在道具
        // 入包/销毁/禁用前触发，监听方仍可访问道具本体。
        item.NotifyPickedUp();

        switch (item.category)
        {
            case PickupCategory.AmmoBox:
                // 弹药箱：补满身上所有武器弹药，本体留在原地
                // 不进背包也不消失；指定次数型耗尽后由
                // PickableItem 自行禁用退出注册表。
                RefillAllWeapons();
                item.ConsumeInteraction();
                break;

            case PickupCategory.Magazine:
                // 弹匣：按枪支名称索引补满对应武器弹药，
                // 不进背包，拾取后本体消失。
                RefillWeaponForMagazine(item);
                Destroy(item.gameObject);
                break;

            default:
                // 道具/武器/特殊收集品：移入背包，可打开库存查看。
                MoveItemToInventory(item);
                break;
        }

        if (pickupSound != null && pickupAudioSource != null)
        {
            pickupAudioSource.PlayOneShot(
                pickupSound,
                pickupSoundVolume
            );
        }
    }

    // 道具/武器/特殊收集品共用的入包流程。
    private void MoveItemToInventory(PickableItem item)
    {
        Transform container =
            radialView != null ? radialView.transform : null;

        if (container == null)
        {
            Debug.LogError(
                "PickupController has no InventoryContainer!",
                this
            );

            return;
        }

        item.transform.SetParent(container, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation =
            Quaternion.Euler(0f, -90f, -90f);

        // 道具入包后停用并关闭物理，由圆盘视图在展示时统一接管。
        item.gameObject.SetActive(false);

        foreach (
            Rigidbody body in
                item.GetComponentsInChildren<Rigidbody>(true)
        )
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        foreach (
            Collider collider in
                item.GetComponentsInChildren<Collider>(true)
        )
        {
            collider.enabled = false;
        }
    }

    // 玩家身上的全部武器（含未激活的备用武器）。
    private IEnumerable<WeaponController> GetPlayerWeapons()
    {
        Transform root =
            weaponRoot != null ? weaponRoot : transform.root;

        return root.GetComponentsInChildren<WeaponController>(
            true
        );
    }

    // 弹药箱：补满所有武器的弹匣与备弹。
    private void RefillAllWeapons()
    {
        foreach (
            WeaponController weapon in GetPlayerWeapons()
        )
        {
            weapon.RefillAmmo();
        }
    }

    // 弹匣：按枪支名称索引补满对应武器；先精确匹配物体名，
    // 未命中再退化为包含匹配，仍找不到则告警丢弃。
    private void RefillWeaponForMagazine(PickableItem item)
    {
        string weaponName = item.ResolveMagazineWeaponName();

        foreach (
            WeaponController weapon in GetPlayerWeapons()
        )
        {
            if (weapon.gameObject.name == weaponName)
            {
                weapon.RefillAmmo();
                return;
            }
        }

        foreach (
            WeaponController weapon in GetPlayerWeapons()
        )
        {
            if (weapon.gameObject.name.Contains(weaponName))
            {
                weapon.RefillAmmo();
                return;
            }
        }

        Debug.LogWarning(
            "PickupController：弹匣未在玩家身上找到对应武器 " +
                weaponName + "，弹药未补充。",
            item
        );
    }

    private void ClearHighlight()
    {
        foreach (
            PickableItem item in
                new List<PickableItem>(highlightedItems)
        )
        {
            RemoveOutline(item);
        }
    }

    private void ApplyOutline(PickableItem item)
    {
        Material outlineMaterial =
            GetOutlineMaterial(item.OutlineColor);

        if (outlineMaterial == null)
        {
            return;
        }

        Renderer[] renderers =
            item.GetComponentsInChildren<Renderer>(true);

        var keptRenderers = new List<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            // 粒子等渲染器不支持附加描边材质，跳过。
            if (!(renderer is MeshRenderer) &&
                !(renderer is SkinnedMeshRenderer))
            {
                continue;
            }

            Material[] itemMaterials =
                renderer.sharedMaterials;

            var materialsWithOutline =
                new Material[itemMaterials.Length + 1];

            for (int i = 0; i < itemMaterials.Length; i++)
            {
                materialsWithOutline[i] = itemMaterials[i];
            }

            materialsWithOutline[itemMaterials.Length] =
                outlineMaterial;

            renderer.sharedMaterials = materialsWithOutline;

            originalMaterials[renderer] = itemMaterials;
            keptRenderers.Add(renderer);
        }

        highlightStates[item] = keptRenderers.ToArray();
    }

    // 取（或创建）指定颜色的描边材质：同色分类共享一个材质，
    // 呼吸脉动时只需遍历少量材质改写透明度。
    private Material GetOutlineMaterial(Color color)
    {
        if (outlineMaterials.TryGetValue(
                color,
                out Material cached
            ))
        {
            return cached;
        }

        Shader outlineShader =
            Shader.Find("Custom/Item Outline");

        if (outlineShader == null)
        {
            Debug.LogError(
                "PickupController could not find the Item Outline shader.",
                this
            );

            return null;
        }

        Material material = new Material(outlineShader);
        material.renderQueue = ItemOutlineQueue;
        material.SetColor("_OutlineColor", color);
        material.SetFloat("_OutlineWidth", outlineWidth);

        outlineMaterials[color] = material;

        return material;
    }

    private void RemoveOutline(PickableItem item)
    {
        highlightedItems.Remove(item);

        if (!highlightStates.TryGetValue(item, out Renderer[] renderers))
        {
            return;
        }

        highlightStates.Remove(item);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (originalMaterials.TryGetValue(
                    renderer,
                    out Material[] original
                ))
            {
                renderer.sharedMaterials = original;
                originalMaterials.Remove(renderer);
            }
        }
    }

    private void UpdatePrompt(
        bool canPick,
        PickableItem item
    )
    {
        if (promptText == null)
        {
            return;
        }

        if (!canPick)
        {
            promptText.text = string.Empty;
            return;
        }

        InventoryItemInfo info =
            item != null
                ? item.GetComponent<InventoryItemInfo>()
                : null;

        string itemName =
            info != null ? info.displayName : null;

        // 提示文案按分类区分：弹药箱与弹匣是补给交互而非入包。
        switch (item.category)
        {
            case PickupCategory.AmmoBox:
                promptText.text = item.unlimitedInteractions
                    ? "按 E 补充所有武器弹药"
                    : "按 E 补充所有武器弹药（剩余 " +
                        item.RemainingInteractions + " 次）";
                break;

            case PickupCategory.Magazine:
                promptText.text =
                    "按 E 拾取弹匣：补充 " +
                    item.ResolveMagazineWeaponName() +
                    " 的弹药";
                break;

            default:
                promptText.text =
                    string.IsNullOrWhiteSpace(itemName)
                        ? "按 E 拾取"
                        : "按 E 拾取：" + itemName;
                break;
        }
    }

    // 与 Shader 中 Geometry+1 一致，描边始终紧跟在道具表面之后绘制。
    private const int ItemOutlineQueue = 2001;
}
