using TMPro;
using UnityEngine;

public class InventoryItemDescriptionView : MonoBehaviour
{
    [Tooltip("展示描述文本的 TextMeshPro 组件")]
    [SerializeField]
    private TMP_Text descriptionText;

    [Tooltip("道具没有 InventoryItemInfo 或描述为空时显示的占位文本")]
    [SerializeField]
    private string fallbackText = "……";

    [Tooltip("背包圆盘控制器，留空则自动查找")]
    [SerializeField]
    private InventoryRadialView radialView;

    private void OnEnable()
    {
        if (radialView == null)
        {
            radialView =
                FindObjectOfType<InventoryRadialView>();
        }

        if (radialView != null)
        {
            radialView.SelectionChanged +=
                HandleSelectionChanged;
        }

        // 订阅之前道具可能已经处于选中状态，直接刷新一次。
        RefreshText();
    }

    private void OnDisable()
    {
        if (radialView != null)
        {
            radialView.SelectionChanged -=
                HandleSelectionChanged;
        }
    }

    private void HandleSelectionChanged(
        int selectionIndex,
        Transform selectedItem
    )
    {
        RefreshText();
    }

    private void RefreshText()
    {
        if (descriptionText == null ||
            radialView == null)
        {
            return;
        }

        Transform selectedItem =
            radialView.GetSelectedItemTransform();

        InventoryItemInfo info =
            selectedItem != null
                ? selectedItem.GetComponent<InventoryItemInfo>()
                : null;

        // 名称作为物品名居中显示在第一行，描述左对齐显示在其下方；
        // 只有一方存在时单独显示，两者都没有时使用占位文本。
        string itemName =
            info != null ? info.displayName : null;

        string itemDescription =
            info != null ? info.description : null;

        bool hasName =
            !string.IsNullOrWhiteSpace(itemName);

        bool hasDescription =
            !string.IsNullOrWhiteSpace(itemDescription);

        if (hasName && hasDescription)
        {
            descriptionText.text =
                "<align=center>" + itemName +
                "\n<align=left>" + itemDescription;
        }
        else if (hasName)
        {
            descriptionText.text =
                "<align=center>" + itemName;
        }
        else if (hasDescription)
        {
            descriptionText.text =
                "<align=left>" + itemDescription;
        }
        else
        {
            descriptionText.text = fallbackText;
        }
    }
}
