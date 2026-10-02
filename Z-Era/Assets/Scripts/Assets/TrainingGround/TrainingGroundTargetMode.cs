using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 训练场训练模式：挂在 ModeRoot 上（其下为"模式"标题与三块模式
/// 选项牌，选项牌挂 TrainingGroundModeSign 并带触发器）。
/// 射击选项牌切换模式；限时训练进行中（准备/训练阶段）所有模式牌
/// 无效，只有退出牌（TrainingGroundModeSign 选 Quit）能结束当前
/// 训练；训练结束后可重新选择任意模式重新开始。
/// - 自由训练：从靶子预制体实例化 targetCount 具靶子，位置在出生
///   区域内随机采样；身体命中 bodyHitsToKnock 次倒下、头部一枪即倒，
///   倒下为局部旋转 X 从 0 转到 90；倒下的靶子换一个随机位置立起。
/// - 静态限时训练：先显示 Traning Canvas 并按 prepCountdown 倒计时
///   （场上无靶），倒计时结束后靶子出现（逻辑同自由训练），训练
///   阶段持续 trainingDuration 秒并统计击倒靶子数、命中率（命中
///   靶子的子弹/发射的子弹）与爆头率（爆头命中/命中靶子的子弹）；
///   剩余时间不足 timeRedThreshold 秒时 TimeText 变红。
/// 命中计数复用 PrologueTarget（BulletHandle 已有调用钩子）。
/// 注意 PrologueTarget 的匕首状态（knifeDown 等）是私有的、没有
/// 重置接口，而自由训练要求每具靶子可被匕首反复击倒，因此每次
/// 倒下后销毁旧计数器组件并重新挂载一个干净的。
/// 子弹命中选项牌的识别与其它选项牌一致：订阅 BulletHandle 的
/// BulletLaunched/BulletSettled 静态事件反查落点。
/// </summary>
public enum TrainingGroundMode
{
    FreeTraining,
    StaticTimedTraining,
    DynamicTimedTraining,
    // 选项牌专用：射击后退出当前限时训练，不会成为 currentMode
    Quit
}

public class TrainingGroundTargetMode : MonoBehaviour
{
    [Header("模式选项")]

    [Tooltip("靶子预制体，需带 PrologueTarget 并在预制体上拖好头部碰撞体")]
    [SerializeField]
    private PrologueTarget targetPrefab;

    [Tooltip("场上同时存在的靶子数量")]
    [SerializeField, Min(1)]
    private int targetCount = 15;

    [Tooltip("击倒靶子需要的身体命中次数（头部一枪即倒）")]
    [SerializeField, Min(1)]
    private int bodyHitsToKnock = 3;

