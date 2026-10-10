using System;
using System.Collections;
using System.Collections.Generic;
using Migration.UI;
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
/// 每次有效点击（推进段落、换图或收尾）同时播放点击音效
/// （Click Sound，留空则静音）；淡入中、打印中被忽略的点击不发声。
///
/// 暂停：画布显示期间游戏时间暂停（Time.timeScale = 0），世界完全
/// 冻结，玩家无法移动、敌人与定时逻辑都不推进；淡入、逐字打印与
/// 收尾溶解全部走未缩放时间，暂停下照常播放。画布关闭时把时间流
/// 归位到 1，恢复玩法。
///
/// 使用方式：
/// 1. 在目标场景新建 Canvas（Screen Space - Overlay），挂本脚本；
/// 2. 根物体需要一个铺满全屏的 Image（Raycast Target 必须勾选），
///    用于接收整屏点击并挡住下层 UI/游戏世界。收尾要溶解的那张
///    全屏黑屏有两种摆法：
///    a) 就用根物体这张 Image 当黑屏（颜色设为不透明黑色），
///       溶解组件挂在根物体上；
///    b) 黑屏单独做一张铺满全屏的黑色 Image 放在画布下，层级要
///       排在漫画图之前（否则会盖住漫画），溶解组件挂它上面；
///       此时根物体的 Image 会在收尾时被清成完全透明，避免它把
///       溶解后露出的画面重新盖黑；
/// 3. 在画布下摆放若干漫画图片子物体（位置、大小随意摆），
///    每张图上需要一个 Canvas Group（可用右键菜单自动添加并填充列表）；
/// 4. 检查器里 Comic Panels 列表的顺序即点击时的淡入顺序，
///    数量、位置、顺序全部在检查器中配置。
///
/// 播完后：画布保持显示，玩家再点击一次执行收尾——所有漫画图片与
/// 剧情文本立即消失，随后那张铺满全屏的黑色 Image 经
/// UIDissolveImage 溶解消失，露出下面的游戏画面（溶解时长用该
/// 组件上配置的 Duration）。溶解结束后才落定：填写了 Next Scene
/// Name 时经 SceneTransitionController.TransitionTo 跳转（走全局
/// 黑屏加载过渡），否则按 Hide Canvas When Finished 隐藏画布。
/// 画布显示期间暴露 static IsOpen，供其他面板的互斥门控检查。
///
/// 光标：显示期间强制解锁并显示光标（场景加载时本画布 OnEnable
/// 早于 PlayerController.Start 执行，一次性解锁会被它随后的锁定
/// 覆盖，因此 Update 中每帧兜底保持解锁）；同时锁住玩家控制，
/// 让鼠标只用于点漫画、不带动视角。画布关闭时若场景中存在
/// PlayerController，则恢复锁定与控制。
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

    [Header("音效")]

    [Tooltip("点击音效：仅在有效点击（推进下一张图/下一段文本、" +
        "或最后一次收尾点击）时播放；淡入中、逐字打印中被忽略的" +
        "点击不播放。留空则全程静音")]
    [SerializeField]
    private AudioClip clickSound;

    [Tooltip("点击音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float clickSoundVolume = 1f;

    [Header("收尾")]

    [Tooltip("收尾时溶解消失的那张全屏黑屏上的溶解组件（UIDissolveImage）。" +
        "黑屏可以就是根物体自己的 Image，也可以是画布下一个单独铺满" +
        "全屏的黑色 Image。最后一次点击后先把漫画图与剧情文本全部" +
        "隐藏，再用它把黑屏溶解掉露出游戏画面，溶解结束才执行跳转/" +
        "隐藏画布。组件的 Material 需指向 Assets/Materials/UI Dissolve.mat。" +
        "留空时自动查找：优先根物体，其次画布子物体里面积最大的一个" +
        "（自动排除漫画图）")]
    [SerializeField]
    private UIDissolveImage blackScreenDissolve;

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

    // 正在收尾（漫画已全部隐藏、黑屏溶解中）时为 true，
    // 期间忽略点击，避免重复触发收尾
    private bool isEnding;

    private Coroutine fadeCoroutine;
    private Coroutine typeCoroutine;
    private Coroutine endCoroutine;

    // 打印完成后点击提示显示的文本内容
    private const string ClickHintContent = "点击继续";

    private void OnEnable()
    {
        IsOpen = true;
        ResetState();

        // 立即解锁并显示光标：本画布需要接收整屏点击
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 暂停游戏时间：漫画播放期间世界完全冻结（淡入、逐字打印
        // 与收尾溶解都走未缩放时间，暂停下照常推进）
        Time.timeScale = 0f;
    }

    // 场景加载时本画布 OnEnable 早于 PlayerController.Start 执行，
    // 上面临时解锁的光标会被它随后的 Start 锁定覆盖；画布显示期间
    // 每帧兜底保持解锁可见，直到画布关闭。
    // 时间流同理：本画布在场景过渡的加载流程中启用，过渡收尾时
    // 会把 timeScale 归位到 1，暂停需每帧兜底保持
    private void Update()
    {
        if (Time.timeScale != 0f)
        {
            Time.timeScale = 0f;
        }

        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }

        // 锁住玩家控制：视角旋转与开火输入都在玩家/武器组件内部直接
        // 读取，只解锁光标挡不住鼠标（输入不受 timeScale 影响），
        // 漫画期间点鼠标会把视角带偏。这里同样每帧兜底——本画布在
        // 场景加载流程中启用，第一帧未必已经找得到玩家
        TrainingGroundControlLock.Lock();

        // 收尾溶解期间的射线同步（与提示/任务面板的溶解遮罩一致）：
        // 溶解中黑屏继续拦截点击，完全溶解后放行。画布若因配置保持
        // 启用（Hide Canvas When Finished 关闭），也不会留下一个
        // 看不见却吃掉点击的全屏 Image
        if (isEnding &&
            blackScreenDissolve != null &&
            blackScreenDissolve.graphic != null)
        {
            bool shouldBlockRaycast = !blackScreenDissolve.isHideComplete;

            SetRaycastTarget(
                blackScreenDissolve.graphic,
                shouldBlockRaycast
            );

            // 黑屏是子物体时，根物体那张全屏 Image 只是挡点击的
            // 挡板，溶解完成后同样放行
            Image blocker = GetComponent<Image>();

            if (blocker != null && blocker != blackScreenDissolve.graphic)
            {
                SetRaycastTarget(blocker, shouldBlockRaycast);
            }
        }
    }

    private static void SetRaycastTarget(Graphic graphic, bool value)
    {
        if (graphic != null && graphic.raycastTarget != value)
        {
            graphic.raycastTarget = value;
        }
    }

    private void OnDisable()
    {
        IsOpen = false;

        // 恢复玩家控制（视角/开火/拾取），回到正常玩法
        TrainingGroundControlLock.Restore();

        // 恢复时间流：直接归位到 1，而不是还原暂停前的存值——
        // 本画布是在场景过渡的加载流程里启用的，那时 timeScale 正是
        // 过渡自己写入的 0，照存值还原会把新场景永久卡在暂停里
        Time.timeScale = 1f;

        // 收尾后回到玩法：场景里有玩家控制器时恢复光标锁定
        //（纯 UI 场景没有 PlayerController，保持解锁避免光标卡死）
        if (FindObjectOfType<PlayerController>() != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        // 画布中途被外部禁用时，停掉残留的淡入/打印/收尾协程
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

        if (endCoroutine != null)
        {
            StopCoroutine(endCoroutine);
            endCoroutine = null;
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

        // 收尾的黑屏溶解找不到组件时，会在收尾那一刻报错说明配置，
        // 这里不再重复检查（自动查找还需要等布局算完才准确）；
        // 但要揪出"黑屏被当成漫画图"的配置错误：它会多出一页点击，
        // 收尾时还会被 HideAllComics 跳过而保持显示
        foreach (CanvasGroup panel in comicPanels)
        {
            if (panel == null)
            {
                continue;
            }

            if (panel.GetComponentInChildren<UIDissolveImage>(true) != null)
            {
                Debug.LogWarning(
                    "ComicIntroController：Comic Panels 里的 '" +
                        panel.name + "' 上挂了 UIDissolveImage" +
                        "（收尾溶解用的黑屏），它会被当成一页漫画参与" +
                        "点击推进。建议把黑屏从这个列表里移除。",
                    panel
                );
                break;
            }
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
    /// 全部图片淡入且全部文本打印完成后进入收尾：漫画图与文本先
    /// 消失，再把全屏黑屏溶解掉。被忽略的点击不发音效，只有真正
    /// 生效的这一类点击才播放。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isFading || isTyping || isEnding)
        {
            return;
        }

        // 走到这里的都是有效点击（推进剧情或收尾），播放点击音效
        PlayClickSound();

        if (finished)
        {
            StartEnding();
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
        //（正常情况下 TryMarkAllDone 已在完成时刻置位，这里兜底）。
        // 必须走 StartEnding——直接 FinishAndClose 会跳过黑屏溶解
        if (!finished)
        {
            finished = true;
            onAllComicsShown?.Invoke();
        }

        StartEnding();
    }

    // 播放点击音效（和其他 UI 按钮一致的做法）：用独立的临时物体
    // 承载音源。收尾点击可能直接隐藏画布或触发场景过渡，挂在画布上
    // 的音源会被一起掐断，临时物体能独立存活到音效播完
    private void PlayClickSound()
    {
        if (clickSound == null)
        {
            return;
        }

        GameObject soundHost = new GameObject("ClickSound");
        AudioSource source = soundHost.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        // 收尾点击可能触发场景过渡（过渡即暂停音频），
        // 点击音效要能穿过黑屏播完
        source.ignoreListenerPause = true;

        source.volume = clickSoundVolume;
        source.PlayOneShot(clickSound);
        Destroy(soundHost, clickSound.length);
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
        isEnding = false;
        typeCoroutine = null;
        fadeCoroutine = null;
        endCoroutine = null;

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

    // 收尾流程：最后一次点击后，所有漫画图与剧情文本立即消失，
    // 随后把那张铺满全屏的黑色 Image 溶解掉，露出下面的游戏画面；
    // 溶解结束后才落定（跳转场景或隐藏画布）
    private void StartEnding()
    {
        if (isEnding)
        {
            return;
        }

        isEnding = true;

        bool autoPicked = blackScreenDissolve == null;
        UIDissolveImage dissolve = ResolveBlackScreenDissolve();

        if (dissolve != null)
        {
            blackScreenDissolve = dissolve;
        }

        // 先解析黑屏再隐藏漫画：HideAllComics 要跳过黑屏本身
        HideAllComics();

        // 找不到溶解组件：收尾仍会走完（跳转/隐藏画布），但黑屏是
        // 瞬间消失而不是溶解——报错说明怎么配，避免"看不出问题"
        if (dissolve == null)
        {
            Debug.LogError(
                "ComicIntroController：画布上找不到 UIDissolveImage，" +
                    "收尾时黑屏不会溶解消失。请在铺满全屏的黑色 Image 上" +
                    "挂 UIDissolveImage（材质用 " +
                    "Assets/Materials/UI Dissolve.mat），" +
                    "再拖到 Black Screen Dissolve 字段上。",
                this
            );

            FinishAndClose();
            return;
        }

        if (autoPicked)
        {
            Debug.Log(
                "ComicIntroController：未指定 Black Screen Dissolve，" +
                    "收尾自动使用 '" + dissolve.name + "' 上的溶解组件。",
                dissolve
            );
        }

        // 溶解要看得见，依赖三件事：组件启用（否则不写 uv1 数据）、
        // 材质用的是溶解 Shader、Duration 大于 0。逐条兜底并把实际
        // 状态打进日志——这个流程一次只播一遍，静默失败很难排查
        dissolve.enabled = true;
        dissolve.gameObject.SetActive(true);

        Graphic blackScreen = dissolve.graphic;
        Material material = blackScreen != null ? blackScreen.material : null;

        if (!IsDissolveMaterial(material))
        {
            // 黑屏上没配溶解材质时，借用场景里其他溶解遮罩正在用的
            // 那一份（同一份 Assets/Materials/UI Dissolve.mat 资产）
            Material borrowed = FindSceneDissolveMaterial();

            if (borrowed != null && blackScreen != null)
            {
                blackScreen.material = borrowed;
                material = borrowed;

                Debug.LogWarning(
                    "ComicIntroController：黑屏 '" + dissolve.name +
                        "' 上没有溶解材质，已临时借用场景中其他溶解遮罩" +
                        "用的材质。建议把 Assets/Materials/UI Dissolve.mat " +
                        "填到该 Image 的 Material 槽上。",
                    dissolve
                );
            }
        }

        // 几何兜底必须排在"清掉根物体黑屏"之前：黑屏没盖住画布时，
        // 先清根物体会让游戏画面提前闪出来
        EnsureBlackScreenCoversCanvas(blackScreen);

        Debug.Log(
            "ComicIntroController 收尾：黑屏 = '" + dissolve.name + "'" +
                (autoPicked ? "（自动查找）" : "（检查器指定）") +
                "，组件启用 = " + dissolve.enabled +
                "，物件激活 = " + dissolve.gameObject.activeInHierarchy +
                "，Shader = " +
                (material != null && material.shader != null
                    ? material.shader.name
                    : "<无材质>") +
                "，Location = " + dissolve.location.ToString("0.00") +
                "，Duration = " + dissolve.duration.ToString("0.00") + " 秒" +
                "，尺寸 = " + DescribeRect(blackScreen),
            dissolve
        );

        if (!IsDissolveMaterial(material))
        {
            Debug.LogWarning(
                "ComicIntroController：黑屏上没有使用溶解 Shader 的材质" +
                    "（当前 Shader = " +
                    (material != null && material.shader != null
                        ? material.shader.name
                        : "<无材质>") +
                    "），收尾看不到溶解效果。请把 " +
                    "Assets/Materials/UI Dissolve.mat 填到黑屏 Image 的 " +
                    "Material 槽（或溶解组件的 Effect Material 字段）上。",
                dissolve
            );
        }

        if (dissolve.duration <= 0f)
        {
            Debug.LogWarning(
                "ComicIntroController：黑屏溶解组件的 Duration 为 0，" +
                    "溶解会瞬间完成（看起来就是直接消失）。" +
                    "请把它设为大于 0 的值。",
                dissolve
            );
        }

        // 黑屏若是单独的子物体，根物体那张全屏 Image 就只剩"挡点击"
        // 的作用：清成完全透明，否则它会把子物体溶解后露出的画面
        // 重新盖成黑的，看起来就是"没有溶解"
        Image blocker = GetComponent<Image>();

        if (blocker != null &&
            blackScreen != null &&
            blackScreen != blocker)
        {
            Color blockerColor = blocker.color;
            blockerColor.a = 0f;
            blocker.color = blockerColor;
        }

        // 与提示面板的遮罩一致：先瞬时拉回完全显示再开始溶解。
        // 组件上的 Start State / Location 可能是别的配置（照抄遮罩
        // 做法时常见 Start State = Hidden），那样 Hide() 会立刻
        // 完成，同样表现为"瞬间消失"
        dissolve.SetVisible(true, true);

        // 溶解动画走未缩放时间（组件上的 Use Unscaled Time），
        // 暂停下照常播放；完毕后由协程收尾
        dissolve.Hide();
        endCoroutine = StartCoroutine(WaitBlackScreenDissolve());
    }

    // 材质是否用了溶解 Shader（Shader 名里带 Dissolve，
    // 工程里 UI Dissolve / 各面板遮罩用的都是这一类）
    private static bool IsDissolveMaterial(Material material)
    {
        return material != null &&
            material.shader != null &&
            material.shader.name.Contains("Dissolve");
    }

    // 黑屏的矩形必须真的盖住整个画布，否则它一个像素都画不出来：
    // Image 的宽或高为负/为 0 时 Graphic 直接不生成网格（常见来源是
    // 在 RectTransform 上只点了锚点预设、没同时按住 Shift+Alt 归零
    // Left/Top/Right/Bottom，于是 stretch 锚点配着一大堆偏移量）。
    // 这样收尾时黑屏是"隐形"的，看到的就只有游戏画面突然出现。
    // 这里把没盖住画布的黑屏临时拉成全屏并给出提示
    private void EnsureBlackScreenCoversCanvas(Graphic blackScreen)
    {
        if (blackScreen == null)
        {
            return;
        }

        RectTransform rect = blackScreen.rectTransform;
        Canvas canvas = blackScreen.canvas;

        if (rect == null || canvas == null)
        {
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect == null)
        {
            return;
        }

        Rect own = rect.rect;
        Rect area = canvasRect.rect;

        bool covers =
            Mathf.Abs(own.width) >= Mathf.Abs(area.width) - 0.5f &&
            Mathf.Abs(own.height) >= Mathf.Abs(area.height) - 0.5f;

        if (covers)
        {
            return;
        }

        Debug.LogWarning(
            "ComicIntroController：黑屏 '" + blackScreen.name +
                "' 的尺寸没有盖住画布（当前 " + DescribeRect(blackScreen) +
                "，画布 " + area.width.ToString("0") + " x " +
                area.height.ToString("0") + "）。宽或高为负/为 0 时 Image " +
                "根本不画，收尾就看不到溶解。已临时把它拉成全屏；" +
                "建议在场景里选中它，按住 Shift+Alt 点 RectTransform 的 " +
                "stretch-stretch 预设（或把 Left/Top/Right/Bottom、Pos X/Y " +
                "全设为 0）。",
            blackScreen
        );

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 图形当前的宽高（用于诊断日志）
    private static string DescribeRect(Graphic graphic)
    {
        if (graphic == null || graphic.rectTransform == null)
        {
            return "<无>";
        }

        Rect rect = graphic.rectTransform.rect;

        return rect.width.ToString("0") + " x " + rect.height.ToString("0");
    }

    // 场景里其他溶解遮罩正在用的溶解材质（不含预制体资产）
    private static Material FindSceneDissolveMaterial()
    {
        foreach (UIDissolveImage other in
            Resources.FindObjectsOfTypeAll<UIDissolveImage>())
        {
            if (other == null ||
                other.graphic == null ||
                !other.gameObject.scene.IsValid())
            {
                continue;
            }

            Material material = other.graphic.material;

            if (IsDissolveMaterial(material))
            {
                return material;
            }
        }

        return null;
    }

    // 解析收尾要溶解的黑屏组件：
    // 1. 检查器里指定的优先；
    // 2. 其次取根物体上的（黑屏就是根物体那张全屏 Image 的摆法）；
    // 3. 再退到画布子物体里面积最大的一个（黑屏是单独一张全屏
    //    Image 的摆法），漫画图自身挂的溶解组件排除在外
    private UIDissolveImage ResolveBlackScreenDissolve()
    {
        if (blackScreenDissolve != null)
        {
            return blackScreenDissolve;
        }

        UIDissolveImage onSelf = GetComponent<UIDissolveImage>();

        if (onSelf != null)
        {
            return onSelf;
        }

        UIDissolveImage largest = null;
        float largestArea = 0f;

        foreach (UIDissolveImage candidate in
            GetComponentsInChildren<UIDissolveImage>(true))
        {
            if (candidate.graphic == null ||
                IsComicPanelGraphic(candidate.graphic))
            {
                continue;
            }

            Rect rect = candidate.graphic.rectTransform.rect;
            float area = Mathf.Abs(rect.width) * Mathf.Abs(rect.height);

            if (largest == null || area > largestArea)
            {
                largest = candidate;
                largestArea = area;
            }
        }

        return largest;
    }

    // 该 Graphic 是否属于漫画图片（Comic Panels 里的某项）：
    // 自动查找黑屏时用来排除漫画图自己挂的溶解组件
    private bool IsComicPanelGraphic(Graphic graphic)
    {
        foreach (CanvasGroup panel in comicPanels)
        {
            if (panel == null)
            {
                continue;
            }

            if (graphic.transform == panel.transform ||
                graphic.transform.IsChildOf(panel.transform))
            {
                return true;
            }
        }

        return false;
    }

    // 等黑屏溶解完毕再收尾。组件被外部销毁时直接放行，不会卡住
    private IEnumerator WaitBlackScreenDissolve()
    {
        yield return new WaitUntil(() =>
            blackScreenDissolve == null ||
            blackScreenDissolve.isHideComplete);

        endCoroutine = null;
        FinishAndClose();
    }

    // 收尾时让所有漫画图与文本立即消失：图片直接置为完全透明并
    // 停止拦截射线，剧情文本清空；点击提示按 Hide Hint When
    // Finished 的决定去留（默认收起）。
    // 黑屏本身必须跳过——它就是待溶解的背景，跟着一起变透明就
    // 看不到溶解了（黑屏被误加进 Comic Panels 时靠这里兜住）
    private void HideAllComics()
    {
        Graphic blackScreen = blackScreenDissolve != null
            ? blackScreenDissolve.graphic
            : null;

        foreach (CanvasGroup panel in comicPanels)
        {
            if (panel == null || IsBlackScreenPanel(panel, blackScreen))
            {
                continue;
            }

            SetAlpha(panel, 0f);
        }

        if (storyText != null)
        {
            storyText.text = string.Empty;
        }

        if (hideHintWhenFinished)
        {
            SetHintVisible(false);
        }
    }

    // 该 Comic Panels 项是否就是黑屏（或黑屏所在的父物体）
    private static bool IsBlackScreenPanel(
        CanvasGroup panel,
        Graphic blackScreen)
    {
        if (blackScreen == null)
        {
            return false;
        }

        return blackScreen.transform == panel.transform ||
            blackScreen.transform.IsChildOf(panel.transform);
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
    /// 编辑器辅助：一键搭建默认画布结构——根物体补全屏黑屏 Image、
    /// 挂上收尾用的溶解组件、创建 3 张默认漫画图（居中，位置后续
    /// 手动摆）和右下角点击提示，并自动填充列表。
    /// 已有的同名物体不会重复创建。
    /// </summary>
    [ContextMenu("搭建默认画布结构")]
    private void BuildDefaultHierarchy()
    {
        // 根物体铺全屏黑屏 Image：接收整屏点击、挡住下层，
        // 收尾时由 UIDissolveImage 溶解消失露出游戏画面
        Image blocker = GetComponent<Image>();
        if (blocker == null)
        {
            blocker = gameObject.AddComponent<Image>();
        }
        blocker.color = new Color(0f, 0f, 0f, 1f);
        blocker.raycastTarget = true;

        AttachBlackScreenDissolve();

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

    /// <summary>
    /// 编辑器辅助：给根物体那张铺满全屏的黑色 Image 挂上
    /// UIDissolveImage（收尾时用来把黑屏溶解掉）并补上溶解材质，
    /// 顺带接好 Black Screen Dissolve 引用。已存在的组件、已指定的
    /// 材质都不会被覆盖，可对已有画布直接执行。
    /// </summary>
    [ContextMenu("给黑屏挂上溶解组件（收尾用）")]
    private void AttachBlackScreenDissolve()
    {
        UIDissolveImage dissolve = GetComponent<UIDissolveImage>();
        if (dissolve == null)
        {
            dissolve = gameObject.AddComponent<UIDissolveImage>();
        }

        // 材质只在未指定时补上，避免覆盖手调配置
        UnityEditor.SerializedObject dissolveData =
            new UnityEditor.SerializedObject(dissolve);
        UnityEditor.SerializedProperty materialProperty =
            dissolveData.FindProperty("m_EffectMaterial");

        if (materialProperty != null &&
            materialProperty.objectReferenceValue == null)
        {
            Material dissolveMaterial =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Materials/UI Dissolve.mat");

            if (dissolveMaterial != null)
            {
                materialProperty.objectReferenceValue = dissolveMaterial;
                dissolveData.ApplyModifiedProperties();
            }
        }

        blackScreenDissolve = dissolve;
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
