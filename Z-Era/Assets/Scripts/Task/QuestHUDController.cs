using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务 HUD：屏幕左上角一行——圆环进度 + 勾 + 当前任务指引文本。
/// 挂在 HUD Canvas 下，引用由检查器拖入。
/// 自动订阅 QuestManager 事件：
/// - 接取任务：显示第一个未完成目标；
/// - 目标进度更新：刷新文本与圆环填充（整体完成比例）；
/// - 任务完成：显示勾并弹跳，停留一段时间后隐藏，
///   若还有其他进行中的任务则自动切换显示。
/// </summary>
public class QuestHUDController : MonoBehaviour
{
    [Header("UI 引用")]

    [Tooltip("圆环进度 Image（Type: Filled, Fill Method: Radial 360）")]
    [SerializeField]
    private Image ringImage;

    [Tooltip("勾 Image，任务完成时显示并弹跳，平时隐藏")]
    [SerializeField]
    private GameObject checkObject;

    [Tooltip("任务指引文本")]
    [SerializeField]
    private TMP_Text guideText;

    [Header("文本格式")]

    [Tooltip("指引文本格式，{0}=目标指引，{1}=当前进度，{2}=需求次数；需求次数为 1 时不显示进度")]
    [SerializeField]
    private string guideFormat = "{0}（{1}/{2}）";

    [Header("动画")]

    [Tooltip("勾填充动画时长（秒）：任务完成时勾从填充 0 渐变到 1。要求勾的 Image 为 Filled 类型")]
    [SerializeField, Min(0.01f)]
    private float checkFillDuration = 1f;

    [Tooltip("勾填充到 1 后停留多久再淡出并切换下一个任务（秒）")]
    [SerializeField, Min(0f)]
    private float completedHoldDuration = 2f;

    [Header("切换过渡")]

    [Tooltip("整行淡入时长（秒）。通过运行时自动添加的 CanvasGroup 驱动，无需手动挂组件")]
    [SerializeField, Min(0.05f)]
    private float fadeInDuration = 0.3f;

    [Tooltip("整行淡出时长（秒）")]
    [SerializeField, Min(0.05f)]
    private float fadeOutDuration = 0.3f;

    [Tooltip("整行根节点（圆环、勾、文本的公共父级，如 TaskGroup），留空则自动向上查找公共父级")]
    [SerializeField]
    private GameObject rowRoot;

    [Header("完成音效")]

    [Tooltip("任务完成时播放的音效，把音频资产拖进来即可，留空则不播放")]
    [SerializeField]
    private AudioClip completeSound;

