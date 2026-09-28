using System;
using System.Collections;
using System.Collections.Generic;
using Migration.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 对话 UI 表现层：正文打字机、说话人信息、选项按钮生成。
/// 只负责显示与输入表现，推进逻辑由 DialogueRunner 驱动。
/// </summary>
public class DialogueUIController : MonoBehaviour
{
    [Header("UI 引用")]

    [Tooltip("说话人名称文本")]
    [SerializeField]
    private TMP_Text speakerText;

    [Tooltip("正文文本")]
    [SerializeField]
    private TMP_Text bodyText;

    [Tooltip("说话人头像，留空则不显示头像")]
    [SerializeField]
    private Image portraitImage;

    [Tooltip("选项按钮的父容器（带 Vertical Layout Group）")]
    [SerializeField]
    private RectTransform choicesContainer;

    [Tooltip("选项按钮预制体：Button + 子 TMP 文本")]
    [SerializeField]
    private RectTransform choiceButtonPrefab;

    [Tooltip("继续指示标记（打字中隐藏，等待点击时显示）")]
    [SerializeField]
    private GameObject continueIndicator;

    [Header("继续标记动画")]

    [Tooltip("继续标记上下浮动的幅度（像素）")]
    [SerializeField, Min(0f)]
    private float continueIndicatorBobDistance = 8f;

    [Tooltip("继续标记上下浮动的速度（每秒循环次数）")]
    [SerializeField, Min(0.1f)]
    private float continueIndicatorBobSpeed = 1.5f;

    [Header("选项按钮宽度")]

    [Tooltip("选项按钮的最小宽度（像素），短选项保底")]
    [SerializeField, Min(0f)]
    private float choiceButtonMinWidth = 160f;

    [Tooltip("选项按钮的最大宽度（像素），超过的选项文本自动换行")]
    [SerializeField, Min(0f)]
    private float choiceButtonMaxWidth = 480f;

    [Tooltip("文字与按钮左右边缘的边距（像素，单侧）")]
    [SerializeField, Min(0f)]
    private float choiceTextMarginX = 24f;

    [Tooltip("文字与按钮上下边缘的边距（像素，单侧）")]
    [SerializeField, Min(0f)]
    private float choiceTextMarginY = 10f;

    [Header("打字机")]

    [Tooltip("每秒打印的字符数")]
    [Min(1f)]
    [SerializeField]
    private float charactersPerSecond = 30f;

    [Tooltip("句末标点（. ! ? 等）后的停顿秒数")]
    [SerializeField, Min(0f)]
    private float sentencePause = 0.15f;

    [Tooltip("逗号类标点后的停顿秒数")]
    [SerializeField, Min(0f)]
    private float commaPause = 0.05f;

    public bool IsTyping { get; private set; }

    // 打字完成事件，DialogueRunner 订阅后决定进入
    // "等待点击"还是"等待选择"状态。
    public event Action TypewriterCompleted;

    private Coroutine typingCoroutine;

    // 继续标记的设计位置，浮动动画围绕它上下摆动。
    private Vector2 continueIndicatorBasePosition;

    private readonly List<RectTransform> spawnedChoiceButtons =
        new List<RectTransform>();

    // 与按钮一一对应的溶解组件（按钮根挂 UIDissolveImage 时生效）。
    private readonly List<IUIDissolveEffect> spawnedChoiceDissolves =
        new List<IUIDissolveEffect>();

    private Coroutine choicesHideCoroutine;

    // 选项按钮正在溶解消失、尚未销毁。
    public bool IsChoicesHiding { get; private set; }

    // 句末标点：打完后停顿更久。
    private static readonly char[] SentenceEnders =
    {
        '.', '!', '?', '。', '！', '？', '…', '；', ';'
    };

    // 逗号类标点：打完后短暂停顿。
    private static readonly char[] CommaChars =
    {
        ',', '，', '、', '：', ':'
    };

    private void Awake()
    {
        // 记录继续标记的设计位置，浮动围绕它进行。
        if (continueIndicator != null &&
            continueIndicator.transform
                is RectTransform rect)
        {
            continueIndicatorBasePosition = rect.anchoredPosition;
        }
    }