    [Tooltip("靶子倒下/立起的旋转时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float targetRotateDuration = 1f;

    [Tooltip("匕首攻击是否可以一刀击倒靶子")]
    [SerializeField]
    private bool knifeCanKnockDown = true;

    [Header("出生区域")]

    [Tooltip("定义随机范围的 BoxCollider：底面为靶子落地高度，物体的" +
        "Y 轴旋转为靶子的统一朝向；运行时脚本会自动禁用该碰撞体，" +
        "只读取它的范围，避免子弹打在隐形盒子上。物体缩放请保持 1")]
    [SerializeField]
    private BoxCollider spawnArea;

    [Tooltip("随机采样时靶子之间的最小间距（米）")]
    [SerializeField, Min(0f)]
    private float minSpacing = 2f;

    [Tooltip("因最小间距采样失败时的最大重试次数，超过后放在最后一次采样的位置")]
    [SerializeField, Min(1)]
    private int maxPlacementTries = 30;

    [Header("静态限时训练")]

    [Tooltip("Traning Canvas 根物体：限时模式显示，其余模式隐藏")]
    [SerializeField]
    private GameObject trainingCanvas;

    [Tooltip("画布上的倒计时文本")]
    [SerializeField]
    private TMP_Text timeText;

    [Tooltip("已击倒靶子数文本")]
    [SerializeField]
    private TMP_Text targetCountText;

    [Tooltip("命中率文本")]
    [SerializeField]
    private TMP_Text hitRateText;

    [Tooltip("爆头率文本")]
    [SerializeField]
    private TMP_Text headshotRateText;

    [Tooltip("训练阶段状态文本：准备倒计时显示“未开始”，训练中显示“进行中”，正常结束显示“已完成”，中途退出显示“未完成”")]
    [SerializeField]
    private TMP_Text stateText;

    [Tooltip("StateText 常规颜色（未开始/进行中）")]
    [SerializeField]
    private Color stateNormalColor = Color.white;

    [Tooltip("StateText 正常结束的颜色")]
    [SerializeField]
    private Color stateDoneColor = new Color(0.25f, 1f, 0.35f);

    [Tooltip("StateText 中途退出的颜色")]
    [SerializeField]
    private Color stateQuitColor = new Color(1f, 0.25f, 0.25f);

    [Tooltip("靶子出现前的准备倒计时（秒）")]
    [SerializeField, Min(1f)]
    private float prepCountdown = 5f;

    [Tooltip("训练阶段总时长（秒）")]
    [SerializeField, Min(1f)]
    private float trainingDuration = 60f;

    [Tooltip("训练阶段剩余时间小于等于该值（秒）时 TimeText 变红")]
    [SerializeField, Min(0f)]
    private float timeRedThreshold = 10f;

    [Tooltip("TimeText 正常颜色")]
    [SerializeField]
    private Color timeNormalColor = Color.white;

    [Tooltip("TimeText 剩余时间不足时的颜色")]
    [SerializeField]
    private Color timeRedColor = new Color(1f, 0.25f, 0.25f);

    [Header("动态限时训练")]

    [Tooltip("靶子移动速度下限（米/秒）")]
    [SerializeField, Min(0.1f)]
    private float minMoveSpeed = 2f;

    [Tooltip("靶子移动速度上限（米/秒）")]
    [SerializeField, Min(0.1f)]
    private float maxMoveSpeed = 5f;

    [Tooltip("靶子出现/消失时距区域左右边缘与前后边缘的内缩距离（米），避免靶子刷在墙里")]
    [SerializeField, Min(0f)]
    private float edgeInset = 0.5f;

    // 限时训练的阶段
    private enum StatsPhase
    {
        Idle,       // 非限时模式
        Prep,       // 准备倒计时，场上无靶
        Training,   // 训练进行中，统计开启
        Finished    // 训练结束，显示最终统计
    }

    // 单具靶子的运行时记录
    private class TargetEntry
    {
        public Transform Target;
        public Collider HeadCollider;
        public Vector3 BaseLocalEuler;
        public Vector3 LocalPosition;
        public PrologueTarget Counter;
        public Coroutine CycleRoutine;
        public Coroutine MoveRoutine;
        public float MoveSpeed;
        public int MoveDirection;
        public bool Cycling;
    }

    private readonly List<TargetEntry> entries =
        new List<TargetEntry>();

    private readonly Dictionary<BulletHandle, Vector3> launchPositions =
        new Dictionary<BulletHandle, Vector3>();

    private TrainingGroundModeSign selectedSign;
    private TrainingGroundMode? currentMode;

    private StatsPhase statsPhase = StatsPhase.Idle;
    private float phaseTimeLeft;

    // 训练阶段统计：发射子弹数、命中靶子弹数、爆头命中数、击倒靶子数
    private int shotsFired;
    private int targetHits;
    private int headHits;
    private int knockdowns;

    // 落点反查射线的参数：子弹停在命中点表面，稍微沿反方向退一点
    // 再沿飞行方向打短射线即可命中原表面
    private const float SettleRayBackOffset = 0.05f;
    private const float SettleRayDistance = 0.15f;

    // 落点反查时排除的图层名，与 BulletHandle 默认排除的图层保持一致，
    // 避免反查射线命中子弹本身被忽略的玩家/武器碰撞体；
    // 图层号在 OnEnable 中查询（Unity API 不允许在字段初始化器中调用）
    private static readonly string[] SettleRayIgnoredLayerNames =
    {
        "Bullet",
        "Player",
        "Weapon",
        "Ignore Raycast"
    };

    private int settleRayIgnoreMask;

    private void OnEnable()
    {
        settleRayIgnoreMask = 0;
        foreach (string layerName in SettleRayIgnoredLayerNames)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer >= 0)
            {
                settleRayIgnoreMask |= 1 << layer;
            }
        }

        // 出生区域碰撞体只用于在编辑器里可视化调整范围，
        // 运行时禁用，避免子弹被它挡住
        if (spawnArea != null)
        {
            spawnArea.enabled = false;
        }

        BulletHandle.BulletLaunched += OnBulletLaunched;
        BulletHandle.BulletSettled += OnBulletSettled;
    }

