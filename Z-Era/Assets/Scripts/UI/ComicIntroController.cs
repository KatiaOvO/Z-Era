using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 漫画剧情介绍画布：从主菜单切换到目标场景后，由场景过渡流程带出的
/// 开场漫画画布。画布上有多张漫画图片，初始全部透明（Alpha = 0），
/// 右下角显示点击提示；玩家每点击一次屏幕，按列表顺序让下一张图片
/// 从透明淡入到不透明（时长可在检查器中修改，默认 1 秒）。
///
/// 图片淡入的同时，画面下方逐字打印该图对应的文本段落（一张图可
/// 配一段或多段）：当前段落打印完才允许继续点击，点击时上一段消失、
/// 开始打印下一段；该图全部段落打印完后再点击才进入下一张图。
/// 打印期间点击提示隐藏，打印完成后显示"点击继续"。
///
/// 使用方式：
/// 1. 在目标场景新建 Canvas（Screen Space - Overlay），挂本脚本；
/// 2. 根物体需要一个铺满全屏的 Image（颜色透明即可，Raycast Target
///    必须勾选），用于接收整屏点击并挡住下层 UI/游戏世界；
/// 3. 在画布下摆放若干漫画图片子物体（位置、大小随意摆），
///    每张图上需要一个 Canvas Group（可用右键菜单自动添加并填充列表）；
/// 4. 检查器里 Comic Panels 列表的顺序即点击时的淡入顺序，
///    数量、位置、顺序全部在检查器中配置。
///
/// 播完后：画布保持显示，玩家再点击一次执行收尾——填写了
/// Next Scene Name 时经 SceneTransitionController.TransitionTo
/// 跳转（走全局黑屏加载过渡），否则按 Hide Canvas When Finished
/// 隐藏画布。画布显示期间暴露 static IsOpen，供其他面板的互斥
/// 门控检查。
///
/// 光标：显示期间强制解锁并显示光标（场景加载时本画布 OnEnable
/// 早于 PlayerController.Start 执行，一次性解锁会被它随后的锁定
/// 覆盖，因此 Update 中每帧兜底保持解锁）；画布关闭时若场景中
/// 存在 PlayerController，则恢复锁定回到正常玩法状态。
/// </summary>
[RequireComponent(typeof(Image))]
public class ComicIntroController : MonoBehaviour, IPointerClickHandler
{
    // 全局门控属性：本画布显示期间为 true，
    // 其他系统的互斥判断可直接读取
    public static bool IsOpen { get; private set; }

    // 一张图片对应的文本段落（与 Comic Panels 按下标一一对应）：
    // 点击逐段打印，段落切换时上一段消失
    [Serializable]
    public class ComicPanelText
    {
        [Tooltip("该图对应的文本段落，按点击顺序逐段打印；不填则该图无文本")]
        public string[] lines = new string[0];
    }

    [Header("引用")]

    [Tooltip("漫画图片列表：按顺序淡入，列表顺序即显示顺序；" +
        "每项指向图片物体上的 Canvas Group（没有会自动补上）。" +
        "数量、位置、顺序都在检查器中配置")]
    [SerializeField]
    private List<CanvasGroup> comicPanels = new List<CanvasGroup>();

    [Tooltip("右下角的点击提示文本（如“点击继续”），留空则不控制显隐")]
    [SerializeField]
    private TMP_Text clickHintText;

    [Tooltip("画面下方的剧情文本（TMP）：逐字打印的显示目标，留空则不打印")]
    [SerializeField]
    private TMP_Text storyText;

    [Header("文本")]

    [Tooltip("每张图对应的文本段落，与 Comic Panels 按下标一一对应；" +
        "点击逐段打印，一张图可配多段，不填则该图无文本")]
    [SerializeField]
    private List<ComicPanelText> panelTexts = new List<ComicPanelText>();

    [Tooltip("逐字打印速度（每秒打印的字符数）")]
    [SerializeField, Min(1f)]
    private float typeSpeed = 30f;

    [Header("参数")]

