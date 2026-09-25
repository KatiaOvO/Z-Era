using System.Collections.Generic;
using UnityEngine;

public class PickableItem : MonoBehaviour
{
    private static readonly List<PickableItem>
        registeredItems = new List<PickableItem>();

    // 当前场景中所有已激活的可拾取道具，供控制器遍历，避免全场扫描。
    public static IReadOnlyList<PickableItem> RegisteredItems
    {
        get { return registeredItems; }
    }

    [Tooltip("覆盖全局的单件交互距离（米），0 表示使用 PickupController 的默认交互距离")]
    public float interactionDistanceOverride;

    private void OnEnable()
    {
        if (!registeredItems.Contains(this))
        {
            registeredItems.Add(this);
        }
    }

    private void OnDisable()
    {
        registeredItems.Remove(this);
    }

    // 查询道具当前是否处于注册状态（已激活且可拾取）。
    public static bool IsRegistered(PickableItem item)
    {
        return item != null && registeredItems.Contains(item);
    }

    // 从射线命中的碰撞体向上查找所属的可拾取道具。
    public static PickableItem FromCollider(Collider collider)
    {
        return collider != null
            ? collider.GetComponentInParent<PickableItem>()
            : null;
    }
}
