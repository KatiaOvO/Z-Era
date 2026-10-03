using System.Collections;
using Migration.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 全局场景过渡服务：挂在 SwitchScene Canvas 上（单例，跨场景复用）。
/// TransitionTo(场景名) 的完整流程：
/// 1. 复位状态（黑屏拉回完全溶解、进度条清零）；
/// 2. 黑屏溶解铺满全屏（本脚本上的 Fade Duration 控制时长）；
/// 3. 异步加载目标场景（allowSceneActivation = false 手动控制激活），
///    Filled Image 进度条按 progress / 0.9 映射显示加载进度；
/// 4. 进度条走满后放行场景激活，等一帧让新场景的 RenderSettings
///    生效，再 DynamicGI.UpdateEnvironment() 基于新天空盒重建环境
///    光照（运行时切换场景不会自动重算，不重算会出现明暗异常）；
/// 5. 黑屏溶解消失，Canvas 保持激活待命，等待下一次过渡。
/// 单例保证跨场景只有一份：场景重新加载带来的重复副本在 Awake 中
/// 销毁；本物体通过 DontDestroyOnLoad 跨场景存活。
/// </summary>
public class SceneTransitionController : MonoBehaviour
{
    private static SceneTransitionController instance;

    // 全局过渡入口：任何场景的任何脚本都可以直接调用。
    // 场景中没有控制器或正在过渡时不做任何事
    public static void TransitionTo(string sceneName)
    {
        if (instance == null)
        {
            instance = FindObjectOfType<SceneTransitionController>();
        }

        if (instance == null)
        {
            Debug.LogError(
                "场景中不存在 SceneTransitionController，无法跳转到场景：" +
                sceneName
            );
            return;
        }

        instance.StartTransition(sceneName);
    }

    [Header("引用")]

    [Tooltip("铺满全屏的黑色溶解图像（挂载 UI Dissolve Image 并指定溶解" +
        "材质，Start State = Hidden，Raycast Target 保持不勾选）")]
    [SerializeField]
    private UIDissolveImage fadeImage;

    [Tooltip("进度条填充 Image（Type = Filled），层级在黑屏之上")]
    [SerializeField]
    private Image progressFill;

    [Tooltip("进度百分比文本（如“47%”），随进度条同步更新")]
    [SerializeField]
    private TMP_Text progressPercentageText;

    [Tooltip("加载提示文本（技巧/剧情/说明等），加载阶段显示")]
    [SerializeField]
    private TMP_Text loadingTipsText;

    [Tooltip("加载提示内容池，每次过渡随机显示其中一条；" +
        "后续随开发在检查器里继续添加即可")]
    [SerializeField]
    private string[] loadingTips;

    [Tooltip("加载图标（ProgressGroup 下），加载阶段绕自身中心旋转；" +
        "图标的 Pivot 需在中心，否则会表现为公转")]
    [SerializeField]
    private RectTransform loadingIcon;

    [Tooltip("加载图标的旋转速度（度/秒），正值顺时针、负值逆时针")]
    [SerializeField, Range(-360f, 360f)]
    private float loadingIconSpinSpeed = 180f;

    [Tooltip("进度条所在容器（加载阶段显示、其余时间隐藏），留空则不控制显隐")]
    [SerializeField]
    private GameObject progressGroup;

    [Header("参数")]

    [Tooltip("黑屏溶解铺满全屏的时长（秒），会覆盖黑屏上 UIDissolveImage 自己的 Duration")]
    [SerializeField, Min(0.01f)]
    private float fadeDuration = 1f;

    [Tooltip("进度条走满后到黑屏开始溶解消失的停留时间（秒）")]
    [SerializeField, Min(0f)]
    private float fullBarHoldDuration = 0.2f;

    [Tooltip("进度条最少展示时长（秒）：场景加载很快时，进度条也会" +
        "用该时长平滑走满，而不是瞬间跳满")]
    [SerializeField, Min(0f)]
    private float minBarDuration = 1.5f;

    private bool isTransitioning;

    // 当前显示的提示在提示池中的索引（-1 表示尚未显示），
    // 点击切换提示时用于排除当前条
    private int currentTipIndex = -1;

    private void Awake()
    {
        // 单例：场景重新加载带来的重复副本直接销毁
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // 跨场景存活：从本场景开始，之后所有场景互跳都复用本 Canvas
        DontDestroyOnLoad(gameObject);

        // 待命状态下黑屏不参与射线检测，不影响正常鼠标点击；
        // 切换开始（溶解出现）时由过渡流程勾选，溶解消失后再取消
        if (fadeImage != null && fadeImage.graphic != null)
        {
            fadeImage.graphic.raycastTarget = false;
        }
    }

