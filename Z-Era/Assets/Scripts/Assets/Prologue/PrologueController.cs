using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 序章训练场的操作门控：由当前场景的 PrologueController 注册并
// 每帧刷新，WeaponController 的射击/检视/匕首输入据此受限。
// 其他场景没有 PrologueController，Active 恒为 false，完全不干预。
public static class PrologueGameplayGates
{
    // PrologueController 存活时为 true，场景卸载后自动关闭。
    public static bool Active;

    // 仅当玩家位于踏板触发器内且移动被剧情锁定时允许开枪。
    public static bool CanShoot;

    // 任务 pr_tk_02 接取后才允许检视武器（V）。
    public static bool CanInspect;

    // 任务 pr_tk_08 接取后才允许匕首攻击（F）。
    public static bool CanKnifeAttack;

    // pr_tk_09 的拾取武器模型目标完成后才允许打开背包（B）。
    public static bool CanOpenInventory;
}

public class PrologueController : MonoBehaviour
{
    [Tooltip("序章场景中Glock上的Pickable Item脚本")]
    public PickableItem glockPickableItem;
    [Tooltip("训练场区域AmmoBox上的Pickable Item脚本（pr_tk_02接取后启用，供玩家无限/限次补弹）")]
    public PickableItem tk02AmmoBoxPickableItem;
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
    [Tooltip("九个靶子共同的父物体（每个直接子物体为一具靶子，没有 PrologueTarget 时自动补挂；tk04 与 tk05 共用）")]
    public Transform targetsRoot;
    [Tooltip("任务资产 pr_tk_04_glockshootalltargets")]
    public QuestAsset tk04Quest;
    [Tooltip("击倒靶子目标的 Objective Id")]
    public string tk04ObjectiveId = "pr_tk_04_glockshootalltargets";
    [Tooltip("全部靶子击倒后要设置的 Flag，驱动后续对话")]
    public string tk04CompleteFlag = "pr_tk_04_glockshootalltargets_finished";
    [Tooltip("靶子依次立起的间隔（秒），立起顺序为 Targets 下子物体顺序")]
    public float tk04StandInterval = 0.25f;

    [Header("训练场AK47（tk05）")]
    [Tooltip("场景中AK47上的Pickable Item脚本")]
    public PickableItem ak47PickableItem;
    [Tooltip("玩家身上的AK47")]
    public GameObject player_AK47;
    [Tooltip("任务资产 pr_tk_05_ak47shootalltargets")]
    public QuestAsset tk05Quest;
    [Tooltip("击倒靶子目标的 Objective Id")]
    public string tk05ObjectiveId = "pr_tk_05_ak47shootalltargets";
    [Tooltip("全部靶子击倒后要设置的 Flag，驱动后续对话")]
    public string tk05CompleteFlag =
        "pr_tk_05_ak47shootalltargets_finished";
    [Tooltip("靶子依次立起的间隔（秒），立起顺序为 Targets 下子物体顺序")]
    public float tk05StandInterval = 0.25f;

    [Header("训练场AK47爆头（tk06）")]
    [Tooltip("任务资产 pr_tk_06_ak47shoothead")]
    public QuestAsset tk06Quest;
    [Tooltip("爆头击倒目标的 Objective Id")]
    public string tk06ObjectiveId = "pr_tk_06_ak47shoothead";
    [Tooltip("全部靶子击倒后要设置的 Flag，驱动后续对话")]
    public string tk06CompleteFlag =
        "pr_tk_06_ak47shoothead_finished";
    [Tooltip("靶子依次立起的间隔（秒），立起顺序为 Targets 下子物体顺序")]
    public float tk06StandInterval = 0.25f;

    [Header("训练场压枪连射（tk07）")]
    [Tooltip("任务资产 pr_tk_07_recoilcontrol")]
    public QuestAsset tk07Quest;
    [Tooltip("单轮长按命中目标的 Objective Id")]
    public string tk07ObjectiveId = "pr_tk_07_recoilcontrol";
    [Tooltip("达标后要设置的 Flag，驱动后续对话")]
    public string tk07CompleteFlag =
        "pr_tk_07_recoilcontrol_finished";
    [Tooltip("玩家身上的全自动步枪（AK47）的 WeaponController，长按/开火事件来源")]
    public WeaponController tk07Weapon;

    [Header("训练场匕首（tk08）")]
    [Tooltip("可被匕首击倒的靶子的父物体（Targets_KnifeAttack，每个直接子物体为一具靶子，没有 PrologueTarget 时自动补挂）")]
    public Transform tk08TargetsRoot;
    [Tooltip("任务资产 pr_tk_08_knifeattack")]
    public QuestAsset tk08Quest;
    [Tooltip("匕首击倒靶子的 Objective Id")]
    public string tk08ObjectiveId = "pr_tk_08_knifeattack";
    [Tooltip("全部靶子击倒后要设置的 Flag，驱动后续对话")]
    public string tk08CompleteFlag =
        "pr_tk_08_knifeattack_finished";
    [Tooltip("匕首靶高亮的颜色（RGB；透明度由下面的高亮透明度控制）")]
    public Color tk08HighlightColor = new Color(1f, 0.25f, 0.2f, 1f);
    [Tooltip("匕首靶高亮罩最不透明时的 Alpha，闪烁时在其与 0 之间往复")]
    [Range(0f, 1f)]
    public float tk08HighlightAlpha = 0.55f;
    [Tooltip("匕首靶高亮罩相对靶子的放大倍数，微放大让罩面浮在靶子表面外")]
    public float tk08HighlightScale = 1.01f;
    [Tooltip("匕首靶高亮闪烁频率（每秒循环次数）")]
    public float tk08BlinkSpeed = 1.5f;

