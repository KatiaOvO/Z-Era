using System.Collections;
using Migration.UI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 主菜单面板关闭器：点击按钮直接关闭整个面板
/// （禁用 Panel Root），点击音效由 Hover Fill 的临时
/// 音频物体播放，不随面板禁用中断，因此无需等待即可秒关。
/// 额外负责面板善后：遮罩溶解完成后禁用遮罩物体，
/// 免去它溶解后仍以透明状态拦截射线和渲染的开销；
/// 重新启用由 MainMenuPanelOpener 在下次打开时负责。
/// </summary>
public class MainMenuPanelCloser : MonoBehaviour, IPointerClickHandler
{
    [Header("引用")]

    [Tooltip("要关闭的面板根物体（背景 + UI 组件 + 遮罩）")]
    [SerializeField]
    private GameObject panelRoot;

    [Tooltip("面板最上层遮罩 Image 上的 UIDissolveImage 组件")]
    [SerializeField]
    private UIDissolveImage maskDissolve;

    private Coroutine disableMaskCoroutine;

    private void OnEnable()
    {
        // 面板打开时本组件随之启用：开始监听本轮溶解
        disableMaskCoroutine = StartCoroutine(DisableMaskRoutine());
    }

    private void OnDisable()
    {
        if (disableMaskCoroutine == null)
        {
            return;
        }

        StopCoroutine(disableMaskCoroutine);
        disableMaskCoroutine = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (panelRoot == null)
        {
            return;
        }

        // 点击音效由独立临时物体播放，禁用面板不会截断它
        panelRoot.SetActive(false);
    }

    private IEnumerator DisableMaskRoutine()
    {
        // 先等打开流程把遮罩瞬时拉回完全显示（Location = 0）。
        // 上一轮遗留的 Location = 1 会让首次判断误以为溶解
        // 已完成，所以不能直接跳过这一步
        while (!maskDissolve.isShowComplete)
        {
            yield return null;
        }

        // 再等溶解消失动画播完（含 Opener 的溶解延迟窗口）
        while (!maskDissolve.isHideComplete)
        {
            yield return null;
        }

        maskDissolve.gameObject.SetActive(false);
        disableMaskCoroutine = null;
    }
}
