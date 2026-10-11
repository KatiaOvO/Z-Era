using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 玩家自白 HUD：挂在 HUD Canvas 下的自白物体上（建议直接挂在
/// 一个 TMP_Text 物体上，文本留空则自动取同物体/子物体上的组件）。
/// 由 MonologueTrigger 通过 Show(asset) 调用：按资产配置摆好
/// 屏幕位置，以打字机形式打印正文，停留指定时长后整体淡出。
/// 可见性只靠 CanvasGroup 透明度控制（缺省运行时自动添加），
/// 物体常驻启用，保证 Instance 在场景加载后即可用。
/// 打字、停留、淡出均使用未缩放时间，暂停面板打开时照常走完。
/// </summary>
public class MonologueUIController : MonoBehaviour
{
    public static MonologueUIController Instance { get; private set; }

    [Tooltip("自白正文文本，留空则自动取同物体或子物体上的 TMP_Text")]
    [SerializeField]
    private TMP_Text monologueText;

    [Tooltip("整体淡出的 CanvasGroup，留空则运行时自动添加")]
    [SerializeField]
    private CanvasGroup canvasGroup;

    private Coroutine displayRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (monologueText == null)
        {
            monologueText = GetComponent<TMP_Text>();

            if (monologueText == null)
            {
                monologueText =
                    GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (monologueText == null)
        {
            Debug.LogError(
                "MonologueUIController：找不到 TMP_Text，" +
                    "自白无法显示。",
                this
            );
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        ResetDisplay();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        // 画布整体被隐藏（如对话开始时关闭 HUD）时收尾，
        // 避免恢复后残留半透明的旧文本。
        ResetDisplay();
    }

    /// <summary>
    /// 显示一段自白。正在显示时新自白会立即顶掉旧的。
    /// </summary>
    public static void Show(MonologueAsset asset)
    {
        if (asset == null)
        {
            return;
        }

        if (Instance == null)
        {
            Debug.LogWarning(
                "MonologueUIController：场景中没有自白 UI" +
                    "（HUD Canvas 下缺 MonologueUIController），" +
                    "无法显示自白。"
            );
            return;
        }

        Instance.BeginShow(asset);
    }

    private void BeginShow(MonologueAsset asset)
    {
        if (monologueText == null)
        {
            return;
        }

        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        ApplyLayout(asset);

        monologueText.text = asset.Text;
        monologueText.maxVisibleCharacters = 0;
        canvasGroup.alpha = 1f;

        // 自白开始显示即写入桥接标记，供后续自白/对话联动。
        if (asset.FlagsToSetOnShown != null)
        {
            foreach (string flag in asset.FlagsToSetOnShown)
            {
                DialogueFlags.SetFlag(flag, true);
            }
        }

        displayRoutine = StartCoroutine(DisplayRoutine(asset));
    }

    // 把资产的屏幕位置配置套到文本的 RectTransform 上。
    private void ApplyLayout(MonologueAsset asset)
    {
        RectTransform rect =
            monologueText.rectTransform;

        rect.anchorMin = asset.AnchorMin;
        rect.anchorMax = asset.AnchorMax;
        rect.pivot = asset.Pivot;
        rect.anchoredPosition = asset.AnchoredPosition;
        rect.sizeDelta = asset.SizeDelta;
    }

    private IEnumerator DisplayRoutine(MonologueAsset asset)
    {
        // 打字机：maxVisibleCharacters 只统计可见字符，
        // 富文本标签不会被当作字符打印出来
        //（与 DialogueUIController 相同的数法）。
        string fullText = asset.Text;
        int total = fullText.Length;
        int revealed = 0;
        float revealProgress = 0f;

        while (revealed < total)
        {
            revealProgress +=
                asset.CharactersPerSecond * Time.unscaledDeltaTime;

            int target = Mathf.Min(
                total,
                Mathf.FloorToInt(revealProgress)
            );

            if (target > revealed)
            {
                monologueText.maxVisibleCharacters = target;
                revealed = target;
            }

            yield return null;
        }

        // 停留：打印完毕后保持完整显示。
        float holdEnd = Time.unscaledTime + asset.HoldDuration;

        while (Time.unscaledTime < holdEnd)
        {
            yield return null;
        }

        // 淡出：透明度整体渐变到 0 后清空文本。
        float fadeDuration = asset.FadeDuration;

        if (fadeDuration > 0f)
        {
            for (float elapsed = 0f;
                 elapsed < fadeDuration;
                 elapsed += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha =
                    1f - elapsed / fadeDuration;

                yield return null;
            }
        }

        ResetDisplay();
    }

    // 清空文本与透明度，回到待机状态。
    private void ResetDisplay()
    {
        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        if (monologueText != null)
        {
            monologueText.text = string.Empty;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
}
