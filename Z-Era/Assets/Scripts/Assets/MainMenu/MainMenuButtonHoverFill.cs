using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 主菜单按钮的悬停表现：鼠标进入时白色填充从左到右铺满、
/// Hover Fill 透明度同步淡入、文字变为悬停颜色，并播放一段
/// 悬停音效；移出时填充保持铺满状态（中断的填充会继续跑完），
/// 透明度淡出，结束后填充进度复位、文字恢复常规颜色。
/// 各动画时长与音效在检查器中设置。
/// 由 DialogueChoiceButton 改造而来，去除了对话系统耦合。
/// </summary>
public class MainMenuButtonHoverFill : MonoBehaviour,
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

    [Tooltip("填充动画时长（秒）：Hover Fill 从当前进度铺满到 1")]
    [SerializeField, Min(0.01f)]
    private float fillDuration = 0.12f;

    [Tooltip("透明度淡入/淡出时长（秒）：悬停时 0→1，移开时 1→0")]
    [SerializeField, Min(0.01f)]
    private float alphaFadeDuration = 0.12f;

    [Header("悬停音效")]

    [Tooltip("鼠标进入按钮时播放的音效，留空则不播放")]
    [SerializeField]
    private AudioClip hoverSound;

    [Tooltip("悬停音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float hoverSoundVolume = 1f;

    [Tooltip("悬停音效音调随机浮动幅度（0~0.5），连续扫过多个按钮时更自然")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float hoverSoundPitchVariation = 0f;

    // 常规文字颜色，初始化时从按钮文字上采集。
    private Color normalTextColor = Color.white;

    private Coroutine fillCoroutine;
    private Coroutine alphaCoroutine;

    // 透明度淡入未完成时鼠标已移出：待淡入到 1 后再开始淡出
    private bool pendingFadeOut;

    // 专属音源：不复用其他系统的 AudioSource，避免被别处的
    // Stop() 中途掐断。
    private AudioSource hoverAudioSource;

    private void Awake()
    {
        // HoverFill 拉伸铺满按钮根：白色填充始终覆盖整个按钮。
        if (hoverFill != null)
        {
            RectTransform fillRect = hoverFill.rectTransform;

            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // 初始状态：进度与透明度都归零，悬停后才开始淡入。
            SetFillAlpha(0f);
            hoverFill.fillAmount = 0f;
        }

        // 常规颜色沿用按钮文字的当前颜色。
        if (label != null)
        {
            normalTextColor = label.color;
        }

        if (hoverSound == null)
        {
            return;
        }

        hoverAudioSource = gameObject.AddComponent<AudioSource>();
        hoverAudioSource.playOnAwake = false;
        hoverAudioSource.spatialBlend = 0f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayHoverSound();

        ApplyTextColor(hoverTextColor);

        // 新一轮悬停：清除上一轮的延迟淡出请求
        pendingFadeOut = false;

        // 填充始终铺满到 1；透明度同步淡入
        StartFill(1f);
        StartAlphaFade(1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 文字恢复常规颜色；填充协程不打断——移开时无论进度
        // 是多少都会继续铺满到 1
        ApplyTextColor(normalTextColor);

        if (alphaCoroutine != null)
        {
            // 透明度还在淡入：先等淡入到 1，再由淡入完成时
            // 自动开始淡出
            pendingFadeOut = true;
        }
        else
        {
            // 透明度已在 1（淡入已完成）：直接开始淡出
            StartAlphaFade(0f);
        }
    }

    private void PlayHoverSound()
    {
        if (hoverSound == null || hoverAudioSource == null)
        {
            return;
        }

        // 每次轻微随机音调，连续悬停多个按钮时不像复读
        hoverAudioSource.pitch =
            1f + Random.Range(
                -hoverSoundPitchVariation,
                hoverSoundPitchVariation);

        hoverAudioSource.PlayOneShot(
            hoverSound,
            hoverSoundVolume);
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
        fillCoroutine = null;
    }

    // 透明度淡入/淡出：淡入（target = 1）完成时若鼠标已移出
    // （pendingFadeOut），立即接续淡出；淡出（target = 0）结束
    // 后把填充进度复位为 0——此时 Fill 已完全透明，复位不可见，
    // 下次悬停填充才会重新从左往右扫过
    private void StartAlphaFade(float target)
    {
        StopAlphaFade();

        if (hoverFill == null)
        {
            return;
        }

        alphaCoroutine = StartCoroutine(
            AlphaFadeRoutine(target)
        );
    }

    private IEnumerator AlphaFadeRoutine(float target)
    {
        float from = hoverFill.color.a;
        float time = 0f;

        while (time < alphaFadeDuration)
        {
            time += Time.unscaledDeltaTime;

            SetFillAlpha(
                Mathf.Lerp(from, target, time / alphaFadeDuration));

            yield return null;
        }

        SetFillAlpha(target);

        bool wasFadeIn = target > 0f;
        alphaCoroutine = null;

        if (wasFadeIn)
        {
            // 淡入完成：若鼠标在淡入期间就已移出，现在开始淡出
            if (pendingFadeOut)
            {
                pendingFadeOut = false;
                StartAlphaFade(0f);
            }
        }
        else
        {
            hoverFill.fillAmount = 0f;
        }
    }

    private void SetFillAlpha(float alpha)
    {
        Color color = hoverFill.color;
        color.a = alpha;
        hoverFill.color = color;
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

    private void StopAlphaFade()
    {
        if (alphaCoroutine == null)
        {
            return;
        }

        StopCoroutine(alphaCoroutine);
        alphaCoroutine = null;
    }
}