    private void OnDisable()
    {
        BulletHandle.BulletLaunched -= OnBulletLaunched;
        BulletHandle.BulletSettled -= OnBulletSettled;

        launchPositions.Clear();
        ClearTargets();

        statsPhase = StatsPhase.Idle;
        HideCanvas();
    }

    private void Update()
    {
        if (statsPhase == StatsPhase.Prep)
        {
            phaseTimeLeft -= Time.deltaTime;

            if (phaseTimeLeft <= 0f)
            {
                BeginTraining();
            }
            else
            {
                UpdateTimeText(phaseTimeLeft);
            }
        }
        else if (statsPhase == StatsPhase.Training)
        {
            phaseTimeLeft -= Time.deltaTime;

            if (phaseTimeLeft <= 0f)
            {
                EndTraining(false);
            }
            else
            {
                UpdateTimeText(phaseTimeLeft);
                UpdateStatsTexts();
            }
        }
    }

    // 选中某个模式选项牌。限时训练进行中（准备倒计时与训练阶段）
    // 所有模式牌无效，只有退出牌可以结束当前训练；
    // 训练结束（已完成/未完成）后可重新选择任意模式重新开始
    public void SelectSign(TrainingGroundModeSign sign)
    {
        if (sign == null)
        {
            return;
        }

        // 退出牌识别：Mode 选 Quit，或物件名为 QuitText（防止
        // 检查器漏选下拉框）
        if (sign.Mode == TrainingGroundMode.Quit ||
            sign.gameObject.name == "QuitText")
        {
            QuitTraining();

            // 退出牌同样以选中色高亮，并取消其它牌子的选中
            selectedSign?.SetSelected(false);
            selectedSign = sign;
            selectedSign.SetSelected(true);

            return;
        }

        if (statsPhase == StatsPhase.Prep ||
            statsPhase == StatsPhase.Training)
        {
            // 限时流程进行中，锁定模式切换
            return;
        }

        selectedSign?.SetSelected(false);
        selectedSign = sign;
        selectedSign.SetSelected(true);

        SetMode(sign.Mode);
    }

    // 射击退出牌：
    // - 限时训练进行中（准备/训练阶段）：结束当前训练，场上的靶子
    //   全部倒下，状态文本显示“未完成”；
    // - 自由训练：全场靶子倒下并不再响应命中（再次射击“自由训练”
    //   可重新生成并立起）；
    // - 其它状态（非限时流程的结束态等）：无效果
    public void QuitTraining()
    {
        if (statsPhase == StatsPhase.Prep ||
            statsPhase == StatsPhase.Training)
        {
            EndTraining(true);
            return;
        }

        if (currentMode == TrainingGroundMode.FreeTraining)
        {
            LayDownAllTargets();
        }
    }

    private void SetMode(TrainingGroundMode mode)
    {
        // 不做 currentMode == mode 的提前返回：训练结束后
        // 重射同一块模式牌应能重新开始
        currentMode = mode;
        ClearTargets();
        statsPhase = StatsPhase.Idle;

        if (mode == TrainingGroundMode.FreeTraining)
        {
            HideCanvas();
            SpawnTargets();
        }
        else if (mode ==
            TrainingGroundMode.StaticTimedTraining ||
            mode ==
            TrainingGroundMode.DynamicTimedTraining)
        {
            StartTimedPrep();
        }
        else
        {
            HideCanvas();
            Debug.LogWarning(
                "该训练模式尚未实现：" + mode,
                this
            );
        }
    }