    private void StartTransition(string sceneName)
    {
        if (isTransitioning)
        {
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning(
                "目标场景名为空，忽略本次场景过渡。",
                this
            );
            return;
        }

        if (fadeImage == null)
        {
            Debug.LogError(
                "黑屏溶解图像（Fade Image）未赋值，无法进行场景过渡。",
                this
            );
            return;
        }

        isTransitioning = true;

        StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        // 复位状态：黑屏瞬时拉回完全溶解（防止上一次的残留进度），
        // 射线遮挡打开（溶解开始出现即勾选，切换过程中限制点击），
        // 进度条清零并隐藏
        fadeImage.gameObject.SetActive(true);

        if (fadeImage.graphic != null)
        {
            fadeImage.graphic.raycastTarget = true;
        }

        float displayedFill = 0f;
        UpdateProgressDisplay(displayedFill);

        if (progressGroup != null)
        {
            progressGroup.SetActive(false);
        }

        // 用本脚本上的时长覆盖黑屏组件自己的 Duration，
        // 溶解节奏统一由本控制器控制
        fadeImage.duration = fadeDuration;
        fadeImage.SetVisible(false, true);
        fadeImage.Show();

        yield return new WaitUntil(() => fadeImage.isShowComplete);

        // 先显示加载界面（0% 进度条 + 提示 + 图标），再发起加载：
        // LoadSceneAsync 的调用瞬间有一次性同步开销（编辑器下可达
        // 1-2 秒），无法消除只能安排在被黑屏盖住的位置——让卡顿
        // 发生时屏幕上已经是加载界面，读作"正在加载"而不是死机
        if (progressGroup != null)
        {
            progressGroup.SetActive(true);
        }

        // 每次过渡从提示池中随机取一条显示
        ShowRandomTip();

        // 异步加载：加载到 90% 后暂停等待激活，激活由本脚本放行，
        // 进度条按 progress / 0.9 映射到 0~100%
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName);

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            displayedFill = AdvanceProgressFill(
                displayedFill,
                Mathf.Clamp01(operation.progress / 0.9f)
            );

            UpdateProgressDisplay(displayedFill);

            yield return null;
        }

        // 场景加载完成后，进度条剩余部分按最少展示时长平滑走满，
        // 避免小场景瞬间加载导致进度条直接跳满
        while (displayedFill < 1f)
        {
            displayedFill = AdvanceProgressFill(displayedFill, 1f);

            UpdateProgressDisplay(displayedFill);

            yield return null;
        }

        UpdateProgressDisplay(1f);

        if (fullBarHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                fullBarHoldDuration
            );
        }

        // 放行场景激活（激活可能卡一两帧，但被黑屏盖住不可见）
        operation.allowSceneActivation = true;

        // 等待加载操作真正完成（新场景全部激活、旧场景销毁）。
        // 放行后操作还需要若干帧才走完剩余的 10%，只等一帧就
        // 开始溶解黑屏的话，会把尚未销毁的旧场景露出来
        yield return new WaitUntil(() => operation.isDone);

        // 再等一帧让新场景与它的 RenderSettings（天空盒等）生效
        yield return null;

        DynamicGI.UpdateEnvironment();

        // 进度条隐藏，黑屏溶解消失露出新场景
        if (progressGroup != null)
        {
            progressGroup.SetActive(false);
        }

        fadeImage.Hide();

        yield return new WaitUntil(() => fadeImage.isHideComplete);

        if (fadeImage.graphic != null)
        {
            fadeImage.graphic.raycastTarget = false;
        }

        isTransitioning = false;
    }

    // 加载图标旋转与提示切换：仅在加载阶段（进度条容器显示时）生效。
    // 旋转使用未缩放时间，与溶解动画的节奏保持一致；
    // 点击画面任意位置（无论鼠标是否锁定）切换到另一条提示
    private void Update()
    {
        bool loadingPhase = progressGroup == null ||
            progressGroup.activeSelf;

        if (!loadingPhase)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            ShowRandomTip();
        }

        if (loadingIcon != null)
        {
            loadingIcon.Rotate(
                0f,
                0f,
                -loadingIconSpinSpeed * Time.unscaledDeltaTime
            );
        }
    }

    // 从提示池随机取一条写入提示文本；连续切换时排除当前条，
    // 不会连续显示同一条（池中仅一条时保持不变）。
    // 提示池为空时清空文本，避免残留上一次的内容
    private void ShowRandomTip()
    {
        if (loadingTipsText == null)
        {
            return;
        }

        if (loadingTips == null || loadingTips.Length == 0)
        {
            loadingTipsText.text = string.Empty;
            currentTipIndex = -1;
            return;
        }

        if (loadingTips.Length == 1)
        {
            currentTipIndex = 0;
            loadingTipsText.text = loadingTips[0];
            return;
        }

        int next = Random.Range(0, loadingTips.Length);

        while (next == currentTipIndex)
        {
            next = Random.Range(0, loadingTips.Length);
        }

        currentTipIndex = next;
        loadingTipsText.text = loadingTips[next];
    }

    // 同步刷新进度条填充与百分比文本（如“47%”）
    private void UpdateProgressDisplay(float displayedFill)
    {
        if (progressFill != null)
        {
            progressFill.fillAmount = displayedFill;
        }

        if (progressPercentageText != null)
        {
            progressPercentageText.text =
                Mathf.RoundToInt(displayedFill * 100f) + "%";
        }
    }

    // 进度条的平滑推进：显示值以"最少展示时长"对应的速度向
    // 目标进度靠拢。
    // 异步加载过程中主线程会出现帧卡顿（反序列化爆发、GC、编辑器
    // 开销），真实帧间隔单帧可能很大，按它推进会让进度条一下跳过
    // 一大段。这里把参与计算的帧间隔钳制到最大 1/30 秒：
    // 每帧至多推进固定的一小步，卡顿时间不计入，观感始终平滑
    private float AdvanceProgressFill(
        float displayedFill,
        float targetFill)
    {
        float speed = minBarDuration > 0f
            ? 1f / minBarDuration
            : float.MaxValue;

        float deltaTime = Mathf.Min(
            Time.unscaledDeltaTime,
            1f / 30f
        );

        return Mathf.MoveTowards(
            displayedFill,
            targetFill,
            speed * deltaTime
        );
    }
}
