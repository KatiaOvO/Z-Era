using UnityEngine;

public class InventoryItemInfo : MonoBehaviour
{
    [Tooltip("道具显示名称，留空则不单独使用")]
    public string displayName;

    [Tooltip("道具描述文本，选中该道具时展示；留空则展示占位文本")]
    [TextArea]
    public string description;
}