    [Header("训练场武器模型（tk09）")]
    [Tooltip("场景中Vector模型上的Pickable Item脚本")]
    public PickableItem vectorPickableItem;
    [Tooltip("场景中P90模型上的Pickable Item脚本")]
    public PickableItem p90PickableItem;
    [Tooltip("检查背包目标的任务资产（pr_tk_09_checkinventory）")]
    public QuestAsset tk09CheckInventoryQuest;
    [Tooltip("检查背包目标的 Objective Id")]
    public string tk09CheckInventoryObjectiveId =
        "pr_tk_09_checkinventory";
    [Tooltip("背包检查完成后要设置的 Flag，驱动后续对话")]
    public string tk09CheckInventoryCompleteFlag =
        "pr_tk_09_checkinventory_finished";

    [Header("木箱引导高亮")]
    [Tooltip("pr_tk_01 阶段（拾取Glock前）高亮闪烁的木箱")]
    public GameObject tk01WoodBox;
    [Tooltip("pr_tk_05 阶段（拾取AK47前）高亮闪烁的木箱")]
    public GameObject tk05WoodBox;
    [Tooltip("pr_tk_09 阶段（拾取武器模型前）高亮闪烁的木箱")]
    public GameObject tk09WoodBox;
    [Tooltip("pr_tk_09 木箱停止高亮所依据的任务资产（含 pr_tk_09_pickweaponmodels 目标）")]
    public QuestAsset tk09PickWeaponModelsQuest;
    [Tooltip("pr_tk_09 木箱停止高亮所依据的 Objective Id")]
    public string tk09PickWeaponModelsObjectiveId =
        "pr_tk_09_pickweaponmodels";
    [Tooltip("木箱高亮的颜色（RGB；透明度由下面的高亮透明度控制）")]
    public Color woodBoxHighlightColor = new Color(0f, 1f, 0.4f, 1f);
    [Tooltip("木箱高亮罩最不透明时的 Alpha，闪烁时在其与 0 之间往复")]
    [Range(0f, 1f)]
    public float woodBoxHighlightAlpha = 0.55f;
    [Tooltip("木箱高亮罩相对木箱的放大倍数，微放大让罩面浮在木箱表面外")]
    public float woodBoxHighlightScale = 1.01f;
    [Tooltip("木箱高亮闪烁频率（每秒循环次数）")]
    public float woodBoxBlinkSpeed = 1.5f;

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
        Tk04Finished,
        // tk05：拾取AK47后，踏板高亮闪烁，等待玩家踩上踏板
        Tk05HighlightStool,
        // tk05：玩家已踩上踏板，锁定移动，全场靶子依次立起中
        Tk05TargetsStanding,
        // tk05：靶子全部立起、计数开启，等待击倒全部靶子
        Tk05Shooting,
        // tk05：全部击倒、目标完成、Flag 已设置
        Tk05Finished,
        // tk06：Flag 已出现，踏板高亮闪烁，等待玩家踩上踏板
        Tk06HighlightStool,
        // tk06：玩家已踩上踏板，锁定移动，全场靶子依次立起中
        Tk06TargetsStanding,
        // tk06：靶子全部立起，等待玩家逐个爆头击倒
        Tk06Shooting,
        // tk06：全部击倒、目标完成、Flag 已设置
        Tk06Finished,
        // tk07：Flag 已出现，踏板高亮闪烁，等待玩家踩上踏板
        Tk07HighlightStool,
        // tk07：玩家已踩上踏板，锁定移动，TargetMain 立起中
        Tk07TargetStanding,
        // tk07：靶子已立起，等待单轮长按命中达标
        Tk07Shooting,
        // tk07：达标，靶子倒下中
        Tk07TargetFalling,
        // tk07：任务完成、Flag 已设置
        Tk07Finished,
        // tk08：任务进行中，匕首靶高亮闪烁，等待匕首击倒
        Tk08Active,
        // tk08：全部击倒、任务完成、Flag 已设置
        Tk08Finished,
        // tk09：武器模型已可拾取，等待玩家打开并关闭背包
        Tk09WaitingInventoryOpen,
        // tk09：背包检查完成、任务完成、Flag 已设置
        Tk09Finished
    }

    // 踏板高亮罩的材质与子物体列表。高亮罩是微放大的透明子物体，
    // 浮在踏板表面外随闪烁改写 Alpha；直接复用原网格（共享资源），
    // 销毁时只需删子物体与材质，不产生网格副本。
    private Material stoolHighlightMaterial;
    private readonly List<GameObject> stoolHighlightOverlays =
        new List<GameObject>();

    // tk04/tk05/tk06：Targets 下的全部靶子（没有计数器的在 Start 自动补挂）
    private readonly List<PrologueTarget> tk04Targets =
        new List<PrologueTarget>();

    // tk04/tk05/tk06：尚未倒下的靶子数，归零时完成任务
    private int tk04TargetsRemaining;

    // 靶子上的命中计数器
    private PrologueTarget prologueTarget;

    // tk07：当前长按轮次编号（与 tk07Weapon.CurrentBurstId 对齐）、
    // 本轮命中数、本轮射出且尚未落定的子弹数（按开火时刻归属，
    // 松手后这些子弹命中仍计入本轮）
    private int tk07BurstId;
    private int tk07BurstHits;
    private int tk07BulletsInFlight;

    // tk07：长按是否进行中；松手/打空/换弹后进入结算等待，
    // 等在飞子弹全部落定才判定本轮失败并清零
    private bool tk07BurstActive;
    private bool tk07BurstSettling;

    // tk08：Targets_KnifeAttack 下的匕首靶（没有计数器的
    // 在 Start 自动补挂）
    private readonly List<PrologueTarget> tk08Targets =
        new List<PrologueTarget>();

    // tk08：匕首靶的高亮罩（每靶一组，击倒后单独熄灭）与共享材质
    private Material knifeHighlightMaterial;
    private readonly Dictionary<PrologueTarget, List<GameObject>>
        knifeHighlightOverlays =
            new Dictionary<PrologueTarget, List<GameObject>>();

    // tk09：背包圆盘视图缓存（其所在物体即 InventoryContainer，
    // 初始为禁用状态，必须包含未激活物体才能找到）
    private InventoryRadialView tk09RadialView;

    // tk09：本轮是否已打开过背包（打开并关闭一次即完成）
    private bool tk09InventoryOpened;

    // 木箱引导高亮：当前高亮的木箱、共享材质与罩子清单。
    // 三个 Flag 窗口按剧情顺序推进、互不重叠，共用一套材质。
    private GameObject woodBoxHighlightTarget;
    private Material woodBoxHighlightMaterial;
    private readonly List<GameObject> woodBoxHighlightOverlays =
        new List<GameObject>();

    // Player 图层号，Start 时缓存
    private int playerLayer;

    // 玩家当前是否站在踏板触发器内（处理高亮开始时
    // 玩家已经在区域内的场景，此时不会再触发 Enter 事件）
    private bool stoolPlayerInside;

    private void OnEnable()
    {
        // 注册序章操作门控；离开场景（禁用/销毁）时注销，
        // 保证门控只在本场景生效
        PrologueGameplayGates.Active = true;
        PrologueGameplayGates.CanShoot = false;
        PrologueGameplayGates.CanInspect = false;
        PrologueGameplayGates.CanKnifeAttack = false;
        PrologueGameplayGates.CanOpenInventory = false;
    }

    private void OnDisable()
    {
        PrologueGameplayGates.Active = false;
    }

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

            prologueTarget.OnBurstHit += OnTk07TargetBurstHit;
        }

        // tk07：订阅全自动步枪的长按事件
        if (tk07Weapon != null)
        {
            tk07Weapon.BurstStarted += OnTk07BurstStarted;
            tk07Weapon.BurstEnded += OnTk07BurstEnded;
        }

        BulletHandle.BulletLaunched += OnTk07BulletLaunched;
        BulletHandle.BulletSettled += OnTk07BulletSettled;

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

                target.OnRequiredHitsReached += () =>
                    OnTk05TargetKnockedDown(target);

                target.OnHeadHit += () =>
                    OnTk06TargetHeadHit(target);

                tk04Targets.Add(target);
            }
        }

        // 收集 tk08 匕首靶：Targets_KnifeAttack 下每个直接子物体
        // 为一具靶子，没有计数器的自动补挂，并订阅匕首击倒事件
        if (tk08TargetsRoot != null)
        {
            foreach (Transform child in tk08TargetsRoot)
            {
                PrologueTarget target =
                    child.GetComponent<PrologueTarget>();

                if (target == null)
                {
                    target = child.gameObject
                        .AddComponent<PrologueTarget>();
                }

                target.OnKnifeHit += () =>
                    OnTk08TargetKnockedDown(target);

                tk08Targets.Add(target);
            }
        }
    }

    void Update()
    {
        Set();
    }

    private void Set()
    {
        // 每帧刷新操作门控：射击要求玩家在踏板触发器内且移动被
        // 剧情锁定；检视/匕首分别由 pr_tk_02 / pr_tk_08 的任务
        // Flag 解锁（Flag 置位后保持，任务结束后仍然可用）
        PrologueGameplayGates.CanShoot =
            stoolPlayerInside &&
            playerController != null &&
            playerController.movementLocked;

        PrologueGameplayGates.CanInspect =
            DialogueFlags.HasFlag(
                "pr_tk_02_glockshoottarget_unfinished");

        PrologueGameplayGates.CanKnifeAttack =
            DialogueFlags.HasFlag(
                "pr_tk_08_knifeattack_unfinished");

        // 背包在 pr_tk_09 的拾取武器模型目标完成后才可打开
        PrologueGameplayGates.CanOpenInventory =
            IsTk09PickWeaponModelsObjectiveComplete();

        // 第一段对话结束后启用场景中的Glock的PickableItem脚本组件
        if (DialogueFlags.HasFlag("pr_tk_01_pickweapon_unfinished"))
        {
            glockPickableItem.enabled = true;
        }
        // 拾取Glock后player身上的Glock随之启用
        // （拾取AK47换枪后不再重新启用Glock）
        if (!glock.activeSelf &&
            !DialogueFlags.HasFlag("pr_tk_05_pickak47_finished"))
        {
            player_Glock.SetActive(true);
        }

        // 任务 pr_tk_05 接取后启用场景中AK47的PickableItem脚本组件
        if (DialogueFlags.HasFlag(
            "pr_tk_05_ak47shootalltargets_unfinished"))
        {
            ak47PickableItem.enabled = true;
        }

        // 任务 pr_tk_02 接取后启用AmmoBox的PickableItem脚本组件
        if (DialogueFlags.HasFlag(
            "pr_tk_02_glockshoottarget_unfinished"))
        {
            tk02AmmoBoxPickableItem.enabled = true;
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

        // 拾取AK47后：收起Glock、换上AK47，踏板再次绿色高亮闪烁，
        // 靶子在玩家踩上踏板后才依次立起
        if (stage == Stage.Tk04Finished &&
            DialogueFlags.HasFlag("pr_tk_05_pickak47_finished"))
        {
            player_Glock.SetActive(false);
            player_AK47.SetActive(true);
            StartStoolHighlight();
            stage = Stage.Tk05HighlightStool;
        }

        // 任务 pr_tk_06 接取后：踏板再次绿色高亮闪烁，
        // 靶子在玩家踩上踏板后才依次立起
        if (stage == Stage.Tk05Finished &&
            DialogueFlags.HasFlag("pr_tk_06_ak47shoothead_unfinished"))
        {
            StartStoolHighlight();
            stage = Stage.Tk06HighlightStool;
        }

        // 任务 pr_tk_07 接取后：踏板再次绿色高亮闪烁，
        // TargetMain 在玩家踩上踏板后才立起
        if (stage == Stage.Tk06Finished &&
            DialogueFlags.HasFlag(
                "pr_tk_07_recoilcontrol_unfinished"
            ))
        {
            StartStoolHighlight();
            stage = Stage.Tk07HighlightStool;
        }

        // 任务 pr_tk_08 接取后：三具匕首靶红色高亮闪烁，
        // 并开启匕首命中统计（此后匕首一刀即倒）
        if (stage == Stage.Tk07Finished &&
            DialogueFlags.HasFlag(
                "pr_tk_08_knifeattack_unfinished"
            ))
        {
            foreach (PrologueTarget target in tk08Targets)
            {
                if (target != null)
                {
                    target.knifeCountingEnabled = true;
                }
            }

            StartKnifeTargetHighlights();
            stage = Stage.Tk08Active;
        }

        // 任务 pr_tk_09 接取后：启用 Vector/P90 模型的拾取，
        // 并进入"打开一次背包"的检查阶段
        if (stage == Stage.Tk08Finished &&
            DialogueFlags.HasFlag(
                "pr_tk_09_pickweaponmodels_unfinished"
            ))
        {
            vectorPickableItem.enabled = true;
            p90PickableItem.enabled = true;
            DialogueFlags.SetFlag(
                "pr_tk_09_pickweaponmodels_finished",
                true
            );

            stage = Stage.Tk09WaitingInventoryOpen;
        }

        // tk09：背包打开一次并关闭后，完成检查背包目标并设置 Flag。
        // InventoryContainer 由 InventoryInput 开关，直接轮询其
        // 激活状态即可感知打开/关闭。
        if (stage == Stage.Tk09WaitingInventoryOpen)
        {
            if (tk09RadialView == null)
            {
                tk09RadialView =
                    FindObjectOfType<InventoryRadialView>(true);
            }

            if (tk09RadialView != null)
            {
                if (tk09RadialView.gameObject.activeInHierarchy)
                {
                    tk09InventoryOpened = true;
                }
                else if (tk09InventoryOpened)
                {
                    FinishTaskTk09();
                }
            }
        }

        // 木箱引导高亮：按当前 Flag 窗口高亮对应木箱
        UpdateWoodBoxHighlight();

        // 木箱高亮期间高亮罩透明度按闪烁频率往复
        if (woodBoxHighlightMaterial != null)
        {
            float pulse =
                Mathf.Sin(Time.time * woodBoxBlinkSpeed * Mathf.PI * 2f)
                    * 0.5f + 0.5f;

            Color color = woodBoxHighlightColor;
            color.a = woodBoxHighlightAlpha * pulse;
            woodBoxHighlightMaterial.SetColor("_HighlightColor", color);
        }

        // 高亮期间高亮罩透明度按闪烁频率在 0 与最大透明度之间往复，
        // Alpha 小于 1 时可透出踏板本色，不会整块糊成实心绿
        if ((stage == Stage.HighlightStool ||
             stage == Stage.Tk03HighlightStool ||
             stage == Stage.Tk04HighlightStool ||
             stage == Stage.Tk05HighlightStool ||
             stage == Stage.Tk06HighlightStool ||
             stage == Stage.Tk07HighlightStool) &&
            stoolHighlightMaterial != null)
        {
            float pulse =
                Mathf.Sin(Time.time * stoolBlinkSpeed * Mathf.PI * 2f)
                    * 0.5f + 0.5f;

            Color color = stoolHighlightColor;
            color.a = stoolHighlightAlpha * pulse;
            stoolHighlightMaterial.SetColor("_HighlightColor", color);
        }

        // tk08：匕首靶的高亮罩用专属参数闪烁（与踏板高亮互不影响）
        if (stage == Stage.Tk08Active &&
            knifeHighlightMaterial != null)
        {
            float pulse =
                Mathf.Sin(Time.time * tk08BlinkSpeed * Mathf.PI * 2f)
                    * 0.5f + 0.5f;

            Color color = tk08HighlightColor;
            color.a = tk08HighlightAlpha * pulse;
            knifeHighlightMaterial.SetColor("_HighlightColor", color);
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
        else if (stage == Stage.Tk05HighlightStool && stoolPlayerInside)
        {
            EnterStoolZoneTk05();
        }
        else if (stage == Stage.Tk06HighlightStool && stoolPlayerInside)
        {
            EnterStoolZoneTk06();
        }
        else if (stage == Stage.Tk07HighlightStool && stoolPlayerInside)
        {
            EnterStoolZoneTk07();
        }

        // tk07：长按进行中按 R 换弹视为中断本轮，与松手同流程结算
        if (stage == Stage.Tk07Shooting &&
            tk07BurstActive &&
            !tk07BurstSettling &&
            Input.GetKeyDown(KeyCode.R))
        {
            BeginTk07BurstSettle();
        }

        // tk07：长按进行中弹匣打空仍未达标：不会再有新弹，
        // 等在飞子弹全部落定后判定本轮失败
        if (stage == Stage.Tk07Shooting &&
            tk07BurstActive &&
            !tk07BurstSettling &&
            tk07Weapon != null &&
            tk07Weapon.currentMagazineAmmo == 0)
        {
            BeginTk07BurstSettle();
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
        StartCoroutine(
            StandTargets(Stage.Tk04Shooting, tk04StandInterval));
    }

    // tk05：玩家再次进入踏板触发器区域，流程同上
    private void EnterStoolZoneTk05()
    {
        StopStoolHighlight();
        LockPlayerMovement(true);
        stage = Stage.Tk05TargetsStanding;
        StartCoroutine(
            StandTargets(Stage.Tk05Shooting, tk05StandInterval));
    }

    // tk06：玩家再次进入踏板触发器区域，流程同上
    private void EnterStoolZoneTk06()
    {
        StopStoolHighlight();
        LockPlayerMovement(true);
        stage = Stage.Tk06TargetsStanding;
        StartCoroutine(
            StandTargets(Stage.Tk06Shooting, tk06StandInterval));
    }

    // tk07：玩家进入踏板触发器区域：停止高亮，锁定移动，
    // TargetMain 立起
    private void EnterStoolZoneTk07()
    {
        StopStoolHighlight();
        LockPlayerMovement(true);
        stage = Stage.Tk07TargetStanding;

        // 清掉上一阶段残留的命中计数（reached=true 会吞掉
        // 后续命中事件），立起完成后再开启计数
        prologueTarget.countingEnabled = false;
        prologueTarget.ResetCounter();

        StartCoroutine(RotateTargetX(90f, 0f));
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

    // tk04/tk05：玩家进入踏板后全场靶子依次立起（旋转 X 90→0）：
    // 起立顺序 = Targets 下的子物体顺序，相邻间隔 standInterval；
    // 全部立起完成后开启命中计数并进入 shootingStage 射击阶段
    private IEnumerator StandTargets(Stage shootingStage, float standInterval)
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

            yield return new WaitForSeconds(standInterval);
        }

        // 最后一具靶子还需转完 targetRotateDuration，
        // 此处等待结束后全部靶子必定立起完成
        yield return new WaitForSeconds(targetRotateDuration);

        tk04TargetsRemaining = tk04Targets.Count;

        foreach (PrologueTarget target in tk04Targets)
        {
            target.countingEnabled = true;
        }

        stage = shootingStage;
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
            KnockDownTarget(target.transform));
    }

    // tk05：单具靶子被击倒（由该靶 PrologueTarget 的事件驱动）：
    // AK47 击倒目标进度 +1，并让该靶子倒下
    private void OnTk05TargetKnockedDown(PrologueTarget target)
    {
        if (stage != Stage.Tk05Shooting)
        {
            return;
        }

        if (tk05Quest == null)
        {
            Debug.LogError(
                "PrologueController：未拖入任务资产 " +
                    "pr_tk_05_ak47shootalltargets，无法统计击倒进度。",
                this
            );

            return;
        }

        QuestManager.AddProgress(
            tk05Quest.QuestId,
            tk05ObjectiveId,
            1
        );

        StartCoroutine(
            KnockDownTarget(target.transform));
    }

    // tk06：子弹命中靶子头部碰撞体（由该靶 PrologueTarget 的
    // OnHeadHit 事件驱动）：爆头一枪即击倒，命中其他碰撞体
    // 无论多少次都不会倒（OnRequiredHitsReached 的处理均按
    // 阶段过滤，tk06 期间不会误触发击倒）。
    private void OnTk06TargetHeadHit(PrologueTarget target)
    {
        if (stage != Stage.Tk06Shooting)
        {
            return;
        }

        if (tk06Quest == null)
        {
            Debug.LogError(
                "PrologueController：未拖入任务资产 " +
                    "pr_tk_06_ak47shoothead，无法统计爆头进度。",
                this
            );

            return;
        }

        // 立即关闭该靶的计数：防止倒下动画期间再次被爆头
        // 重复计入进度（countingEnabled=false 后 RegisterHit
        // 不再触发 OnHeadHit）
        target.countingEnabled = false;

        QuestManager.AddProgress(
            tk06Quest.QuestId,
            tk06ObjectiveId,
            1
        );

        StartCoroutine(
            KnockDownTarget(target.transform));
    }

    // tk04/tk05/tk06：靶子倒下动画播完后递减剩余数，全部倒下时
    // 按当前阶段完成对应任务
    private IEnumerator KnockDownTarget(Transform target)
    {
        yield return RotateTargetTk04(target, 0f, 90f);

        tk04TargetsRemaining--;

        if (tk04TargetsRemaining <= 0)
        {
            if (stage == Stage.Tk06Shooting)
            {
                FinishTaskTk06();
            }
            else if (stage == Stage.Tk05Shooting)
            {
                FinishTaskTk05();
            }
            else
            {
                FinishTaskTk04();
            }
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

    // tk05：全部靶子倒下后：确保击倒目标完成、设置 Flag、解锁移动
    private void FinishTaskTk05()
    {
        // 每次击倒已经累计过进度，这里兜底把目标直接置为完成，
        // 防止任务资产中的 requiredAmount 与实际击倒数不一致
        if (tk05Quest != null)
        {
            QuestManager.SetObjectiveComplete(
                tk05Quest.QuestId,
                tk05ObjectiveId
            );
        }

        DialogueFlags.SetFlag(tk05CompleteFlag, true);
        LockPlayerMovement(false);
        stage = Stage.Tk05Finished;
    }

    // tk06：全部靶子爆头击倒后：确保爆头目标完成、设置 Flag、
    // 解锁移动
    private void FinishTaskTk06()
    {
        // 每次爆头击倒已经累计过进度，这里兜底把目标直接置为
        // 完成，防止任务资产中的 requiredAmount 与实际击倒数不一致
        if (tk06Quest != null)
        {
            QuestManager.SetObjectiveComplete(
                tk06Quest.QuestId,
                tk06ObjectiveId
            );
        }

        DialogueFlags.SetFlag(tk06CompleteFlag, true);
        LockPlayerMovement(false);
        stage = Stage.Tk06Finished;
    }

    // tk07：子弹命中 TargetMain（由 PrologueTarget 的 OnBurstHit
    // 事件驱动，任意碰撞体均算有效）：按开火时刻归属，只统计
    // 当前长按轮次内射出的子弹；达标立即判成功，靶子倒下。
    private void OnTk07TargetBurstHit(int burstId)
    {
        if (stage != Stage.Tk07Shooting ||
            !tk07BurstActive ||
            burstId != tk07BurstId)
        {
            return;
        }

        if (tk07Quest == null)
        {
            Debug.LogError(
                "PrologueController：未拖入任务资产 " +
                    "pr_tk_07_recoilcontrol，无法统计命中进度。",
                this
            );

            return;
        }

        tk07BurstHits++;

        QuestManager.AddProgress(
            tk07Quest.QuestId,
            tk07ObjectiveId,
            1
        );

        QuestAsset.QuestObjective objective =
            tk07Quest.GetObjective(tk07ObjectiveId);

        if (objective == null ||
            tk07BurstHits < objective.requiredAmount)
        {
            return;
        }

        // 达标即成功（不必等松手）：关闭该靶计数防止倒下期间
        // 重复计入，靶子倒下后完成任务
        prologueTarget.countingEnabled = false;
        stage = Stage.Tk07TargetFalling;
        StartCoroutine(RotateTargetX(0f, 90f));
    }

    // tk07：玩家按下鼠标左键开新一轮长按（由 WeaponController
    // 的事件驱动）。上一轮若仍在等待在飞子弹落定，视为放弃
    // 上一轮，先按失败清零再开新一轮。
    private void OnTk07BurstStarted(int burstId)
    {
        if (stage != Stage.Tk07Shooting)
        {
            return;
        }

        if (tk07BurstActive || tk07BurstSettling)
        {
            ResolveTk07Burst();
        }

        tk07BurstId = burstId;
        tk07BurstHits = 0;
        tk07BulletsInFlight = 0;
        tk07BurstActive = true;
        tk07BurstSettling = false;
    }

    // tk07：玩家松开鼠标左键（由 WeaponController 的事件驱动）：
    // 进入结算等待，本轮射出的在飞子弹命中仍计入，全部落定后
    // 未达标则清零
    private void OnTk07BurstEnded()
    {
        if (stage == Stage.Tk07Shooting && tk07BurstActive)
        {
            BeginTk07BurstSettle();
        }
    }

    // tk07：子弹发射广播（由 BulletHandle 的静态事件驱动）：
    // 属于当前长按轮次的子弹计入在飞数
    private void OnTk07BulletLaunched(BulletHandle bullet)
    {
        if (stage == Stage.Tk07Shooting &&
            tk07BurstActive &&
            bullet.BurstId == tk07BurstId)
        {
            tk07BulletsInFlight++;
        }
    }

    // tk07：子弹落定广播（命中或超时回收）：在飞数递减，
    // 结算等待中全部落定时判定本轮
    private void OnTk07BulletSettled(BulletHandle bullet)
    {
        if (bullet.BurstId != tk07BurstId ||
            tk07BulletsInFlight <= 0)
        {
            return;
        }

        tk07BulletsInFlight--;

        if (tk07BurstSettling && tk07BulletsInFlight == 0)
        {
            ResolveTk07Burst();
        }
    }

    // tk07：进入结算等待（松手/打空/长按中按 R 换弹共用入口）：
    // 先等本轮在飞子弹全部落定（命中先于落定上报，不会漏计）
    private void BeginTk07BurstSettle()
    {
        tk07BurstSettling = true;

        if (tk07BulletsInFlight == 0)
        {
            ResolveTk07Burst();
        }
    }

    // tk07：本轮结算：未达标（或放弃上一轮开新一轮）时把本地
    // 计数与任务系统进度一起清零，HUD 才会跟着归零
    private void ResolveTk07Burst()
    {
        tk07BurstActive = false;
        tk07BurstSettling = false;

        if (stage != Stage.Tk07Shooting || tk07BurstHits <= 0)
        {
            return;
        }

        tk07BurstHits = 0;

        if (tk07Quest != null)
        {
            QuestManager.SetProgress(
                tk07Quest.QuestId,
                tk07ObjectiveId,
                0
            );
        }
    }

    // tk07：达标后靶子倒下：确保目标完成、设置 Flag、解锁移动
    private void FinishTaskTk07()
    {
        // 每次命中已经累计过进度，这里兜底把目标直接置为完成，
        // 防止任务资产中的 requiredAmount 与本地计数不一致
        if (tk07Quest != null)
        {
            QuestManager.SetObjectiveComplete(
                tk07Quest.QuestId,
                tk07ObjectiveId
            );
        }

        DialogueFlags.SetFlag(tk07CompleteFlag, true);
        LockPlayerMovement(false);
        stage = Stage.Tk07Finished;
    }

    // tk08：给全部匕首靶挂绿色高亮罩（复用踏板高亮机制，
    // 每靶一组罩子，击倒后单独熄灭）
    private void StartKnifeTargetHighlights()
    {
        // 防御性收尾：上次高亮未正常熄灭时避免罩子叠罩子
        StopKnifeTargetHighlights();

        Shader shader = Shader.Find("Custom/Stool Highlight");

        if (shader == null)
        {
            Debug.LogError(
                "PrologueController：找不到 Custom/Stool Highlight " +
                    "Shader，匕首靶无法高亮。",
                this
            );

            return;
        }

        knifeHighlightMaterial = new Material(shader);
        knifeHighlightMaterial.SetColor(
            "_HighlightColor",
            tk08HighlightColor
        );

        foreach (PrologueTarget target in tk08Targets)
        {
            if (target == null)
            {
                continue;
            }

            var overlays = new List<GameObject>();
            knifeHighlightOverlays[target] = overlays;

            foreach (
                Renderer renderer in
                    target.GetComponentsInChildren<Renderer>()
            )
            {
                CreateStoolHighlightOverlay(
                    renderer,
                    knifeHighlightMaterial,
                    overlays,
                    tk08HighlightScale
                );
            }
        }
    }

    // tk08：熄灭指定匕首靶的高亮罩（被砍倒时调用）
    private void RemoveKnifeTargetHighlight(PrologueTarget target)
    {
        if (!knifeHighlightOverlays.TryGetValue(
                target,
                out List<GameObject> overlays
            ))
        {
            return;
        }

        knifeHighlightOverlays.Remove(target);

        foreach (GameObject overlay in overlays)
        {
            if (overlay != null)
            {
                Destroy(overlay);
            }
        }
    }

    // tk08：熄灭全部匕首靶高亮罩并销毁共享材质
    private void StopKnifeTargetHighlights()
    {
        foreach (
            List<GameObject> overlays in
                knifeHighlightOverlays.Values
        )
        {
            foreach (GameObject overlay in overlays)
            {
                if (overlay != null)
                {
                    Destroy(overlay);
                }
            }
        }

        knifeHighlightOverlays.Clear();

        if (knifeHighlightMaterial != null)
        {
            Destroy(knifeHighlightMaterial);
            knifeHighlightMaterial = null;
        }
    }

    // tk08：一具匕首靶被砍倒（由该靶 PrologueTarget 的 OnKnifeHit
    // 事件驱动）：击倒目标进度 +1，熄灭该靶高亮并让它倒下；
    // 全部倒下后完成任务
    private void OnTk08TargetKnockedDown(PrologueTarget target)
    {
        if (stage != Stage.Tk08Active)
        {
            return;
        }

        if (tk08Quest == null)
        {
            Debug.LogError(
                "PrologueController：未拖入任务资产 " +
                    "pr_tk_08_knifeattack，无法统计击倒进度。",
                this
            );

            return;
        }

        QuestManager.AddProgress(
            tk08Quest.QuestId,
            tk08ObjectiveId,
            1
        );

        RemoveKnifeTargetHighlight(target);

        StartCoroutine(
            RotateTargetTk04(target.transform, 0f, 90f));

        QuestAsset.QuestObjective objective =
            tk08Quest.GetObjective(tk08ObjectiveId);

        if (objective == null)
        {
            return;
        }

        if (QuestManager.GetObjectiveCurrent(
                tk08Quest.QuestId,
                tk08ObjectiveId
            ) >= objective.requiredAmount)
        {
            FinishTaskTk08();
        }
    }

    // tk08：全部匕首靶倒下后：确保目标完成、设置 Flag、
    // 熄灭剩余高亮
    private void FinishTaskTk08()
    {
        // 每次击倒已经累计过进度，这里兜底把目标直接置为完成，
        // 防止任务资产中的 requiredAmount 与实际击倒数不一致
        if (tk08Quest != null)
        {
            QuestManager.SetObjectiveComplete(
                tk08Quest.QuestId,
                tk08ObjectiveId
            );
        }

        DialogueFlags.SetFlag(tk08CompleteFlag, true);
        StopKnifeTargetHighlights();
        stage = Stage.Tk08Finished;
    }

    // tk09：玩家打开并关闭背包后：确保检查背包目标完成、
    // 设置 Flag（pickweaponmodels 的 Flag 在阶段进入时已设置）
    private void FinishTaskTk09()
    {
        // 兜底把目标直接置为完成，防止任务资产中的
        // requiredAmount 与实际开关次数不一致
        if (tk09CheckInventoryQuest != null)
        {
            QuestManager.SetObjectiveComplete(
                tk09CheckInventoryQuest.QuestId,
                tk09CheckInventoryObjectiveId
            );
        }

        DialogueFlags.SetFlag(
            tk09CheckInventoryCompleteFlag,
            true
        );

        stage = Stage.Tk09Finished;
    }

    // 木箱引导高亮：按 Flag 窗口决定当前应高亮的木箱——
    // tk01：pr_tk_01_pickweapon_unfinished → pickweapon_finished；
    // tk05：pr_tk_05_ak47shootalltargets_unfinished → pickak47_finished；
    // tk09：pr_tk_09_pickweaponmodels_unfinished →
    //       pr_tk_09_pickweaponmodels 目标完成。
    // 窗口按剧情顺序推进、互不重叠，目标变化时换高亮对象。
    private void UpdateWoodBoxHighlight()
    {
        GameObject desired = null;

        if (DialogueFlags.HasFlag(
                "pr_tk_01_pickweapon_unfinished") &&
            !DialogueFlags.HasFlag(
                "pr_tk_01_pickweapon_finished"))
        {
            desired = tk01WoodBox;
        }
        else if (DialogueFlags.HasFlag(
                "pr_tk_05_ak47shootalltargets_unfinished") &&
            !DialogueFlags.HasFlag(
                "pr_tk_05_pickak47_finished"))
        {
            desired = tk05WoodBox;
        }
        else if (DialogueFlags.HasFlag(
                "pr_tk_09_pickweaponmodels_unfinished") &&
            !IsTk09PickWeaponModelsObjectiveComplete())
        {
            desired = tk09WoodBox;
        }

        if (desired == woodBoxHighlightTarget)
        {
            return;
        }

        StopWoodBoxHighlight();

        woodBoxHighlightTarget = desired;

        if (desired != null)
        {
            StartWoodBoxHighlight(desired);
        }
    }

    // pr_tk_09 的拾取武器模型目标是否已完成（以任务系统中的
    // 实际进度为准，与 HUD 显示始终一致）
    private bool IsTk09PickWeaponModelsObjectiveComplete()
    {
        if (tk09PickWeaponModelsQuest == null)
        {
            return false;
        }

        QuestAsset.QuestObjective objective =
            tk09PickWeaponModelsQuest.GetObjective(
                tk09PickWeaponModelsObjectiveId);

        if (objective == null)
        {
            return false;
        }

        return QuestManager.GetObjectiveCurrent(
            tk09PickWeaponModelsQuest.QuestId,
            tk09PickWeaponModelsObjectiveId
        ) >= objective.requiredAmount;
    }

    // 给指定木箱的全部渲染器挂高亮罩（复用踏板高亮机制）
    private void StartWoodBoxHighlight(GameObject woodBox)
    {
        Shader shader = Shader.Find("Custom/Stool Highlight");

        if (shader == null)
        {
            Debug.LogError(
                "PrologueController：找不到 Custom/Stool Highlight " +
                    "Shader，木箱无法高亮。",
                this
            );

            return;
        }

        woodBoxHighlightMaterial = new Material(shader);
        woodBoxHighlightMaterial.SetColor(
            "_HighlightColor",
            woodBoxHighlightColor
        );

        foreach (
            Renderer renderer in
                woodBox.GetComponentsInChildren<Renderer>()
        )
        {
            CreateStoolHighlightOverlay(
                renderer,
                woodBoxHighlightMaterial,
                woodBoxHighlightOverlays,
                woodBoxHighlightScale
            );
        }
    }

    // 销毁木箱高亮罩子物体与材质，停止高亮
    private void StopWoodBoxHighlight()
    {
        foreach (GameObject overlay in woodBoxHighlightOverlays)
        {
            if (overlay != null)
            {
                Destroy(overlay);
            }
        }

        woodBoxHighlightOverlays.Clear();

        if (woodBoxHighlightMaterial != null)
        {
            Destroy(woodBoxHighlightMaterial);
            woodBoxHighlightMaterial = null;
        }
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
        else if (stage == Stage.Tk05HighlightStool)
        {
            EnterStoolZoneTk05();
        }
        else if (stage == Stage.Tk06HighlightStool)
        {
            EnterStoolZoneTk06();
        }
        else if (stage == Stage.Tk07HighlightStool)
        {
            EnterStoolZoneTk07();
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
        else if (stage == Stage.Tk07TargetStanding)
        {
            // TargetMain 立起完成：重置连射状态并开启计数，
            // 等待玩家单轮长按命中达标
            tk07BurstId = 0;
            tk07BurstHits = 0;
            tk07BulletsInFlight = 0;
            tk07BurstActive = false;
            tk07BurstSettling = false;

            prologueTarget.countingEnabled = true;
            stage = Stage.Tk07Shooting;
        }
        else if (stage == Stage.Tk07TargetFalling)
        {
            FinishTaskTk07();
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
            CreateStoolHighlightOverlay(
                renderer,
                stoolHighlightMaterial,
                stoolHighlightOverlays,
                stoolHighlightScale
            );
        }
    }

    // 为指定渲染器创建微放大的高亮罩子物体：直接复用原网格
    // （共享资源，不做副本），透明罩面浮在踏板表面之外，
    // 靠近相机的各面叠加在踏板贴图之上泛绿闪烁。
    // 材质与罩子清单由调用方提供（踏板与匕首靶共用此机制）。
    private void CreateStoolHighlightOverlay(
        Renderer sourceRenderer,
        Material highlightMaterial,
        List<GameObject> overlayList,
        float overlayScale)
    {
        MeshFilter sourceFilter =
            sourceRenderer.GetComponent<MeshFilter>();

        if (sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            Debug.LogWarning(
                "PrologueController：高亮目标下存在没有网格的渲染器，" +
                    "该渲染器不参与高亮。",
                sourceRenderer
            );

            return;
        }

        GameObject overlay = new GameObject("StoolHighlightOverlay");

        overlayList.Add(overlay);

        // 恒等局部变换挂为源渲染器的子物体保证几何重合，
        // 再整体微放大让罩面浮出表面。
        overlay.transform.SetParent(sourceRenderer.transform, false);
        overlay.transform.localScale =
            Vector3.one * overlayScale;

        MeshFilter overlayFilter = overlay.AddComponent<MeshFilter>();
        overlayFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer overlayRenderer =
            overlay.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterial = highlightMaterial;
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