    // 限时训练（静态/动态）：先准备倒计时（场上无靶），结束后进入训练阶段
    private void StartTimedPrep()
    {
        if (trainingCanvas == null || timeText == null)
        {
            Debug.LogError(
                "Traning Canvas 或 TimeText 未赋值，无法开始静态限时训练。",
                this
            );
            return;
        }

        trainingCanvas.SetActive(true);

        // 上次训练结束时 TimeText 被禁用，重新开始前先启用
        timeText.gameObject.SetActive(true);

        SetStateText("未开始", stateNormalColor);

        ResetStats();
        UpdateStatsTexts();

        statsPhase = StatsPhase.Prep;
        phaseTimeLeft = prepCountdown;
        UpdateTimeText(phaseTimeLeft);
    }

    // 准备倒计时结束：靶子以倒下状态出现并立起，统计开始。
    // 静态限时在区域内随机落位；动态限时从左右两边出现并开始移动
    private void BeginTraining()
    {
        SetStateText("进行中", stateNormalColor);

        if (currentMode == TrainingGroundMode.DynamicTimedTraining)
        {
            for (int i = 0; i < targetCount; i++)
            {
                SpawnDynamicTarget();
            }
        }
        else
        {
            SpawnTargets(true);
        }

        ResetStats();
        UpdateStatsTexts();

        statsPhase = StatsPhase.Training;
        phaseTimeLeft = trainingDuration;
        UpdateTimeText(phaseTimeLeft);
    }

    // 训练结束：场上靶子统一倒下并保留在原地（不再计数、不再响应
    // 命中），最终统计保留供玩家查看；靶子在下次切换模式或禁用时清理。
    // 正常结束显示“已完成”，射击退出牌中途结束显示“未完成”
    private void EndTraining(bool quit)
    {
        statsPhase = StatsPhase.Finished;

        if (timeText != null)
        {
            timeText.gameObject.SetActive(false);
        }

        SetStateText(
            quit ? "未完成" : "已完成",
            quit ? stateQuitColor : stateDoneColor
        );

        UpdateStatsTexts();
        LayDownAllTargets();
    }

    private void SetStateText(string message, Color color)
    {
        if (stateText == null)
        {
            return;
        }

        stateText.text = message;
        stateText.color = color;
    }

    // 让场上所有靶子（包括正处于倒下/立起动画中的）播放倒下动画；
    // 动画从各自当前角度开始，避免中途被打断时角度跳变
    private void LayDownAllTargets()
    {
        foreach (TargetEntry entry in entries)
        {
            if (entry.CycleRoutine != null)
            {
                StopCoroutine(entry.CycleRoutine);
            }

            // 移动中的靶子就地停下，从当前角度倒下
            if (entry.MoveRoutine != null)
            {
                StopCoroutine(entry.MoveRoutine);
            }

            // 倒下后不再参与任何命中与统计
            entry.Cycling = true;

            if (entry.Counter != null)
            {
                entry.Counter.countingEnabled = false;
                entry.Counter.knifeCountingEnabled = false;
            }

            float fromX = entry.Target != null
                ? entry.Target.localEulerAngles.x
                : 90f;

            entry.CycleRoutine = StartCoroutine(
                EndFallRoutine(entry, fromX)
            );
        }
    }

    private IEnumerator EndFallRoutine(
        TargetEntry entry,
        float fromX)
    {
        yield return RotateTargetX(entry, fromX, 90f);
    }

    private void ResetStats()
    {
        shotsFired = 0;
        targetHits = 0;
        headHits = 0;
        knockdowns = 0;
    }

    private void UpdateTimeText(float remaining)
    {
        if (timeText == null)
        {
            return;
        }

        timeText.text = Mathf.CeilToInt(
            Mathf.Max(0f, remaining)
        ).ToString();

        timeText.color =
            statsPhase == StatsPhase.Training &&
            remaining <= timeRedThreshold
                ? timeRedColor
                : timeNormalColor;
    }