    private void Update()
    {
        // 继续标记可见时围绕设计位置上下浮动。
        if (continueIndicator == null ||
            !continueIndicator.activeInHierarchy)
        {
            return;
        }

        if (continueIndicator.transform
                is not RectTransform rect)
        {
            return;
        }

        float bob = Mathf.Sin(
            Time.unscaledTime *
            continueIndicatorBobSpeed *
            Mathf.PI * 2f
        ) * continueIndicatorBobDistance;

        rect.anchoredPosition =
            continueIndicatorBasePosition + Vector2.up * bob;
    }

    // 在文本测量结果之上额外增加的整体余量。
    private const float ChoiceExtraWidth = 20f;
    private const float ChoiceExtraHeight = 10f;

    /// <summary>
    /// 清空全部显示内容。对话打开、溶解入场播放前调用，
    /// 避免预制体中的占位文本在溶解期间露出来。
    /// </summary>
    public void ResetDisplay()
    {
        StopTyping();
        IsTyping = false;

        if (speakerText != null)
        {
            speakerText.gameObject.SetActive(false);
            speakerText.text = string.Empty;
        }

        if (bodyText != null)
        {
            bodyText.text = string.Empty;
        }

        if (portraitImage != null)
        {
            portraitImage.gameObject.SetActive(false);
        }

        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }

