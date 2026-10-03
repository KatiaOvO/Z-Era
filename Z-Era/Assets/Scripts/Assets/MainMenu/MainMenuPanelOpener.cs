using System.Collections;
using Migration.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 主菜单面板开启器：点击按钮后激活整个面板，
/// 随后把最上层的遮罩 Image 溶解掉，露出面板内容。
/// 面板层级结构（从下到上）：背景 Image → 中间 UI 组件 → 遮罩 Image，
/// 遮罩 Image 上需挂 UIDissolveImage 并指定溶解材质。
/// </summary>
public class MainMenuPanelOpener : MonoBehaviour, IPointerClickHandler
{
    [Header("引用")]

    [Tooltip("整个面板的根物体（背景 + UI 组件 + 遮罩），点击前处于禁用状态")]
    [SerializeField]
    private GameObject panelRoot;

    [Tooltip("面板最上层的遮罩 Image 上的 UIDissolveImage 组件")]
    [SerializeField]
    private UIDissolveImage maskDissolve;

    [Header("表现")]

    [Tooltip("面板出现后等待多久再开始溶解遮罩（秒），0 表示立即开始")]
    [SerializeField, Min(0f)]
    private float dissolveDelay = 0f;

    [Tooltip("遮罩溶解消失的动画时长（秒），会覆盖遮罩上 UIDissolveImage 的 Duration；0 表示瞬间消失")]
    [SerializeField, Min(0f)]
    private float dissolveDuration = 0.45f;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (panelRoot == null || maskDissolve == null)
        {
            return;
        }

        // 面板已打开：忽略重复点击
        if (panelRoot.activeSelf)
        {
            return;
        }

        // 结束上一轮还在等待的溶解流程，避免旧协程干扰
        StopAllCoroutines();

        // 整个面板出现；出现的瞬间被遮罩完全盖住，
        // 中间 UI 的直接显示过程不会被看见
        panelRoot.SetActive(true);

        // 上次点击关闭的按钮没走完悬停淡出流程就被整体禁用，
        // 会把填充铺满的悬停状态带进这一轮：打开时把面板内
        // 所有按钮的悬停表现统一复位
        foreach (MainMenuButtonHoverFill fill in
            panelRoot.GetComponentsInChildren<MainMenuButtonHoverFill>(true))
        {
            fill.ResetVisualState();
        }

        // 上一轮溶解完成后 MainMenuPanelCloser 会禁用遮罩物体，
        // 打开时先启用回来，再瞬时拉回完全显示（Location = 0），
        // 随后才开始溶解消失
        maskDissolve.gameObject.SetActive(true);
        maskDissolve.SetVisible(true, true);

        StartCoroutine(DissolveMaskRoutine());
    }

    // MaskBgImage 的 Raycast Target 同步：
    // 溶解中/完全显示时勾选（遮罩照常挡住射线），溶解完毕
    // （完全消失）后取消勾选，不再挡住面板内容的点击；
    // 再次打开面板时 SetVisible(true, true) 瞬时显示，
    // 勾选会自动恢复，无需手动切换
    private void Update()
    {
        if (maskDissolve == null || maskDissolve.graphic == null)
        {
            return;
        }

        bool shouldBlockRaycast = !maskDissolve.isHideComplete;

        if (maskDissolve.graphic.raycastTarget != shouldBlockRaycast)
        {
            maskDissolve.graphic.raycastTarget = shouldBlockRaycast;
        }
    }

    private IEnumerator DissolveMaskRoutine()
    {
        if (dissolveDelay > 0f)
        {
            // 与 UIDissolveImage 默认的未缩放时间保持一致，
            // 暂停/时间缩放不影响面板流程
            yield return new WaitForSecondsRealtime(dissolveDelay);
        }

        // 用面板脚本上的时长覆盖遮罩组件自己的 Duration，
        // 溶解节奏统一由本脚本控制
        maskDissolve.duration = dissolveDuration;

        maskDissolve.Hide();
    }
}
