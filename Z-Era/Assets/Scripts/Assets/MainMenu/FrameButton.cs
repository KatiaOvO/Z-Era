using System.Collections;
using Migration.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 矩形按钮的悬停缩放表现：鼠标移入时按钮的 Image 稍微放大、
/// 移出时恢复原状，按钮根物体的尺寸与射线范围不变。
/// 缩放目标应为按钮根物体下的 Image（或其 RectTransform），
/// 其 Pivot 需为中心，否则会表现为位移而非居中放大。
/// 可与 MainMenuButtonHoverFill 叠加挂在同一按钮上，互不干扰。
///
/// 锁面模式：勾选"锁定"后本按钮全程不跳转，作为盖在跳转按钮
/// 上方的锁面使用——解锁标记（DialogueFlags）未设置时点击显示
/// 提示文字，已设置时点击触发 UI 溶解并在溶解完成后禁用自身，
/// 露出下方的跳转按钮；已溶解的锁面在面板重新打开时保持消失。
/// </summary>
public class FrameButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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

    [Header("悬停透明度")]

    [Tooltip("按钮图片的初始透明度，鼠标移出后恢复到该值")]
    [SerializeField, Range(0f, 1f)]
    private float normalAlpha = 0.5f;

    [Tooltip("鼠标移入时按钮图片的透明度")]
    [SerializeField, Range(0f, 1f)]
    private float hoverAlpha = 1f;

    [Header("悬停音效")]

    [Tooltip("鼠标进入按钮时播放的音效")]
    [SerializeField]
    private AudioClip hoverClip;

    [Tooltip("悬停音效的音量")]
    [SerializeField, Range(0f, 1f)]
    private float hoverVolume = 1f;

    // 初始缩放，恢复时回到这个基准而不是固定的 1，
    // 兼容 Image 本身带非 1 缩放配置的情况
    [Header("点击音效")]

    [Tooltip("点击按钮时播放的音效，留空则不播放")]
    [SerializeField]
    private AudioClip clickClip;

    [Tooltip("点击音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float clickSoundVolume = 1f;

    [Header("场景跳转")]

    [Tooltip("跳转的目标场景名，场景需加入 Build Settings；" +
        "留空则点击后不跳转。跳转的溶解与加载进度由场景中的 " +
        "SceneTransitionController 全局服务负责")]
    [SerializeField]
    private string sceneName;

    [Header("锁定（勾选后本按钮成为锁面，全程不跳转）")]

    [Tooltip("勾选后本按钮全程不跳转：解锁标记未设置时点击显示" +
        "提示文字，已设置时点击触发 UI 溶解并禁用自身")]
    [SerializeField]
    private bool lockEnabled;

    [Tooltip("解锁判定用的全局对话标记（DialogueFlags）；" +
        "已设置时点击溶解消失，未设置时点击显示提示文字")]
    [SerializeField]
    private string unlockFlagName;

    [Tooltip("锁面按钮上挂的溶解组件，留空则自动取同物体或子物体上的")]
    [SerializeField]
    private UIDissolveImage dissolve;

    [Tooltip("锁定提示文本（场景中单独创建的 TMP 物体），留空则点击无提示")]
    [SerializeField]
    private TMP_Text hintText;

    [Tooltip("提示文字内容，非空时点击会把内容写入提示文本，" +
        "因此多个锁面按钮可以共用一个文本物体；留空则保留文本现有内容")]
    [SerializeField]
    private string hintMessage;

    [Tooltip("提示文字淡入时长（秒），从当前透明度升到 1")]
    [SerializeField, Min(0f)]
    private float hintFadeInDuration = 0.3f;

    [Tooltip("提示文字淡出前的停留时长（秒），期间再次点击会重置计时")]
    [SerializeField, Min(0f)]
    private float hintHoldDuration = 1.5f;

    [Tooltip("提示文字淡出时长（秒），从当前透明度降到 0")]
    [SerializeField, Min(0f)]
    private float hintFadeOutDuration = 0.5f;

    private Vector3 baseScale = Vector3.one;

    private Coroutine scaleCoroutine;

    private Coroutine hintCoroutine;

    // 溶解已触发：此后点击全部忽略，直到溶解完成禁用自身
    private bool dissolving;

    // 本次主菜单停留期间已通过点击溶解过：面板重开时保持消失。
    // 回主菜单（重进场景）后自动复位，锁面重新出现、可再次点击溶解
    private bool dissolved;

    private AudioSource audioSource;

    private void Awake()
    {
        if (targetImage != null)
        {
            baseScale = targetImage.rectTransform.localScale;

            // 初始透明度：移入前按钮图片半透明
            SetAlpha(normalAlpha);
        }

        // 复用物体上已有的 AudioSource，没有则自动补一个，
        // playOnAwake 关掉避免物体激活时误播
        if (!TryGetComponent(out audioSource))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;

        // 世界暂停（场景过渡/设置面板的 AudioListener.pause）期间
        // UI 反馈音效照常播放，不随世界一起静音
        audioSource.ignoreListenerPause = true;

        // 提示文本若盖在锁面按钮上，不能拦截射线吃掉点击
        if (hintText != null)
        {
            hintText.raycastTarget = false;
        }
    }

    private void OnEnable()
    {
        // 已点击溶解过的锁面在面板重新打开时不复活：当帧直接禁用，
        // 发生在渲染前，不会闪一帧，也不重播溶解动画。
        // 注意不查 flag：flag 为 true 但还没点击溶解时，
        // 锁面要正常出现，等玩家点击再溶解
        if (dissolved)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // 面板整体关闭时收尾：停掉淡入淡出并清透明度，
        // 避免重开面板时残留半透明旧提示
        if (hintCoroutine != null)
        {
            StopCoroutine(hintCoroutine);
            hintCoroutine = null;
        }

        if (hintText != null)
        {
            hintText.alpha = 0f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartScale(baseScale * hoverScale);

        // 透明度直接变为悬停值，不做渐变
        SetAlpha(hoverAlpha);

        // PlayOneShot 允许快速来回悬停时音效重叠播放而不是互相打断
        if (hoverClip != null)
        {
            audioSource.PlayOneShot(hoverClip, hoverVolume);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartScale(baseScale);

        // 透明度直接恢复初始值
        SetAlpha(normalAlpha);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 锁面按钮全程不跳转：溶解已触发后点击全部忽略
        if (lockEnabled)
        {
            if (dissolving)
            {
                return;
            }

            PlayClickSound();

            if (DialogueFlags.HasFlag(unlockFlagName))
            {
                StartDissolve();
            }
            else
            {
                ShowHint();
            }

            return;
        }

        PlayClickSound();

        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneTransitionController.TransitionTo(sceneName);
        }
    }

    // 解锁标记未设置时的点击反馈：提示文字从当前透明度淡入，
    // 停留指定时长后淡出；期间再次点击会重启计时并继续升到 1
    private void ShowHint()
    {
        if (hintText == null)
        {
            Debug.LogWarning(
                "FrameButton：勾选了锁定但未指定提示文本（Hint Text），" +
                    "点击没有提示。",
                this
            );
            return;
        }

        if (!string.IsNullOrEmpty(hintMessage))
        {
            hintText.text = hintMessage;
        }

        if (hintCoroutine != null)
        {
            StopCoroutine(hintCoroutine);
        }

        hintCoroutine = StartCoroutine(HintRoutine());
    }

    private IEnumerator HintRoutine()
    {
        // 淡入：从当前透明度升到 1，重复点击时只升不降
        if (hintFadeInDuration > 0f)
        {
            float from = hintText.alpha;

            for (float time = 0f;
                 time < hintFadeInDuration;
                 time += Time.unscaledDeltaTime)
            {
                hintText.alpha = Mathf.Lerp(
                    from,
                    1f,
                    time / hintFadeInDuration
                );

                yield return null;
            }
        }

        hintText.alpha = 1f;

        // 停留：再次点击会重启整个协程，停留计时随之重置
        float holdEnd = Time.unscaledTime + hintHoldDuration;

        while (Time.unscaledTime < holdEnd)
        {
            yield return null;
        }

        // 淡出：从当前透明度降到 0
        if (hintFadeOutDuration > 0f)
        {
            float from = hintText.alpha;

            for (float time = 0f;
                 time < hintFadeOutDuration;
                 time += Time.unscaledDeltaTime)
            {
                hintText.alpha = Mathf.Lerp(
                    from,
                    0f,
                    time / hintFadeOutDuration
                );

                yield return null;
            }
        }

        hintText.alpha = 0f;
        hintCoroutine = null;
    }

    // 解锁标记已设置时的点击反馈：播放 UI 溶解，
    // 完全溶解后禁用自身，露出下方的跳转按钮
    private void StartDissolve()
    {
        if (dissolve == null)
        {
            dissolve = GetComponent<UIDissolveImage>();

            if (dissolve == null)
            {
                dissolve =
                    GetComponentInChildren<UIDissolveImage>(true);
            }
        }

        dissolving = true;
        dissolved = true;

        if (dissolve == null)
        {
            Debug.LogWarning(
                "FrameButton：锁面按钮上没有 UIDissolveImage，" +
                    "跳过溶解直接禁用。",
                this
            );
            gameObject.SetActive(false);
            return;
        }

        dissolve.Hide();

        StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        // 等溶解组件把自己的 Hide 动画播完（未缩放时间）
        while (dissolve != null && !dissolve.isHideComplete)
        {
            yield return null;
        }

        gameObject.SetActive(false);
    }

    // 用独立的临时物体播放点击音效：按钮常随面板整体禁用，
    // 挂在按钮上的音源会被一起掐断；临时物体独立存活到
    // 音频播完，面板秒关也不会截断音效
    private void PlayClickSound()
    {
        if (clickClip == null)
        {
            return;
        }

        GameObject soundHost = new GameObject("ClickSound");
        AudioSource source = soundHost.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        // 点击可能直接触发场景过渡（过渡即暂停音频），
        // 点击音效要能穿过黑屏播完
        source.ignoreListenerPause = true;

        source.volume = clickSoundVolume;
        source.PlayOneShot(clickClip);
        Destroy(soundHost, clickClip.length);
    }

    // 一次性设置按钮图片的透明度
    private void SetAlpha(float alpha)
    {
        if (targetImage == null)
        {
            return;
        }

        Color color = targetImage.color;
        color.a = alpha;
        targetImage.color = color;
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
