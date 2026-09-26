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

    [Tooltip("高亮描边的颜色（Alpha 为呼吸最满时的不透明度）")]
    public Color outlineColor = new Color(1f, 0.8f, 0.2f, 1f);

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

    private Camera cachedCamera;
    private Material outlineMaterial;
    private AudioSource pickupAudioSource;

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

        // 有道具处于高亮状态时，描边透明度按呼吸频率在 0 与颜色 Alpha 之间往复。
        if (highlightedItems.Count > 0)
        {
            float pulse =
                Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;

            Color pulsingColor = outlineColor;
            pulsingColor.a = outlineColor.a * pulse;

            outlineMaterial.SetColor("_OutlineColor", pulsingColor);

            outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
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

        if (pickable == null)
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

        // 先还原描边材质，避免描边跟随道具进入背包。
        RemoveOutline(item);

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

        if (pickupSound != null && pickupAudioSource != null)
        {
            pickupAudioSource.PlayOneShot(
                pickupSound,
                pickupSoundVolume
            );
        }
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
        if (outlineMaterial == null)
        {
            Shader outlineShader =
                Shader.Find("Custom/Item Outline");

            if (outlineShader == null)
            {
                Debug.LogError(
                    "PickupController could not find the Item Outline shader.",
                    this
                );

                return;
            }

            outlineMaterial = new Material(outlineShader);
            outlineMaterial.renderQueue = ItemOutlineQueue;
        }

        outlineMaterial.SetColor("_OutlineColor", outlineColor);
        outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);

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

        promptText.text =
            string.IsNullOrWhiteSpace(itemName)
                ? "按 E 拾取"
                : "按 E 拾取：" + itemName;
    }

    // 与 Shader 中 Geometry+1 一致，描边始终紧跟在道具表面之后绘制。
    private const int ItemOutlineQueue = 2001;
}
