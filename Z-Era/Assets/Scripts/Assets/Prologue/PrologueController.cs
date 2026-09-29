using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PrologueController : MonoBehaviour
{
    [Tooltip("序章场景中Glock上的Pickable Item脚本")]
    public PickableItem glockPickableItem;
    [Tooltip("场景中的Glock")]
    public GameObject glock;
    [Tooltip("序章场景中player身上的Glock")]
    public GameObject player_Glock;

    [Header("训练场引导")]
    [Tooltip("任务 pr_tk_02 接取后绿色高亮闪烁的踏板（高亮作用于其全部Renderer）")]
    public GameObject steppingStool;
    [Tooltip("踏板上用于判定的触发器碰撞体（推荐用复制的SteppingStoolTrigger：仅触发器、无渲染），精确指定玩家进入检测用；留空则自动查找踏板下所有触发器")]
    public Collider steppingStoolTrigger;
    [Tooltip("踏板高亮的颜色（RGB；透明度由下面的高亮透明度控制）")]
    public Color stoolHighlightColor = new Color(0f, 1f, 0.4f, 1f);
    [Tooltip("高亮罩最不透明时的 Alpha，闪烁时在其与 0 之间往复；小于 1 即可透出踏板本色")]
    [Range(0f, 1f)]
    public float stoolHighlightAlpha = 0.55f;
    [Tooltip("高亮罩相对踏板的放大倍数，微放大让罩面浮在踏板表面外，避免共面闪烁")]
    public float stoolHighlightScale = 1.01f;
    [Tooltip("踏板高亮闪烁频率（每秒循环次数）")]
    public float stoolBlinkSpeed = 1.5f;
    [Tooltip("训练场中间的靶子（初始倒地，X=90，身上或父级挂载PrologueTarget）")]
    public Transform targetMain;
    [Tooltip("靶子立起/倒下的持续时间（秒）")]
    public float targetRotateDuration = 1f;
    [Tooltip("靶子倒下后要完成的目标所属的任务资产（pr_tk_02_glockshoot）")]
    public QuestAsset tk02Quest;
    [Tooltip("靶子倒下后要完成的目标的Objective Id（pr_tk_02_glockshoot的Objectives第一项）")]
    public string tk02ObjectiveId = "pr_tk_02_glockshoottarget";
    [Tooltip("靶子倒下后要设置的Flag，驱动后续对话")]
    public string tk03StartFlag = "pr_die_harper_03_tk03_start";

    [Header("训练场爆头（tk03）")]
    [Tooltip("爆头目标所属的任务资产（pr_tk_03）")]
    public QuestAsset tk03Quest;
    [Tooltip("爆头目标的Objective Id（倒下时机以该目标在任务系统中的实际进度为准）")]
    public string tk03ObjectiveId = "pr_tk_03_glockshoothead";
    [Tooltip("爆头目标完成后要设置的Flag，驱动后续对话")]
    public string tk03CompleteFlag = "pr_tk_03_glockshoothead";
    [Tooltip("玩家身上的PlayerController，留空则自动查找（锁定/解锁移动用）")]
    public PlayerController playerController;

    [Header("训练场全场靶（tk04）")]
    [Tooltip("九个靶子共同的父物体（每个直接子物体为一具靶子，没有 PrologueTarget 时自动补挂）")]
    public Transform targetsRoot;
    [Tooltip("任务资产 pr_tk_04_glockshootalltargets")]
    public QuestAsset tk04Quest;
    [Tooltip("击倒靶子目标的 Objective Id")]
    public string tk04ObjectiveId = "pr_tk_04_glockshootalltargets";
    [Tooltip("全部靶子击倒后要设置的 Flag，驱动后续对话")]
    public string tk04CompleteFlag = "pr_tk_04_glockshootalltargets_finished";
    [Tooltip("靶子依次立起的间隔（秒），立起顺序为 Targets 下子物体顺序")]
    public float tk04StandInterval = 0.25f;

    // 线性阶段的当前进度
    private Stage stage = Stage.WaitingFlag;

    private enum Stage
    {
        // 等待任务 pr_tk_02 接取（Flag 未出现）
        WaitingFlag,
        // Flag 已出现：踏板绿色高亮闪烁，等待玩家踩上踏板
        HighlightStool,
        // 玩家已踩上踏板：锁定移动，靶子立起中
        TargetStanding,
        // 靶子已立起，等待玩家命中足够次数
        WaitingHits,
        // 靶子倒下中
        TargetFalling,
        // 任务完成、Flag 已设置，tk02 线性内容结束
        Finished,
        // 等待任务 pr_tk_03 接取（Flag 未出现）
        Tk03WaitingFlag,
        // Flag 已出现：踏板再次绿色高亮闪烁
        Tk03HighlightStool,
        // 玩家已踩上踏板：锁定移动，靶子再次立起中
        Tk03TargetStanding,
        // 靶子已立起，等待玩家爆头足够次数
        Tk03WaitingHeadHits,
        // 靶子倒下中
        Tk03TargetFalling,
        // tk03 线性内容结束
        AllFinished,
        // tk04：Flag 已出现，踏板高亮闪烁，等待玩家踩上踏板
        Tk04HighlightStool,
        // tk04：玩家已踩上踏板，锁定移动，全场靶子依次立起中
        Tk04TargetsStanding,
        // tk04：靶子全部立起、计数开启，等待击倒全部靶子
        Tk04Shooting,
        // tk04：全部击倒、目标完成、Flag 已设置
        Tk04Finished
    }

    // 踏板高亮罩的材质与子物体列表。高亮罩是微放大的透明子物体，
    // 浮在踏板表面外随闪烁改写 Alpha；直接复用原网格（共享资源），
    // 销毁时只需删子物体与材质，不产生网格副本。
    private Material stoolHighlightMaterial;
    private readonly List<GameObject> stoolHighlightOverlays =
        new List<GameObject>();

    // tk04：Targets 下的全部靶子（没有计数器的在 Start 自动补挂）
    private readonly List<PrologueTarget> tk04Targets =
        new List<PrologueTarget>();

    // tk04：尚未倒下的靶子数，归零时完成任务
    private int tk04TargetsRemaining;

    // 靶子上的命中计数器
    private PrologueTarget prologueTarget;

    // Player 图层号，Start 时缓存
    private int playerLayer;

    // 玩家当前是否站在踏板触发器内（处理高亮开始时
    // 玩家已经在区域内的场景，此时不会再触发 Enter 事件）
    private bool stoolPlayerInside;

    void Start()
    {
        playerLayer = LayerMask.NameToLayer("Player");

        // 给踏板触发器挂载转发组件：优先用拖拽指定的触发器，
        // 保证判定精确；未拖入时退化为自动查找踏板下所有触发器。
        // 玩家进出任一触发器都会回调 OnStoolTriggerEnter/Exit。
        if (steppingStool != null)
        {
            if (steppingStoolTrigger != null)
            {
                AttachStoolRelay(steppingStoolTrigger.gameObject);
            }
            else
            {
                Collider[] colliders =
                    steppingStool.GetComponentsInChildren<Collider>(
                        true
                    );

                foreach (Collider collider in colliders)
                {
                    if (collider.isTrigger)
                    {
                        AttachStoolRelay(collider.gameObject);
                    }
                }
            }
        }

        // 获取靶子上的命中计数器，没有则自动添加
        if (targetMain != null)
        {
            prologueTarget =
                targetMain.GetComponentInParent<PrologueTarget>();

            if (prologueTarget == null)
            {
                prologueTarget =
                    targetMain.gameObject.AddComponent<PrologueTarget>();
            }

            prologueTarget.OnRequiredHitsReached +=
                OnTargetRequiredHitsReached;

            prologueTarget.OnHeadHit += OnTargetHeadHit;
        }

        // 收集 tk04 全场靶子：Targets 下每个直接子物体为一具靶子，
        // 没有计数器的自动补挂，并订阅各自的击倒事件
        if (targetsRoot != null)
        {
            foreach (Transform child in targetsRoot)
            {
                PrologueTarget target = child.GetComponent<PrologueTarget>();

                if (target == null)
                {
                    target = child.gameObject.AddComponent<PrologueTarget>();
                }

                target.OnRequiredHitsReached += () =>
                    OnTk04TargetKnockedDown(target);

                tk04Targets.Add(target);
            }
        }
    }

    void Update()
    {
        Set();
    }

    private void Set()
    {
        // 第一段对话结束后启用场景中的Glock的PickableItem脚本组件
        if (DialogueFlags.HasFlag("pr_tk_01_pickweapon_unfinished"))
        {
            glockPickableItem.enabled = true;
        }
        // 拾取Glock后player身上的Glock随之启用
        if (!glock.activeSelf)
        {
            player_Glock.SetActive(true);
        }

        // 任务 pr_tk_02 接取后：踏板绿色高亮闪烁，指引玩家踩上踏板
        if (stage == Stage.WaitingFlag &&
            DialogueFlags.HasFlag("pr_tk_02_glockshoottarget_unfinished"))
        {
            StartStoolHighlight();
            stage = Stage.HighlightStool;
        }

        // 任务 pr_tk_03 接取后：踏板再次绿色高亮闪烁
        if (stage == Stage.Finished &&
            DialogueFlags.HasFlag("pr_tk_03_glockshoothead_unfinished"))
        {
            StartStoolHighlight();
            stage = Stage.Tk03HighlightStool;
        }

        // 任务 pr_tk_04 接取后：踏板再次绿色高亮闪烁，
        // 靶子在玩家踩上踏板后才依次立起
        if (stage == Stage.AllFinished &&
            DialogueFlags.HasFlag(
                "pr_tk_04_glockshootalltargets_unfinished"
            ))
        {
            StartStoolHighlight();
            stage = Stage.Tk04HighlightStool;
        }

        // 高亮期间高亮罩透明度按闪烁频率在 0 与最大透明度之间往复，
        // Alpha 小于 1 时可透出踏板本色，不会整块糊成实心绿
        if ((stage == Stage.HighlightStool ||
             stage == Stage.Tk03HighlightStool ||
             stage == Stage.Tk04HighlightStool) &&
            stoolHighlightMaterial != null)
        {
            float pulse =
                Mathf.Sin(Time.time * stoolBlinkSpeed * Mathf.PI * 2f)
                    * 0.5f + 0.5f;

            Color color = stoolHighlightColor;
            color.a = stoolHighlightAlpha * pulse;
            stoolHighlightMaterial.SetColor("_HighlightColor", color);
        }

        // 高亮开始时玩家若已站在触发器内，同样视为进入
        if (stage == Stage.HighlightStool && stoolPlayerInside)
        {
            EnterStoolZone();
        }
        else if (stage == Stage.Tk03HighlightStool && stoolPlayerInside)
        {
            EnterStoolZoneTk03();
        }
        else if (stage == Stage.Tk04HighlightStool && stoolPlayerInside)
        {
            EnterStoolZoneTk04();
        }
    }

    // 在指定物体上挂载（或复用）踏板触发器转发组件。
    // 注意：不要给触发物体加刚体——CharacterController 与
    // 运动学刚体触发器之间不会产生触发事件，
    // 静态触发器（无刚体）反而是与玩家可靠触发的组合。
    private void AttachStoolRelay(GameObject target)
    {
        PrologueStoolRelay relay =
            target.GetComponent<PrologueStoolRelay>();

        if (relay == null)
        {
            relay = target.AddComponent<PrologueStoolRelay>();
        }

        relay.controller = this;
    }

    // 玩家进入踏板触发器区域：停止高亮，锁定移动，靶子立起
    private void EnterStoolZone()
    {
        StopStoolHighlight();
        LockPlayerMovement(true);
        stage = Stage.TargetStanding;
        StartCoroutine(RotateTargetX(90f, 0f));
    }

    // tk03：玩家再次进入踏板触发器区域，流程同上
    private void EnterStoolZoneTk03()
    {
        StopStoolHighlight();
        LockPlayerMovement(true);
        stage = Stage.Tk03TargetStanding;
        StartCoroutine(RotateTargetX(90f, 0f));
    }

    // tk04：玩家进入踏板触发器区域：停止高亮，锁定移动，
    // 全场靶子开始依次立起
    private void EnterStoolZoneTk04()
    {
        StopStoolHighlight();
        LockPlayerMovement(true);
        stage = Stage.Tk04TargetsStanding;
        StartCoroutine(StandTargetsTk04());
    }

    // tk03：子弹命中头部碰撞体（由 PrologueTarget 的事件驱动）：
    // 爆头目标进度 +1，达到目标需求次数后靶子倒下。
    // 倒下判定以任务系统中的实际进度为准，与 HUD 显示始终一致。
    private void OnTargetHeadHit()
    {
        if (stage != Stage.Tk03WaitingHeadHits)
        {
            return;
        }

        if (tk03Quest == null)
        {
            Debug.LogError(
                "PrologueController：未拖入任务资产 pr_tk_03，" +
                    "无法统计爆头进度。",
                this
            );

            return;
        }

        QuestManager.AddProgress(
            tk03Quest.QuestId,
            tk03ObjectiveId,
            1
        );

        QuestAsset.QuestObjective objective =
            tk03Quest.GetObjective(tk03ObjectiveId);

        if (objective == null)
        {
            return;
        }

        if (QuestManager.GetObjectiveCurrent(
                tk03Quest.QuestId,
                tk03ObjectiveId
            ) >= objective.requiredAmount)
        {
            stage = Stage.Tk03TargetFalling;
            StartCoroutine(RotateTargetX(0f, 90f));
        }
    }

    // tk03：靶子倒下后：确保爆头目标完成、设置 Flag、解锁移动
    private void FinishTaskTk03()
    {
        // 每次命中已经累计过进度，这里兜底把目标直接置为完成，
        // 防止任务资产中的requiredAmount与本地计数不一致
        if (tk03Quest != null)
        {
            QuestManager.SetObjectiveComplete(
                tk03Quest.QuestId,
                tk03ObjectiveId
            );
        }

        DialogueFlags.SetFlag(tk03CompleteFlag, true);
        LockPlayerMovement(false);
        stage = Stage.AllFinished;
    }

    // tk04：玩家进入踏板后全场靶子依次立起（旋转 X 90→0）：
    // 起立顺序 = Targets 下的子物体顺序，相邻间隔 tk04StandInterval；
    // 全部立起完成后开启命中计数并进入射击阶段
    private IEnumerator StandTargetsTk04()
    {
        // Target Main 等靶子可能参与过 tk02/tk03，reached/hitCount
        // 与 countingEnabled 都有残留；reached=true 会让 RegisterHit
        // 直接丢弃后续命中，击倒事件永远不触发，立起前统一清零
        foreach (PrologueTarget target in tk04Targets)
        {
            target.countingEnabled = false;
            target.ResetCounter();
        }

        foreach (PrologueTarget target in tk04Targets)
        {
            StartCoroutine(
                RotateTargetTk04(target.transform, 90f, 0f));

            yield return new WaitForSeconds(tk04StandInterval);
        }

        // 最后一具靶子还需转完 targetRotateDuration，
        // 此处等待结束后全部靶子必定立起完成
        yield return new WaitForSeconds(targetRotateDuration);

        tk04TargetsRemaining = tk04Targets.Count;

        foreach (PrologueTarget target in tk04Targets)
        {
            target.countingEnabled = true;
        }

        stage = Stage.Tk04Shooting;
    }

    // tk04：单具靶子绕自身 X 轴旋转（立起 90→0 或倒下 0→90）
    private IEnumerator RotateTargetTk04(
        Transform target,
        float fromX,
        float toX)
    {
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / targetRotateDuration;

            Vector3 euler = target.localEulerAngles;
            euler.x = Mathf.Lerp(fromX, toX, Mathf.Clamp01(t));
            target.localEulerAngles = euler;

            yield return null;
        }

        Vector3 finalEuler = target.localEulerAngles;
        finalEuler.x = toX;
        target.localEulerAngles = finalEuler;
    }

    // tk04：一具靶子被击倒（由该靶 PrologueTarget 的事件驱动）：
    // 击倒目标进度 +1，并让该靶子倒下
    private void OnTk04TargetKnockedDown(PrologueTarget target)
    {
        if (stage != Stage.Tk04Shooting)
        {
            return;
        }

        if (tk04Quest == null)
        {
            Debug.LogError(
                "PrologueController：未拖入任务资产 " +
                    "pr_tk_04_glockshootalltargets，无法统计击倒进度。",
                this
            );

            return;
        }

        QuestManager.AddProgress(
            tk04Quest.QuestId,
            tk04ObjectiveId,
            1
        );

        StartCoroutine(
            KnockDownTargetTk04(target.transform));
    }

    // tk04：靶子倒下动画播完后递减剩余数，全部倒下时完成任务
    private IEnumerator KnockDownTargetTk04(Transform target)
    {
        yield return RotateTargetTk04(target, 0f, 90f);

        tk04TargetsRemaining--;

        if (tk04TargetsRemaining <= 0)
        {
            FinishTaskTk04();
        }
    }

    // tk04：全部靶子倒下后：确保击倒目标完成、设置 Flag、解锁移动
    private void FinishTaskTk04()
    {
        // 每次击倒已经累计过进度，这里兜底把目标直接置为完成，
        // 防止任务资产中的 requiredAmount 与实际击倒数不一致
        if (tk04Quest != null)
        {
            QuestManager.SetObjectiveComplete(
                tk04Quest.QuestId,
                tk04ObjectiveId
            );
        }

        DialogueFlags.SetFlag(tk04CompleteFlag, true);
        LockPlayerMovement(false);
        stage = Stage.Tk04Finished;
    }

    // 玩家进入踏板触发器（由 PrologueStoolRelay 转发）
    public void OnStoolTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != playerLayer)
        {
            return;
        }

        stoolPlayerInside = true;

        if (stage == Stage.HighlightStool)
        {
            EnterStoolZone();
        }
        else if (stage == Stage.Tk03HighlightStool)
        {
            EnterStoolZoneTk03();
        }
        else if (stage == Stage.Tk04HighlightStool)
        {
            EnterStoolZoneTk04();
        }
    }

    // 玩家离开踏板触发器（由 PrologueStoolRelay 转发）
    public void OnStoolTriggerExit(Collider other)
    {
        if (other.gameObject.layer != playerLayer)
        {
            return;
        }

        stoolPlayerInside = false;
    }

    // 靶子命中次数达到需求（由 PrologueTarget 的事件驱动）：靶子倒下
    private void OnTargetRequiredHitsReached()
    {
        if (stage != Stage.WaitingHits)
        {
            return;
        }

        stage = Stage.TargetFalling;
        StartCoroutine(RotateTargetX(0f, 90f));
    }

    // 靶子绕自身 X 轴旋转：立起（90→0）或倒下（0→90），
    // 结束后根据当前阶段进入下一环节
    private IEnumerator RotateTargetX(float fromX, float toX)
    {
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / targetRotateDuration;

            Vector3 euler = targetMain.localEulerAngles;
            euler.x = Mathf.Lerp(fromX, toX, Mathf.Clamp01(t));
            targetMain.localEulerAngles = euler;

            yield return null;
        }

        Vector3 finalEuler = targetMain.localEulerAngles;
        finalEuler.x = toX;
        targetMain.localEulerAngles = finalEuler;

        if (stage == Stage.TargetStanding)
        {
            // 靶子立起完成：开启命中统计，等待玩家射击
            prologueTarget.countingEnabled = true;
            stage = Stage.WaitingHits;
        }
        else if (stage == Stage.TargetFalling)
        {
            FinishTask();
        }
        else if (stage == Stage.Tk03TargetStanding)
        {
            // 靶子立起完成：开启命中统计（含爆头检测）
            prologueTarget.countingEnabled = true;
            stage = Stage.Tk03WaitingHeadHits;
        }
        else if (stage == Stage.Tk03TargetFalling)
        {
            FinishTaskTk03();
        }
    }

    // 靶子倒下后：完成任务 pr_tk_02 中“击倒靶子”的目标、
    // 设置 Flag、解锁移动。
    // 注意：只完成该目标；若以后给 pr_tk_02 添加更多目标，
    // 任务不会在此整体完成，需等所有目标达标。
    private void FinishTask()
    {
        if (tk02Quest != null)
        {
            tk02Quest.CompleteObjectiveFromEvent(tk02ObjectiveId);
        }
        else
        {
            Debug.LogError(
                "PrologueController：未拖入任务资产 pr_tk_02_glockshoot，" +
                    "无法完成任务目标。",
                this
            );
        }

        DialogueFlags.SetFlag(tk03StartFlag, true);
        LockPlayerMovement(false);
        stage = Stage.Finished;
    }

    // 用半透明绿色高亮罩闪烁踏板：给踏板下每个网格渲染器挂一个
    // 微放大的透明罩子物体，整块踏板（含朝向玩家的棱面）一起
    // 泛绿闪烁。不用描边外壳——硬边立方体的外壳描边在朝向
    // 玩家的棱上无法成形，改用整面半透明罩最稳妥。
    private void StartStoolHighlight()
    {
        // 防御性收尾：上一次高亮未正常停止时避免罩子叠罩子。
        StopStoolHighlight();

        Shader shader = Shader.Find("Custom/Stool Highlight");

        if (shader == null)
        {
            Debug.LogError(
                "PrologueController：找不到 Custom/Stool Highlight " +
                    "Shader，踏板无法高亮。",
                this
            );

            return;
        }

        stoolHighlightMaterial = new Material(shader);
        stoolHighlightMaterial.SetColor(
            "_HighlightColor",
            stoolHighlightColor
        );

        Renderer[] renderers =
            steppingStool.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            CreateStoolHighlightOverlay(renderer);
        }
    }

    // 为指定渲染器创建微放大的高亮罩子物体：直接复用原网格
    // （共享资源，不做副本），透明罩面浮在踏板表面之外，
    // 靠近相机的各面叠加在踏板贴图之上泛绿闪烁。
    private void CreateStoolHighlightOverlay(Renderer sourceRenderer)
    {
        MeshFilter sourceFilter =
            sourceRenderer.GetComponent<MeshFilter>();

        if (sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            Debug.LogWarning(
                "PrologueController：踏板下存在没有网格的渲染器，" +
                    "该渲染器不参与高亮。",
                sourceRenderer
            );

            return;
        }

        GameObject overlay = new GameObject("StoolHighlightOverlay");

        stoolHighlightOverlays.Add(overlay);

        // 恒等局部变换挂为源渲染器的子物体保证几何重合，
        // 再整体微放大让罩面浮出踏板表面。
        overlay.transform.SetParent(sourceRenderer.transform, false);
        overlay.transform.localScale =
            Vector3.one * stoolHighlightScale;

        MeshFilter overlayFilter = overlay.AddComponent<MeshFilter>();
        overlayFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer overlayRenderer =
            overlay.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterial = stoolHighlightMaterial;
        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
    }

    // 销毁高亮罩子物体与材质，停止高亮
    private void StopStoolHighlight()
    {
        foreach (GameObject overlay in stoolHighlightOverlays)
        {
            if (overlay != null)
            {
                Destroy(overlay);
            }
        }

        stoolHighlightOverlays.Clear();

        if (stoolHighlightMaterial != null)
        {
            Destroy(stoolHighlightMaterial);
            stoolHighlightMaterial = null;
        }
    }

    // 锁定/解锁玩家移动：只置位 PlayerController 的移动锁标志，
    // 视角旋转与射击不受影响（禁用整个组件会导致视角冻结、
    // 开枪的后坐力无法回中）
    private void LockPlayerMovement(bool locked)
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.movementLocked = locked;
        }
    }
}