        HideChoices();
    }

    /// <summary>
    /// 显示一个节点：设置说话人、头像，并开始打字机。
    /// </summary>
    public void ShowNode(DialogueAsset.DialogueNode node)
    {
        StopTyping();

        bool hasSpeaker =
            node != null &&
            !string.IsNullOrWhiteSpace(node.speakerName);

        if (speakerText != null)
        {
            speakerText.gameObject.SetActive(hasSpeaker);

            if (hasSpeaker)
            {
                speakerText.text = node.speakerName;

                // 名字只使用节点颜色的 RGB：
                // 检查器取色器的 Alpha 滑条容易停在 0，
                // 会导致名字完全透明、看起来像"没赋值"。
                Color nameColor = node.speakerColor;
                nameColor.a = 1f;
                speakerText.color = nameColor;
            }
        }

        bool hasPortrait = node != null && node.portrait != null;

        if (portraitImage != null)
        {
            portraitImage.gameObject.SetActive(hasPortrait);

            if (hasPortrait)
            {
                portraitImage.sprite = node.portrait;
            }
        }

        HideChoices();

        string text = node != null ? node.text : string.Empty;
        typingCoroutine = StartCoroutine(
            TypeRoutine(text ?? string.Empty)
        );
    }

    /// <summary>
    /// 立即显示全部正文（玩家在打字途中点击时调用）。
    /// </summary>
    public void CompleteTypewriter()
    {
        if (!IsTyping)
        {
            return;
        }

        StopTyping();
        FinishTyping();
    }

    /// <summary>
    /// 显示选项按钮组，只传入已通过条件筛选的选项。
    /// </summary>
    public void ShowChoices(
        IReadOnlyList<DialogueAsset.DialogueChoice> choices,
        UnityAction<DialogueAsset.DialogueChoice> onSelect
    )
    {
        // 上一次隐藏动画若未结束，立即清理掉再生成新按钮。
        ForceClearChoices();

        if (choicesContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogError(
                "DialogueUIController：未配置选项容器或按钮预制体。",
                this
            );
            return;
        }

        choicesContainer.gameObject.SetActive(true);

        foreach (
            DialogueAsset.DialogueChoice choice in choices
        )
        {
            RectTransform button = Instantiate(
                choiceButtonPrefab,
                choicesContainer
            );

            // 注入引用，悬停表现组件就绪。
            button.GetComponent<DialogueChoiceButton>()
                ?.Setup();

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = choice.buttonText;

                // 文字子物体拉伸铺满按钮、居中、可换行，
                // 按钮尺寸随后由 ApplyUniformChoiceWidths 统一设定。
                ConfigureChoiceLabel(label);
            }

            button.GetComponent<Button>().onClick.AddListener(
                () => onSelect(choice)
            );

            spawnedChoiceButtons.Add(button);

            // 缓存按钮及其文字上的全部溶解组件；
            // 出现时统一驱动（UIDissolveImage 不像 UIDissolveText
            // 那样会在 OnEnable 自驱动，必须显式 Show）。
            IUIDissolveEffect[] dissolves =
                button.GetComponentsInChildren<
                    IUIDissolveEffect
                >(true);

            foreach (
                IUIDissolveEffect dissolve in dissolves
            )
            {
                spawnedChoiceDissolves.Add(dissolve);

                dissolve.SetLocation(1f);
                dissolve.Show();
            }
        }

        ApplyUniformChoiceWidths();
    }

    // 让文字子物体拉伸铺满按钮，边距与对齐方式由代码接管，
    // 不依赖预制体里文字的锚点/对齐配置。
    private void ConfigureChoiceLabel(TMP_Text label)
    {
        RectTransform rect = label.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(
            choiceTextMarginX,
            choiceTextMarginY
        );
        rect.offsetMax = new Vector2(
            -choiceTextMarginX,
            -choiceTextMarginY
        );

        label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.Left;
    }

    // 统一选项按钮尺寸：宽度取最长选项（夹在最小/最大之间），
    // 高度按各自文本（长选项换行后）自适应。
    // 全部由代码计算，不依赖按钮预制体上的布局组件配置。
    private void ApplyUniformChoiceWidths()
    {
        if (spawnedChoiceButtons.Count == 0)
        {
            return;
        }

        // 容器若开了"拉伸子物体宽度"会覆盖代码设定的宽度，
        // 这里强制关闭并居中对齐。
        VerticalLayoutGroup containerLayout =
            choicesContainer != null
                ? choicesContainer
                    .GetComponent<VerticalLayoutGroup>()
                : null;

        if (containerLayout != null)
        {
            containerLayout.childForceExpandWidth = false;
            containerLayout.childAlignment = TextAnchor.MiddleCenter;
        }

        float widest = 0f;

        foreach (
            RectTransform button in spawnedChoiceButtons
        )
        {
            if (button == null)
            {
                continue;
            }

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);

            if (label == null)
            {
                continue;
            }

            // 单行不换行时的文本宽度 + 左右边距 = 按钮所需宽度。
            Vector2 preferred = label.GetPreferredValues(
                label.text,
                Mathf.Infinity,
                Mathf.Infinity
            );

            widest = Mathf.Max(
                widest,
                preferred.x + choiceTextMarginX * 2f + ChoiceExtraWidth
            );
        }

        float finalWidth = Mathf.Clamp(
            widest,
            choiceButtonMinWidth,
            choiceButtonMaxWidth
        );

        foreach (
            RectTransform button in spawnedChoiceButtons
        )
        {
            if (button == null)
            {
                continue;
            }

            button.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                finalWidth
            );

            // 宽度确定后重新测量文本（长选项会换行变高），
            // 让按钮高度跟随实际行数。
            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);

            if (label == null)
            {
                continue;
            }

            Vector2 preferred = label.GetPreferredValues(
                label.text,
                finalWidth - choiceTextMarginX * 2f,
                Mathf.Infinity
            );

            button.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                preferred.y + choiceTextMarginY * 2f + ChoiceExtraHeight
            );
        }

        if (choicesContainer != null)
        {
            // 立即重排，避免出现一帧按钮挤在一起的闪烁。
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                choicesContainer
            );
        }
    }

    /// <summary>
    /// 隐藏选项：按钮根挂有溶解组件时先播溶解消失，
    /// 播完再销毁；没挂则立即清理。非阻塞，
    /// 动画状态通过 IsChoicesHiding 查询。
    /// </summary>
    public void HideChoices()
    {
        if (spawnedChoiceButtons.Count == 0 || IsChoicesHiding)
        {
            return;
        }

        // 点击的瞬间就清空按钮文字，溶解消失随后（延迟后）开始。
        foreach (
            RectTransform button in spawnedChoiceButtons
        )
        {
            if (button == null)
            {
                continue;
            }

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = string.Empty;
            }

            // 中断悬停填充并还原，避免白色填充残留在溶解画面里。
            button.GetComponent<DialogueChoiceButton>()
                ?.OnHide();
        }

        IsChoicesHiding = true;
        choicesHideCoroutine = StartCoroutine(
            FinishHideChoices()
        );
    }

    private IEnumerator FinishHideChoices()
    {
        bool hasDissolve = false;

        foreach (
            IUIDissolveEffect dissolve in
                spawnedChoiceDissolves
        )
        {
            if (dissolve == null)
            {
                continue;
            }

            dissolve.Hide();
            hasDissolve = true;
        }

        if (!hasDissolve)
        {
            ForceClearChoices();
            yield break;
        }

        // 最多等 2 秒兜底，防止某个溶解卡住导致按钮永不销毁。
        float timeout = Time.unscaledTime + 2f;

        while (Time.unscaledTime < timeout &&
               !AllChoiceDissolvesHidden())
        {
            yield return null;
        }

        ForceClearChoices();
    }

    private bool AllChoiceDissolvesHidden()
    {
        foreach (
            IUIDissolveEffect dissolve in
                spawnedChoiceDissolves
        )
        {
            Component component = dissolve as Component;

            if (dissolve == null ||
                component == null ||
                !component.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!dissolve.isHideComplete)
            {
                return false;
            }
        }

        return true;
    }

    // 立即销毁全部选项按钮（不播溶解动画）。
    private void ForceClearChoices()
    {
        if (choicesHideCoroutine != null)
        {
            StopCoroutine(choicesHideCoroutine);
            choicesHideCoroutine = null;
        }

        IsChoicesHiding = false;

        foreach (
            RectTransform button in spawnedChoiceButtons
        )
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        spawnedChoiceButtons.Clear();
        spawnedChoiceDissolves.Clear();

        if (choicesContainer != null)
        {
            choicesContainer.gameObject.SetActive(false);
        }
    }

    private IEnumerator TypeRoutine(string fullText)
    {
        IsTyping = true;

        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }

        bodyText.text = fullText;

        // maxVisibleCharacters 只统计可见字符，
        // 富文本标签不会被当作字符打印出来。
        bodyText.maxVisibleCharacters = 0;

        int total = fullText.Length;
        int revealed = 0;
        float revealProgress = 0f;
        float pauseRemaining = 0f;

        while (revealed < total)
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (pauseRemaining > 0f)
            {
                // 标点停顿期间不推进打印。
                pauseRemaining -= deltaTime;
            }
            else
            {
                revealProgress +=
                    charactersPerSecond * deltaTime;

                int target = Mathf.Min(
                    total,
                    Mathf.FloorToInt(revealProgress)
                );

                // 只有真的露出了新字符才更新显示并结算停顿：
                // 首帧 deltaTime 可能为 0（target 仍为 0），
                // 慢速打字时 target 也可能连续几帧不变，
                // 这两种情况都不能取 target - 1，否则越界或重复停顿。
                if (target > revealed)
                {
                    char lastChar = fullText[target - 1];

                    bodyText.maxVisibleCharacters = target;
                    revealed = target;

                    if (Array.IndexOf(SentenceEnders, lastChar) >= 0)
                    {
                        pauseRemaining = sentencePause;
                    }
                    else if (Array.IndexOf(CommaChars, lastChar) >= 0)
                    {
                        pauseRemaining = commaPause;
                    }
                }
            }

            yield return null;
        }

        FinishTyping();
    }

    private void FinishTyping()
    {
        IsTyping = false;

        if (continueIndicator != null)
        {
            continueIndicator.SetActive(true);
        }

        TypewriterCompleted?.Invoke();
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
    }

    private void OnDisable()
    {
        // 面板被隐藏时终止打字协程，避免残留回调。
        StopTyping();
        IsTyping = false;
    }
}
