using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 选项按钮的悬停表现：鼠标进入时白色填充从左到右铺满、
/// 文字变为悬停颜色，移出时填充瞬间消失、文字恢复常规颜色。
/// 填充动画时长在检查器中设置；音效由 DialogueUIController 播放。
/// </summary>
public class DialogueChoiceButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("引用")]

    [Tooltip("白色填充 Image（Image Type = Filled，Fill Method = Horizontal，Origin = Left）")]
    [SerializeField]
    private Image hoverFill;

    [Tooltip("按钮文字")]
    [SerializeField]
    private TMP_Text label;

    [Header("悬停表现")]

    [Tooltip("文字悬停时的颜色")]
    [SerializeField]
    private Color hoverTextColor = Color.black;

    [Tooltip("填充动画时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float fillDuration = 0.12f;

    // 常规文字颜色，生成时从预制体文字上采集。
    private Color normalTextColor = Color.white;

    private Coroutine fillCoroutine;

    // 由 DialogueUIController 在生成按钮时调用。
    public void Setup()
    {
        // HoverFill 拉伸铺满按钮根：按钮尺寸由
        // ApplyUniformChoiceWidths 按文本统一设定，
        // 白色填充始终覆盖整个按钮。
        if (hoverFill != null)
        {
            RectTransform fillRect = hoverFill.rectTransform;

            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        // 常规颜色沿用预制体文字的当前颜色。
        if (label != null)
        {
            normalTextColor = label.color;
        }

        ApplyNormalState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        DialogueRunner.Instance?.PlayChoiceHoverSound();

        ApplyTextColor(hoverTextColor);
        StartFill(1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 移出时填充瞬间消失、文字恢复常规颜色。
        ApplyNormalState();
    }

    // 选项开始溶解消失时由 DialogueUIController 调用：
    // 中断填充动画并瞬间还原，避免白色填充残留在溶解画面里。
    public void OnHide()
    {
        ApplyNormalState();
    }

    private void ApplyNormalState()
    {
        StopFill();

        ApplyTextColor(normalTextColor);

        if (hoverFill != null)
        {
            hoverFill.fillAmount = 0f;
        }
    }

    private void ApplyTextColor(Color color)
    {
        if (label != null)
        {
            label.color = color;
        }
    }

    private void StartFill(float target)
    {
        StopFill();

        if (hoverFill == null)
        {
            return;
        }

        fillCoroutine = StartCoroutine(
            FillRoutine(target)
        );
    }

    private IEnumerator FillRoutine(float target)
    {
        float from = hoverFill.fillAmount;
        float time = 0f;

        while (time < fillDuration)
        {
            time += Time.unscaledDeltaTime;

            hoverFill.fillAmount = Mathf.Lerp(
                from,
                target,
                time / fillDuration
            );

            yield return null;
        }

        hoverFill.fillAmount = target;
    }

    private void StopFill()
    {
        if (fillCoroutine == null)
        {
            return;
        }

        StopCoroutine(fillCoroutine);
        fillCoroutine = null;
    }
}