    private void UpdateStatsTexts()
    {
        if (targetCountText != null)
        {
            targetCountText.text = knockdowns.ToString();
        }

        if (hitRateText != null)
        {
            hitRateText.text = FormatRatio(
                targetHits,
                shotsFired
            );
        }

        if (headshotRateText != null)
        {
            headshotRateText.text = FormatRatio(
                headHits,
                targetHits
            );
        }
    }

    private static string FormatRatio(int part, int total)
    {
        if (total <= 0)
        {
            return "0%";
        }

        return Mathf.RoundToInt(100f * part / total) + "%";
    }

    private void HideCanvas()
    {
        if (trainingCanvas != null)
        {
            trainingCanvas.SetActive(false);
        }
    }

    private void OnBulletLaunched(BulletHandle bullet)
    {
        if (bullet == null)
        {
            return;
        }

        launchPositions[bullet] = bullet.transform.position;

        // 训练阶段内发射的每一颗子弹都计入命中率分母
        if (statsPhase == StatsPhase.Training)
        {
            shotsFired++;
        }
    }

    private void OnBulletSettled(BulletHandle bullet)
    {
        if (bullet == null ||
            !launchPositions.TryGetValue(
                bullet,
                out Vector3 startPosition))
        {
            return;
        }

        launchPositions.Remove(bullet);

        // 超时回收的子弹没有命中任何表面，落点反查打不到东西时会
        // 自然忽略，这里无需额外区分
        TrySelectSignAtHit(bullet.transform.position, startPosition);
    }

    // 用"发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短
    // 射线，找出子弹实际命中的碰撞体；命中模式选项牌时选中它
    private void TrySelectSignAtHit(
        Vector3 settlePosition,
        Vector3 launchPosition)
    {
        Vector3 direction = settlePosition - launchPosition;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        if (Physics.Raycast(
            settlePosition - direction * SettleRayBackOffset,
            direction,
            out RaycastHit hit,
            SettleRayDistance,
            ~settleRayIgnoreMask,
            QueryTriggerInteraction.Collide))
        {
            TrainingGroundModeSign sign =
                hit.collider.GetComponentInParent<TrainingGroundModeSign>();

            if (sign != null)
            {
                SelectSign(sign);
            }
        }
    }

    // riseFromLying = true 时，靶子先以倒下状态出现再立起
    // （静态限时训练用）；false 时生成即立起（自由训练用）
    private void SpawnTargets(bool riseFromLying = false)
    {
        if (targetPrefab == null || spawnArea == null)
        {
            Debug.LogError(
                "靶子预制体或出生区域未赋值，无法生成靶子。",
                this
            );
            return;
        }

        for (int i = 0; i < targetCount; i++)
        {
            PrologueTarget prefabCounter =
                Instantiate(targetPrefab, spawnArea.transform);

            TargetEntry entry = new TargetEntry
            {
                Target = prefabCounter.transform,
                HeadCollider = prefabCounter.headCollider,
                BaseLocalEuler = prefabCounter.transform.localEulerAngles
            };

            if (entry.HeadCollider == null)
            {
                Debug.LogWarning(
                    "靶子预制体未配置头部碰撞体，头部一枪即倒不会生效。",
                    prefabCounter
                );
            }

            // 先采样（此时该靶子尚未加入列表，不参与间距检查），
            // 再登记，最后销毁预制体自带的计数器并挂载受管理的干净组件；
            // 预制体计数器的私有匕首状态无法重置，不能直接复用
            entry.LocalPosition = FindSpawnPosition();
            entries.Add(entry);

            entry.Target.localPosition = entry.LocalPosition;
            entry.Target.localRotation = Quaternion.Euler(
                0f,
                entry.BaseLocalEuler.y,
                entry.BaseLocalEuler.z
            );

            Destroy(prefabCounter);
            AttachCounter(entry);

            if (riseFromLying)
            {
                // 以倒下状态出现，立起过程中不计数；
                // knifeCountingEnabled 暂关，避免立起期间被匕首
                // 命中白白消耗该靶的匕首击倒状态
                entry.Cycling = true;
                entry.Counter.knifeCountingEnabled = false;

                entry.Target.localRotation = Quaternion.Euler(
                    90f,
                    entry.BaseLocalEuler.y,
                    entry.BaseLocalEuler.z
                );

                entry.CycleRoutine = StartCoroutine(
                    SpawnRiseRoutine(entry)
                );
            }
            else
            {
                // 靶子生成时就是立起状态，直接开启命中计数
                // （击倒循环里的靶子则是等重新立起后才开启）
                entry.Counter.countingEnabled = true;
            }
        }
    }

