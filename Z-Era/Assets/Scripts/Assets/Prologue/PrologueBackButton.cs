using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 序章“返回主菜单”入口按钮：挂在 BackButton 上，点击后启用
/// BackToMainMenu 确认框（确认框的溶解出场与 Yes/No 按钮行为
/// 由 PrologueBackToMainMenu 处理）。
/// 引用留空时在场景中自动查找（含未启用的确认框物体）。
/// </summary>
public class PrologueBackButton : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("BackToMainMenu 确认框（其上的 PrologueBackToMainMenu 组件），留空则自动在场景中查找")]
    [SerializeField]
    private PrologueBackToMainMenu backToMainMenu;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (backToMainMenu == null)
        {
            // 确认框初始未启用，普通查找找不到，需带 includeInactive
            backToMainMenu =
                FindObjectOfType<PrologueBackToMainMenu>(true);
        }

        if (backToMainMenu == null)
        {
            Debug.LogWarning(
                "PrologueBackButton：场景中未找到 PrologueBackToMainMenu，" +
                    "无法打开返回主菜单确认框。",
                this
            );
            return;
        }

        backToMainMenu.gameObject.SetActive(true);
    }
}
