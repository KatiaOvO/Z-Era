using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 矩形按钮的悬停缩放表现：鼠标移入时按钮的 Image 稍微放大、
/// 移出时恢复原状，按钮根物体的尺寸与射线范围不变。
/// 缩放目标应为按钮根物体下的 Image（或其 RectTransform），
/// 其 Pivot 需为中心，否则会表现为位移而非居中放大。
/// 可与 MainMenuButtonHoverFill 叠加挂在同一按钮上，互不干扰。
/// </summary>
public class FrameButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("引用")]

    [Tooltip("悬停时被放大的 Image（按钮根物体的子物体），Pivot 建议为中心")]
    [SerializeField]
    private Image targetImage;

    [Header("悬停表现")]

    [Tooltip("悬停时的放大倍率，1 表示不放大")]
    [SerializeField, Min(1f)]
    private float hoverScale = 1.08f;

    [Tooltip("放大/恢复的动画时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float scaleDuration = 0.12f;

    [Tooltip("缩放插值曲线：横轴为归一化时间，纵轴为缩放进度（1 = 到达目标）。缓动曲线消除线性过渡的生硬感；曲线超出 1 可做出回弹过冲")]
    [SerializeField]
    private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // 初始缩放，恢复时回到这个基准而不是固定的 1，
    // 兼容 Image 本身带非 1 缩放配置的情况
    private Vector3 baseScale = Vector3.one;

    private Coroutine scaleCoroutine;

    private void Awake()
    {
        if (targetImage != null)
        {
            baseScale = targetImage.rectTransform.localScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartScale(baseScale * hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartScale(baseScale);
    }

    // 从当前缩放开始过渡，中途移出/再进入不会跳变
    private void StartScale(Vector3 target)
    {
        StopScale();

        if (targetImage == null)
        {
            return;
        }

        scaleCoroutine = StartCoroutine(
            ScaleRoutine(target)
        );
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        RectTransform rect = targetImage.rectTransform;
        Vector3 from = rect.localScale;
        float time = 0f;

        while (time < scaleDuration)
        {
            time += Time.unscaledDeltaTime;

            // LerpUnclamped 配合曲线：允许曲线超出 0~1 时做出
            // 过冲回弹；EaseInOut 默认值则为平滑的加速-减速
            rect.localScale = Vector3.LerpUnclamped(
                from,
                target,
                scaleCurve.Evaluate(time / scaleDuration)
            );

            yield return null;
        }

        rect.localScale = target;
        scaleCoroutine = null;
    }

    private void StopScale()
    {
        if (scaleCoroutine == null)
        {
            return;
        }

        StopCoroutine(scaleCoroutine);
        scaleCoroutine = null;
    }
}