    [Tooltip("单张图片从透明到不透明的淡入时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float fadeDuration = 1f;

    [Tooltip("全部漫画显示完后，下一次点击跳转的目标场景名" +
        "（走全局场景过渡）。留空则不跳转，按下面的选项收尾")]
    [SerializeField]
    private string nextSceneName = "";

        [Tooltip("全部漫画显示完后，再点击一次隐藏整个画布" +
            "（不再拦截屏幕点击）；若填写了 Next Scene Name，" +
            "则以跳转场景为准，本选项不生效")]
        [SerializeField]
        private bool hideCanvasWhenFinished = true;

        [Tooltip("全部漫画显示完后，在最后一次点击收尾时隐藏右下角" +
            "的点击提示（等待收尾点击期间保留，作为点击引导）")]
        [SerializeField]
        private bool hideHintWhenFinished = true;

    [Tooltip("全部漫画显示完毕后触发一次（可用来接对话、任务等后续逻辑）")]
    [SerializeField]
    private UnityEvent onAllComicsShown;

    // 当前正在淡入/已显示的图片下标（-1 表示尚未开始）
    private int activeImageIndex = -1;

    // 当前图片已打印到的文本段落下标
    private int activeLineIndex;

    // 正在逐字打印文本：打印完成前忽略点击
    private bool isTyping;

    // 正在淡入时为 true，期间忽略点击（连点不会跳张）
    private bool isFading;

    // 图片与文本全部完成（淡入 + 打印）后为 true，
    // 之后的点击走跳转/收尾逻辑
    private bool finished;

    private Coroutine fadeCoroutine;
    private Coroutine typeCoroutine;

    // 打印完成后点击提示显示的文本内容
    private const string ClickHintContent = "点击继续";

    private void OnEnable()
    {
        IsOpen = true;
        ResetState();

        // 立即解锁并显示光标：本画布需要接收整屏点击
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 场景加载时本画布 OnEnable 早于 PlayerController.Start 执行，
    // 上面临时解锁的光标会被它随后的 Start 锁定覆盖；画布显示期间
    // 每帧兜底保持解锁可见，直到画布关闭
    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }
    }

    private void OnDisable()
    {
        IsOpen = false;

        // 收尾后回到玩法：场景里有玩家控制器时恢复光标锁定
        //（纯 UI 场景没有 PlayerController，保持解锁避免光标卡死）
        if (FindObjectOfType<PlayerController>() != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        // 画布中途被外部禁用时，停掉残留的淡入/打印协程
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
            isFading = false;
        }

        if (typeCoroutine != null)
        {
            StopCoroutine(typeCoroutine);
            typeCoroutine = null;
            isTyping = false;
        }
    }

    private void Start()
    {
        if (comicPanels.Count == 0)
        {
            Debug.LogWarning(
                "ComicIntroController 未配置任何漫画图片，" +
                "请在检查器的 Comic Panels 列表中添加（或用右键菜单自动填充）"
            );
        }

        if (storyText == null)
        {
            foreach (ComicPanelText text in panelTexts)
            {
                if (text != null &&
                    text.lines != null &&
                    text.lines.Length > 0)
                {
                    Debug.LogWarning(
                        "ComicIntroController：已配置文本段落但 Story Text" +
                            "未赋值，打印会被跳过（点击仍可正常推进）。",
                        this
                    );
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 整屏点击入口（根物体铺全屏的 Image 负责接收）：
    /// 淡入中或文本打印中忽略；打印完成后按“先切段、再换图”推进，
    /// 全部图片淡入且全部文本打印完成后走跳转/收尾。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isFading || isTyping)
        {
            return;
        }

        if (finished)
        {
            FinishAndClose();
            return;
        }

        // 尚未开始：淡入第一张并同时打印它的第一段文本
        if (activeImageIndex < 0)
        {
            StartImage(0);
            return;
        }

        string[] lines = GetLines(activeImageIndex);

        // 当前图片还有下一段文本：上一段消失，开始打印下一段
        if (activeLineIndex + 1 < lines.Length)
        {
            activeLineIndex++;
            StartTyping(lines[activeLineIndex]);
            return;
        }

        // 当前图片的文本全部打印完：进入下一张图片
        if (activeImageIndex + 1 < comicPanels.Count)
        {
            StartImage(activeImageIndex + 1);
            return;
        }

        // 最后一张图片的最后一段文本也打印完了：执行收尾
        //（正常情况下 TryMarkAllDone 已在完成时刻置位，这里兜底）
        if (!finished)
        {
            finished = true;
            onAllComicsShown?.Invoke();
        }

        FinishAndClose();
    }

    /// <summary>
    /// 重置到初始状态：全部图片透明、从头开始等待点击。
    /// 画布每次启用（含场景加载）时调用。
    /// </summary>
    private void ResetState()
    {
        activeImageIndex = -1;
        activeLineIndex = 0;
        finished = false;
        isTyping = false;
        isFading = false;
        typeCoroutine = null;
        fadeCoroutine = null;

        // 剔除已删除物体的空引用，避免点击时索引到 null
        comicPanels.RemoveAll(panel => panel == null);

        // 文本段落列表与图片列表按下标对齐
        PadPanelTexts();

        foreach (CanvasGroup panel in comicPanels)
        {
            if (panel != null)
            {
                SetAlpha(panel, 0f);
            }
        }

        if (storyText != null)
        {
            storyText.text = string.Empty;
        }

        // 未在打印时点击提示保持显示（打印期间由 StartTyping 隐藏），
        // 文本内容固定为“点击继续”
        if (clickHintText != null)
        {
            clickHintText.text = ClickHintContent;
            clickHintText.gameObject.SetActive(true);
        }
    }

    private IEnumerator FadeInPanel(int index)
    {
        isFading = true;
        CanvasGroup panel = comicPanels[index];

        // 用 unscaledDeltaTime：即使后续有系统暂停 Time.timeScale
        // （如设置面板），漫画淡入也不会被卡住
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(panel, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        SetAlpha(panel, 1f);
        fadeCoroutine = null;
        isFading = false;

        // 若这是最后一张且文本已全部打印完，标记整体完成
        TryMarkAllDone();
    }

    // 进入第 index 张图片：开始淡入，并同时打印该图的第一段文本
    //（没有配置文本时传空串：清掉上一张的文本且不进入打印状态）
    private void StartImage(int index)
    {
        if (comicPanels.Count == 0)
        {
            return;
        }

        activeImageIndex = index;
        activeLineIndex = 0;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeInPanel(index));

        string[] lines = GetLines(index);
        StartTyping(lines.Length > 0 ? lines[0] : string.Empty);
    }

    // 开始打印一段文本：清掉已显示的内容，打印期间隐藏点击提示，
    // 打印完成后恢复显示。空段落或未配置 Story Text 时视为
    // 立即打印完成，提示保持显示
    private void StartTyping(string line)
    {
        if (typeCoroutine != null)
        {
            StopCoroutine(typeCoroutine);
            typeCoroutine = null;
        }

        isTyping = false;

        if (storyText == null)
        {
            return;
        }

        storyText.text = string.Empty;

        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        SetHintVisible(false);
        isTyping = true;
        typeCoroutine = StartCoroutine(TypeLine(line));
    }

    // 逐字打印一段文本。用未缩放时间推进：设置面板把 timeScale
    // 暂停为 0 时打印不会卡住；完成后恢复点击提示并检查整体完成
    private IEnumerator TypeLine(string line)
    {
        float elapsed = 0f;
        int shownChars = 0;

        while (shownChars < line.Length)
        {
            elapsed += Time.unscaledDeltaTime;

            int nextChars = Mathf.Min(
                line.Length,
                Mathf.FloorToInt(elapsed * typeSpeed));

            if (nextChars != shownChars)
            {
                shownChars = nextChars;
                storyText.text = line.Substring(0, shownChars);
            }

            yield return null;
        }

        storyText.text = line;

        typeCoroutine = null;
        isTyping = false;

        SetHintVisible(true);
        TryMarkAllDone();
    }

    // 最后一张图片淡入完成、且它的全部段落都打印完毕时置位
    // finished 并触发完成事件；由淡入与打印两条收尾路径共同调用，
    // 先到的一方看到另一方未完成会直接返回。画布保持显示，等待
    // 玩家再点击一次执行 FinishAndClose 收尾
    private void TryMarkAllDone()
    {
        if (finished ||
            comicPanels.Count == 0 ||
            activeImageIndex != comicPanels.Count - 1 ||
            isFading ||
            isTyping)
        {
            return;
        }

        // 最后一张还有未打印的段落：不算完成，
        // 等玩家点击切到下一段后重新判定
        if (activeLineIndex + 1 < GetLines(activeImageIndex).Length)
        {
            return;
        }

        finished = true;
        onAllComicsShown?.Invoke();
    }

    // 收尾点击：按配置收起提示，然后跳转场景或隐藏画布
    private void FinishAndClose()
    {
        if (clickHintText != null && hideHintWhenFinished)
        {
            clickHintText.gameObject.SetActive(false);
        }

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneTransitionController.TransitionTo(nextSceneName);
        }
        else if (hideCanvasWhenFinished)
        {
            gameObject.SetActive(false);
        }
    }

    // 第 index 张图片配置的文本段落（列表缺项或未配置时为空）
    private string[] GetLines(int index)
    {
        if (index < 0 ||
            index >= panelTexts.Count ||
            panelTexts[index] == null ||
            panelTexts[index].lines == null)
        {
            return Array.Empty<string>();
        }

        return panelTexts[index].lines;
    }

    // 点击提示的显隐（未配置提示时不处理）
    private void SetHintVisible(bool visible)
    {
        if (clickHintText != null)
        {
            clickHintText.gameObject.SetActive(visible);
        }
    }

    // 文本段落列表与图片列表按下标对齐：缺的补空项，
    // 多出的保留但不会被读取
    private void PadPanelTexts()
    {
        while (panelTexts.Count < comicPanels.Count)
        {
            panelTexts.Add(new ComicPanelText());
        }
    }

    private static void SetAlpha(CanvasGroup panel, float alpha)
    {
        panel.alpha = alpha;
        // 透明时不参与射线检测，避免透明图挡住其他点击
        panel.blocksRaycasts = alpha > 0.99f;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器辅助：按子节点层级顺序自动填充漫画列表，
    /// 子物体上没有 Canvas Group 会自动补上。
    /// 想调整淡入顺序时，直接在 Hierarchy 中调整子物体顺序后重新执行。
    /// </summary>
    [ContextMenu("按子节点顺序自动填充列表")]
    private void AutoFillPanels()
    {
        comicPanels.Clear();
        foreach (Transform child in transform)
        {
            comicPanels.Add(child.gameObject.GetComponent<CanvasGroup>() ??
                child.gameObject.AddComponent<CanvasGroup>());
            }

            // 同步补齐文本段落列表，保持与图片列表下标对齐
            PadPanelTexts();

            UnityEditor.EditorUtility.SetDirty(this);
        }

    /// <summary>
    /// 编辑器辅助：一键搭建默认画布结构——根物体补全屏透明 Image、
    /// 创建 3 张默认漫画图（居中，位置后续手动摆）和右下角点击提示，
    /// 并自动填充列表。已有的同名物体不会重复创建。
    /// </summary>
    [ContextMenu("搭建默认画布结构")]
    private void BuildDefaultHierarchy()
    {
        // 根物体铺全屏透明 Image：接收整屏点击并挡住下层
        Image blocker = GetComponent<Image>();
        if (blocker == null)
        {
            blocker = gameObject.AddComponent<Image>();
        }
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;

        // 默认 3 张漫画图，居中 800x600，位置和贴图后续在检查器里调
        Transform container = transform.Find("ComicContainer");
        if (container == null)
        {
            GameObject containerGo = new GameObject(
                "ComicContainer", typeof(RectTransform));
            container = containerGo.transform;
            container.SetParent(transform, false);
            StretchFull((RectTransform)container);

            for (int i = 0; i < 3; i++)
            {
                GameObject panelGo = new GameObject(
                    "ComicImage_" + i, typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(container, false);

                RectTransform rect = (RectTransform)panelGo.transform;
                rect.sizeDelta = new Vector2(800f, 600f);
                rect.anchoredPosition = Vector2.zero;

                panelGo.GetComponent<Image>().raycastTarget = false;

                // 新建物体不会自动执行 RequireComponent 之外的事，
                // CanvasGroup 由自动填充逻辑统一补上
            }
        }

        // 右下角点击提示
        Transform hint = transform.Find("ClickHint");
        if (hint == null)
        {
            GameObject hintGo = new GameObject(
                "ClickHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(transform, false);

            RectTransform rect = (RectTransform)hintGo.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(300f, 50f);
            rect.anchoredPosition = new Vector2(-30f, 25f);

            TextMeshProUGUI text = hintGo.GetComponent<TextMeshProUGUI>();
            text.text = "点击继续";
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.Right;
            text.raycastTarget = false;
        }

        // 底部剧情文本（逐字打印的显示目标，铺满画面下方）
        Transform story = transform.Find("StoryText");
        if (story == null)
        {
            GameObject storyGo = new GameObject(
                "StoryText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            storyGo.transform.SetParent(transform, false);

            RectTransform storyRect = (RectTransform)storyGo.transform;
            storyRect.anchorMin = new Vector2(0f, 0f);
            storyRect.anchorMax = new Vector2(1f, 0f);
            storyRect.pivot = new Vector2(0.5f, 0f);
            storyRect.sizeDelta = new Vector2(-120f, 180f);
            storyRect.anchoredPosition = new Vector2(0f, 40f);

            TextMeshProUGUI storyLabel =
                storyGo.GetComponent<TextMeshProUGUI>();
            storyLabel.text = string.Empty;
            storyLabel.fontSize = 34f;
            storyLabel.alignment = TextAlignmentOptions.Center;
            storyLabel.raycastTarget = false;
        }

        // 列表为空时才自动填充，避免覆盖手动配置好的列表
        if (comicPanels.Count == 0)
        {
            AutoFillPanels();
        }

        // 把提示文本引用接到检查器上
        if (clickHintText == null)
        {
            clickHintText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // 底部剧情文本引用（按名字精确查找，避免误取到点击提示）
        if (storyText == null)
        {
            Transform storyTarget = transform.Find("StoryText");

            if (storyTarget != null)
            {
                storyText = storyTarget.GetComponent<TMP_Text>();
            }
        }

        PadPanelTexts();

        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
#endif
}