    // 倒下状态出现的靶子：立起完成后开启计数
    private IEnumerator SpawnRiseRoutine(TargetEntry entry)
    {
        yield return RotateTargetX(entry, 90f, 0f);

        if (entry.Counter != null)
        {
            entry.Counter.countingEnabled = true;
            entry.Counter.knifeCountingEnabled = knifeCanKnockDown;
        }

        entry.Cycling = false;
    }

    // 动态限时：从区域左右两边生成一具靶子，先立起再开始移动
    private void SpawnDynamicTarget()
    {
        PrologueTarget prefabCounter =
            Instantiate(targetPrefab, spawnArea.transform);

        TargetEntry entry = new TargetEntry
        {
            Target = prefabCounter.transform,
            HeadCollider = prefabCounter.headCollider,
            BaseLocalEuler = prefabCounter.transform.localEulerAngles
        };

        if (entry.HeadCollider == null)
        {
            Debug.LogWarning(
                "靶子预制体未配置头部碰撞体，头部一枪即倒不会生效。",
                prefabCounter
            );
        }

        entries.Add(entry);
        PlaceAtEdge(entry);

        Destroy(prefabCounter);
        AttachCounter(entry);

        // 以倒下状态出现，立起期间不计数、匕首计数暂关
        // （避免立起期间被匕首命中白白消耗该靶的匕首击倒状态）
        entry.Cycling = true;
        entry.Counter.knifeCountingEnabled = false;

        entry.Target.localRotation = Quaternion.Euler(
            90f,
            entry.BaseLocalEuler.y,
            entry.BaseLocalEuler.z
        );

        entry.CycleRoutine = StartCoroutine(
            DynamicRiseRoutine(entry)
        );
    }

    // 动态限时：立起完成后开启计数并开始移动
    private IEnumerator DynamicRiseRoutine(TargetEntry entry)
    {
        yield return RotateTargetX(entry, 90f, 0f);

        if (entry.Counter != null)
        {
            entry.Counter.countingEnabled = true;
            entry.Counter.knifeCountingEnabled = knifeCanKnockDown;
        }

        entry.Cycling = false;

        entry.MoveRoutine = StartCoroutine(
            MoveAndExitRoutine(entry)
        );
    }

    // 把靶子放到区域某一侧的边缘（局部坐标，Y 取区域底面），
    // 并随机化移动方向与速度；移动方向指向另一侧
    private void PlaceAtEdge(TargetEntry entry)
    {
        Vector3 size = spawnArea.size;
        Vector3 center = spawnArea.center;

        float xInset = Mathf.Min(edgeInset, size.x * 0.5f);
        float zMargin = Mathf.Min(edgeInset, size.z * 0.5f);

        int side = Random.value < 0.5f ? -1 : 1;

        entry.LocalPosition = new Vector3(
            center.x + side * (size.x * 0.5f - xInset),
            center.y - size.y * 0.5f,
            Random.Range(
                center.z - size.z * 0.5f + zMargin,
                center.z + size.z * 0.5f - zMargin
            )
        );

        entry.MoveDirection = -side;
        entry.MoveSpeed = Random.Range(
            Mathf.Min(minMoveSpeed, maxMoveSpeed),
            Mathf.Max(minMoveSpeed, maxMoveSpeed)
        );

        entry.Target.localPosition = entry.LocalPosition;
    }

