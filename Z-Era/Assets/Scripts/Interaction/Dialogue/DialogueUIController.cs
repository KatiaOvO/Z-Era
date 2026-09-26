using System;
using System.Collections;
using System.Collections.Generic;
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

    private readonly List<RectTransform> spawnedChoiceButtons =
        new List<RectTransform>();

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
        HideChoices();

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

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = choice.buttonText;
            }

            button.GetComponent<Button>().onClick.AddListener(
                () => onSelect(choice)
            );

            spawnedChoiceButtons.Add(button);
        }
    }

    public void HideChoices()
    {
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