    [Tooltip("完成音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float completeSoundVolume = 1f;

    // OnEnable 缓存的管理器引用，OnDisable 退订时使用，
    // 避免运行末尾再触发管理器的自动创建。
    private QuestManager manager;

    // 整行透明度驱动，Awake 时在行根节点上自动创建。
    private CanvasGroup rowCanvasGroup;

    // 勾的 Image，Awake 时从 checkObject 上获取，
    // 完成动画通过 fillAmount 驱动（要求 Filled 类型）。
    private Image checkImage;

    // 勾原始颜色的透明度，淡出后由 HideCheck 恢复。
    private float checkBaseAlpha = 1f;

    // 完成音效的专属音源，配置了音效时在 Awake 创建。
    private AudioSource completeAudioSource;

    private QuestAsset displayedQuest;

    // 完成序列播放期间接取的新任务在此排队，
    // 序列收尾后自动切换显示，避免打断勾的填充演出。
    private QuestAsset pendingQuest;

    private Coroutine activeAnimation;

    private void Awake()
    {
        if (completeSound != null)
        {
            // 专属音源（2D），不复用其他系统的音源，
            // 避免脚步声等被 Stop 时把完成音效一起掐断。
            completeAudioSource =
                gameObject.AddComponent<AudioSource>();

            completeAudioSource.playOnAwake = false;
            completeAudioSource.spatialBlend = 0f;
        }

        if (checkObject != null)
        {
            checkImage = checkObject.GetComponent<Image>();

            if (checkImage == null)
            {
                Debug.LogWarning(
                    "QuestHUDController：checkObject 上没有 Image 组件，" +
                        "勾填充动画无法播放。",
                    this
                );
            }
            else
            {
                checkBaseAlpha = checkImage.color.a;
            }
        }

        // 运行时兜底：填充动画要求勾的 Image 为 Filled 类型，
        // 检查器漏设或设置未生效时在此强制纠正，
        // 否则 fillAmount 对 Simple 类型完全无效（勾不会渐显）。
        // 圆环按需求保持 Simple，不做纠正。
        if (checkImage != null &&
            checkImage.type != Image.Type.Filled)
        {
            checkImage.type = Image.Type.Filled;
            checkImage.fillMethod = Image.FillMethod.Radial360;
        }

        // 勾位于圆环内，强制排到同级最后以保证渲染在圆环之上，
        // 避免被圆环贴图的不透明中心遮挡。
        if (checkObject != null)
        {
            checkObject.transform.SetAsLastSibling();
        }

        Transform root = FindRowRoot();

        if (root == null)
        {
            Debug.LogWarning(
                "QuestHUDController：找不到包含圆环、勾与文本的公共父级，" +
                "切换过渡退化为瞬间显隐。可在检查器手动指定 Row Root。",
                this
            );

            return;
        }

        rowCanvasGroup = root.GetComponent<CanvasGroup>();

        if (rowCanvasGroup == null)
        {
            rowCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        }

        rowCanvasGroup.blocksRaycasts = false;
        rowCanvasGroup.interactable = false;
        rowCanvasGroup.alpha = 0f;

        // TaskText 里可能留有占位文本，行隐藏后编辑器里也不会看到，
        // 运行时首帧即隐藏，等首个任务接取后淡入。
    }

    // 查找同时包含圆环、勾、文本的最小公共父级。
    private Transform FindRowRoot()
    {
        if (rowRoot != null)
        {
            return rowRoot.transform;
        }

        if (ringImage == null)
        {
            return null;
        }

        Transform candidate = ringImage.transform.parent;

        while (candidate != null)
        {
            bool coversGuide = guideText == null ||
                guideText.transform.IsChildOf(candidate);

            bool coversCheck = checkObject == null ||
                checkObject.transform.IsChildOf(candidate);

            if (coversGuide && coversCheck)
            {
                return candidate;
            }

            candidate = candidate.parent;
        }

        return null;
    }

    private void OnEnable()
    {
        // 首次访问自动创建常驻管理器（同 DialogueFlags 模式）
        manager = QuestManager.Instance;

        manager.QuestAccepted += OnQuestAccepted;
        manager.ObjectiveUpdated += OnObjectiveUpdated;
        manager.QuestCompleted += OnQuestCompleted;

        HideCheck();

        // HUD 所在场景中途加载（任务早已接取）时，
        // 直接显示最近接取的活动任务，不做入场动画。
        IReadOnlyList<QuestAsset> activeQuests =
            QuestManager.ActiveQuests;

        if (displayedQuest == null && activeQuests.Count > 0)
        {
            displayedQuest = activeQuests[activeQuests.Count - 1];

            HideCheck();

            RefreshDisplay();
            SetRowAlphaImmediate(1f);
        }
        else if (displayedQuest == null)
        {
            SetRowAlphaImmediate(0f);
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.QuestAccepted -= OnQuestAccepted;
            manager.ObjectiveUpdated -= OnObjectiveUpdated;
            manager.QuestCompleted -= OnQuestCompleted;

            manager = null;
        }

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
            activeAnimation = null;
        }
    }

    private void OnQuestAccepted(QuestAsset quest)
    {
        // 完成序列播放中不打断演出（否则勾的填充动画会被
        // StopCoroutine 杀死在起始帧），把新任务排队，
        // 序列自然收尾后由 CompletedSequence 切换显示。
        if (activeAnimation != null)
        {
            pendingQuest = quest;
            return;
        }

        // 始终显示最新接取的任务。
        ShowQuest(quest);
    }

    private void OnObjectiveUpdated(
        QuestAsset quest,
        QuestAsset.QuestObjective objective,
        int current,
        int required
    )
    {
        if (quest != displayedQuest)
        {
            return;
        }

        // 单个目标达成（任务尚未整体完成）时播放勾填充演出，
        // 演出结束后再切换到下一个目标的文本；
        // 最后一个目标达成时不在此演出，交给任务完成序列。
        if (current >= required && !AllObjectivesComplete(quest))
        {
            if (activeAnimation != null)
            {
                StopCoroutine(activeAnimation);
            }

            activeAnimation = StartCoroutine(
                ObjectiveCompletedSequence()
            );

            return;
        }

        RefreshDisplay();
    }

    // 任务的所有目标是否都已达标。
    private bool AllObjectivesComplete(QuestAsset quest)
    {
        foreach (
            QuestAsset.QuestObjective objective
                in quest.Objectives
        )
        {
            if (objective == null)
            {
                continue;
            }

            if (QuestManager.GetObjectiveCurrent(
                    quest.QuestId,
                    objective.objectiveId
                ) < objective.requiredAmount)
            {
                return false;
            }
        }

        return true;
    }

    // 单个目标完成：音效 → 勾填充 0 到 1 → 勾淡出
    // → 指引文本淡出并切换到下一个目标 → 文本淡入。
    // 整行保持显示，任务继续进行。
    private IEnumerator ObjectiveCompletedSequence()
    {
        if (completeAudioSource != null && completeSound != null)
        {
            completeAudioSource.PlayOneShot(
                completeSound,
                completeSoundVolume
            );
        }

        if (checkObject != null)
        {
            checkObject.SetActive(true);
        }

        if (checkImage != null)
        {
            checkImage.fillAmount = 0f;

            for (float t = 0f;
                 t < 1f;
                 t += Time.deltaTime / checkFillDuration)
            {
                checkImage.fillAmount = t;

                yield return null;
            }

            checkImage.fillAmount = 1f;
        }

        yield return FadeCheck(0f, fadeOutDuration);

        yield return FadeGuideText(0f, fadeOutDuration);

        RefreshDisplay();

        yield return FadeGuideText(1f, fadeInDuration);

        HideCheck();

        activeAnimation = null;
    }

    private void OnQuestCompleted(QuestAsset quest)
    {
        if (quest != displayedQuest)
        {
            return;
        }

        // 文本刷新为完成态（显示最后一个目标），圆环充满，勾弹跳。
        RefreshDisplay();

        if (ringImage != null)
        {
            ringImage.fillAmount = 1f;
        }

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        activeAnimation = StartCoroutine(CompletedSequence());
    }

    // 任务完成：播放音效 → 勾从填充 0 渐变到 1 → 停留 → 淡出 → 切换下一个任务。
    private IEnumerator CompletedSequence()
    {
        if (completeAudioSource != null && completeSound != null)
        {
            completeAudioSource.PlayOneShot(
                completeSound,
                completeSoundVolume
            );
        }

        if (checkObject != null)
        {
            checkObject.SetActive(true);
        }

        if (checkImage != null)
        {
            // 清掉上次残留的填充值，从 0 渐变到 1；
            // Filled Image 填充为 0 时完全不渲染，
            // 这也是之前“勾不出现”的原因。
            checkImage.fillAmount = 0f;

            for (float t = 0f;
                 t < 1f;
                 t += Time.deltaTime / checkFillDuration)
            {
                checkImage.fillAmount = t;

                yield return null;
            }

            checkImage.fillAmount = 1f;
        }

        if (completedHoldDuration > 0f)
        {
            yield return new WaitForSeconds(completedHoldDuration);
        }

        yield return FadeRow(0f, fadeOutDuration);

        displayedQuest = null;

        HideCheck();

        // 优先显示完成期间排队的新任务，否则取最新接取的活动任务。
        QuestAsset nextQuest = pendingQuest;
        pendingQuest = null;

        if (nextQuest == null)
        {
            IReadOnlyList<QuestAsset> activeQuests =
                QuestManager.ActiveQuests;

            if (activeQuests.Count > 0)
            {
                nextQuest = activeQuests[activeQuests.Count - 1];
            }
        }

        if (nextQuest != null)
        {
            yield return ShowQuestSequence(nextQuest);
        }

        activeAnimation = null;
    }

    private void ShowQuest(QuestAsset quest)
    {
        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        activeAnimation = StartCoroutine(
            ShowQuestSequence(quest)
        );
    }

    // 显示任务：行可见时先淡出旧内容，再换新内容淡入；
    // 行本就隐藏时直接换内容淡入。
    private IEnumerator ShowQuestSequence(QuestAsset quest)
    {
        if (rowCanvasGroup != null &&
            rowCanvasGroup.alpha > 0.001f)
        {
            yield return FadeRow(0f, fadeOutDuration);
        }

        displayedQuest = quest;

        HideCheck();

        RefreshDisplay();

        yield return FadeRow(1f, fadeInDuration);

        // 协程自然结束（含被 CompletedSequence 嵌套调用的情况），
        // 清空引用让后续接取不再被误判为“演出进行中”。
        activeAnimation = null;
    }

    // 刷新指引文本与圆环填充：显示第一个未完成的目标；
    // 全部完成时显示最后一个目标。
    private void RefreshDisplay()
    {
        if (displayedQuest == null)
        {
            return;
        }

        QuestAsset.QuestObjective shownObjective = null;

        foreach (
            QuestAsset.QuestObjective objective
                in displayedQuest.Objectives
        )
        {
            if (objective == null)
            {
                continue;
            }

            shownObjective = objective;

            if (QuestManager.GetObjectiveCurrent(
                    displayedQuest.QuestId,
                    objective.objectiveId
                ) < objective.requiredAmount)
            {
                break;
            }
        }

        if (shownObjective == null)
        {
            if (guideText != null)
            {
                guideText.text = string.Empty;
            }

            return;
        }

        int current = QuestManager.GetObjectiveCurrent(
            displayedQuest.QuestId,
            shownObjective.objectiveId
        );

        if (guideText != null)
        {
            guideText.text = shownObjective.requiredAmount > 1
                ? string.Format(
                    guideFormat,
                    shownObjective.description,
                    current,
                    shownObjective.requiredAmount
                )
                : shownObjective.description;
        }

        if (ringImage != null)
        {
            ringImage.fillAmount = GetOverallProgress();
        }
    }

    // 整体完成比例：所有目标进度之和 / 所有目标需求之和。
    private float GetOverallProgress()
    {
        int done = 0;
        int total = 0;

        foreach (
            QuestAsset.QuestObjective objective
                in displayedQuest.Objectives
        )
        {
            if (objective == null)
            {
                continue;
            }

            total += objective.requiredAmount;

            done += Mathf.Min(
                QuestManager.GetObjectiveCurrent(
                    displayedQuest.QuestId,
                    objective.objectiveId
                ),
                objective.requiredAmount
            );
        }

        return total > 0 ? (float)done / total : 1f;
    }

    // 隐藏勾并清零填充与透明度，保证下次完成动画从干净状态开始。
    private void HideCheck()
    {
        if (checkObject != null)
        {
            checkObject.SetActive(false);
        }

        if (checkImage != null)
        {
            checkImage.fillAmount = 0f;

            Color color = checkImage.color;
            color.a = checkBaseAlpha;
            checkImage.color = color;
        }
    }

    // 勾的透明度渐变（完成演出收尾时淡出用）。
    private IEnumerator FadeCheck(float target, float duration)
    {
        if (checkImage == null)
        {
            yield break;
        }

        float start = checkImage.color.a;

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            Color color = checkImage.color;
            color.a = Mathf.Lerp(start, target, t);
            checkImage.color = color;

            yield return null;
        }

        Color finalColor = checkImage.color;
        finalColor.a = target;
        checkImage.color = finalColor;
    }

    // 指引文本透明度渐变（目标切换时先淡出再淡入）。
    private IEnumerator FadeGuideText(float target, float duration)
    {
        if (guideText == null)
        {
            yield break;
        }

        float start = guideText.alpha;

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            guideText.alpha = Mathf.Lerp(start, target, t);

            yield return null;
        }

        guideText.alpha = target;
    }

    // 立即设置整行透明度（场景中途加载等无动画场景使用）。
    private void SetRowAlphaImmediate(float alpha)
    {
        if (rowCanvasGroup != null)
        {
            rowCanvasGroup.alpha = alpha;
        }
    }

    // 整行透明度渐变，从当前 alpha 平滑过渡到目标值。
    private IEnumerator FadeRow(float target, float duration)
    {
        if (rowCanvasGroup == null)
        {
            yield break;
        }

        float start = rowCanvasGroup.alpha;

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            rowCanvasGroup.alpha = Mathf.Lerp(start, target, t);

            yield return null;
        }

        rowCanvasGroup.alpha = target;
    }
}