    // 匀速移动到另一侧；未被击倒到达时倒下并消失，
    // 训练阶段内补充一具新靶子，保持场上靶子数量不变
    private IEnumerator MoveAndExitRoutine(TargetEntry entry)
    {
        float exitX = spawnArea.center.x +
            entry.MoveDirection *
            (spawnArea.size.x * 0.5f -
                Mathf.Min(edgeInset, spawnArea.size.x * 0.5f));

        while (entry.Target != null &&
            statsPhase == StatsPhase.Training)
        {
            Vector3 position = entry.Target.localPosition;
            position.x += entry.MoveDirection *
                entry.MoveSpeed * Time.deltaTime;
            entry.Target.localPosition = position;
            entry.LocalPosition = position;

            bool reachedExit = entry.MoveDirection > 0
                ? position.x >= exitX
                : position.x <= exitX;

            if (reachedExit)
            {
                break;
            }

            yield return null;
        }

        if (entry.Target == null ||
            statsPhase != StatsPhase.Training)
        {
            yield break;
        }

        // 到达另一边：先倒下再消失
        entry.Cycling = true;
        entry.Counter.countingEnabled = false;
        entry.Counter.knifeCountingEnabled = false;

        yield return RotateTargetX(entry, 0f, 90f);

        RemoveEntry(entry);

        if (statsPhase == StatsPhase.Training &&
            currentMode ==
            TrainingGroundMode.DynamicTimedTraining)
        {
            SpawnDynamicTarget();
        }
    }

    // 从场上移除并销毁一具靶子
    private void RemoveEntry(TargetEntry entry)
    {
        entries.Remove(entry);

        if (entry.Target != null)
        {
            Destroy(entry.Target.gameObject);
        }
    }

    // 在出生区域内采样一个与现有靶子保持最小间距的位置
    // （区域局部坐标，Y 取区域底面）
    private Vector3 FindSpawnPosition()
    {
        Vector3 size = spawnArea.size;
        Vector3 center = spawnArea.center;

        Vector3 position = new Vector3(
            center.x,
            center.y - size.y * 0.5f,
            center.z
        );

        for (int attempt = 0; attempt < maxPlacementTries; attempt++)
        {
            position = new Vector3(
                Random.Range(
                    center.x - size.x * 0.5f,
                    center.x + size.x * 0.5f
                ),
                center.y - size.y * 0.5f,
                Random.Range(
                    center.z - size.z * 0.5f,
                    center.z + size.z * 0.5f
                )
            );

            bool tooClose = false;

            foreach (TargetEntry entry in entries)
            {
                if ((entry.LocalPosition - position)
                    .sqrMagnitude <
                    minSpacing * minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                break;
            }
        }

        return position;
    }

    // 挂载一个干净的计数器并配置、订阅；
    // 匕首击倒状态（knifeDown 等）无法从外部重置，
    // 用全新组件保证每具靶子可被反复击倒
    private void AttachCounter(TargetEntry entry)
    {
        entry.Counter = entry.Target.gameObject.AddComponent<PrologueTarget>();
        entry.Counter.requiredHits = bodyHitsToKnock;
        entry.Counter.headCollider = entry.HeadCollider;
        entry.Counter.countingEnabled = false;
        entry.Counter.knifeCountingEnabled = knifeCanKnockDown;

        entry.Counter.OnRequiredHitsReached +=
            () => KnockDown(entry);
        entry.Counter.OnHeadHit +=
            () => KnockDown(entry);
        entry.Counter.OnHeadHit += OnTargetHeadHit;
        entry.Counter.OnKnifeHit +=
            () => KnockDown(entry);
        entry.Counter.OnBurstHit += OnTargetBurstHit;
    }

    // 命中达标（身体/头部/匕首）后倒下、换位、重新立起
    private void KnockDown(TargetEntry entry)
    {
        if (entry.Cycling)
        {
            return;
        }

        entry.Cycling = true;
        entry.Counter.countingEnabled = false;

        // 倒下/立起期间关闭匕首计数，避免该期间被匕首命中
        // 白白消耗干净计数器的匕首击倒状态
        entry.Counter.knifeCountingEnabled = false;

        // 移动中的靶子被击倒后立即停止移动，等重新立起后再恢复
        if (entry.MoveRoutine != null)
        {
            StopCoroutine(entry.MoveRoutine);
            entry.MoveRoutine = null;
        }

        // 限时训练阶段内击倒的靶子计入统计
        if (statsPhase == StatsPhase.Training)
        {
            knockdowns++;
        }

        entry.CycleRoutine = StartCoroutine(
            KnockDownRoutine(entry)
        );
    }

    // 命中靶子统计：OnBurstHit 在计数开启时每次命中触发一次
    private void OnTargetBurstHit(int burstId)
    {
        if (statsPhase == StatsPhase.Training)
        {
            targetHits++;
        }
    }

    // 爆头统计：每次命中头部碰撞体触发一次
    private void OnTargetHeadHit()
    {
        if (statsPhase == StatsPhase.Training)
        {
            headHits++;
        }
    }

    private IEnumerator KnockDownRoutine(TargetEntry entry)
    {
        yield return RotateTargetX(entry, 0f, 90f);

        if (currentMode == TrainingGroundMode.DynamicTimedTraining)
        {
            // 动态限时：被击倒的靶子从左右两边随机一侧重新出现
            PlaceAtEdge(entry);
        }
        else
        {
            // 静态限时/自由训练：区域内随机换位
            entry.LocalPosition = FindSpawnPosition();
            entry.Target.localPosition = entry.LocalPosition;
        }

        Destroy(entry.Counter);
        AttachCounter(entry);

        // 立起期间同样关闭匕首计数，立起完成后再开启
        entry.Counter.knifeCountingEnabled = false;

        yield return RotateTargetX(entry, 90f, 0f);

        entry.Counter.countingEnabled = true;
        entry.Counter.knifeCountingEnabled = knifeCanKnockDown;

        entry.Cycling = false;

        // 动态限时的靶子立起后继续移动
        if (currentMode == TrainingGroundMode.DynamicTimedTraining)
        {
            entry.MoveRoutine = StartCoroutine(
                MoveAndExitRoutine(entry)
            );
        }
    }

    // 局部旋转 X 从 from 转到 to，其余欧拉角保持立起时记录的基准值；
    // 靶子的轴心需在底部铰链处。
    // 本方法只作为 KnockDownRoutine 的嵌套协程运行，不管理
    // CycleRoutine 句柄（句柄始终指向外层 KnockDownRoutine）
    private IEnumerator RotateTargetX(
        TargetEntry entry,
        float fromX,
        float toX)
    {
        float time = 0f;

        while (time < targetRotateDuration)
        {
            // 靶子被清场销毁后立即终止，防止访问已销毁的 Transform
            if (entry.Target == null)
            {
                yield break;
            }

            time += Time.deltaTime;

            float x = Mathf.Lerp(
                fromX,
                toX,
                Mathf.Clamp01(time / targetRotateDuration)
            );

            entry.Target.localRotation = Quaternion.Euler(
                x,
                entry.BaseLocalEuler.y,
                entry.BaseLocalEuler.z
            );

            yield return null;
        }

        if (entry.Target == null)
        {
            yield break;
        }

        entry.Target.localRotation = Quaternion.Euler(
            toX,
            entry.BaseLocalEuler.y,
            entry.BaseLocalEuler.z
        );
    }

    // 清空场上全部靶子（切换模式、训练结束与禁用时调用）
    private void ClearTargets()
    {
        foreach (TargetEntry entry in entries)
        {
            if (entry.CycleRoutine != null)
            {
                StopCoroutine(entry.CycleRoutine);
            }

            if (entry.MoveRoutine != null)
            {
                StopCoroutine(entry.MoveRoutine);
            }

            if (entry.Target != null)
            {
                Destroy(entry.Target.gameObject);
            }
        }

        entries.Clear();
    }
}
