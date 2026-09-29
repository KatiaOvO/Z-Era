using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// NPC 对话触发器，挂在人物根物体上。
/// 准星指向本 NPC 且在交互距离内时显示提示，
/// 按下交互键开始对话。
/// 支持后续对话样式：
/// - 默认：每次对话使用默认资产，内容相同；
/// - playOnce：只能完整对话一次；
/// - talkedFlag：对话结束后自动设置标记，
///   配合条件对话列表可在下次交互时切换为新内容；
/// - 条件对话：按标记决定本次使用哪个对话资产，
///   标记不满足且无默认资产时无法对话。
/// </summary>
public class NPCDialogue : MonoBehaviour
{
    [Serializable]
    public class ConditionalDialogue
    {
        [Tooltip("这里列出的标记全部已设置时，才使用该对话资产")]
        public string[] requiredFlags;

        [Tooltip("条件满足时使用的对话资产")]
        public DialogueAsset dialogue;
    }

    [Header("对话")]

    [Tooltip("默认对话资产：条件对话都不满足时使用，留空则条件不满足时无法对话")]
    [SerializeField]
    private DialogueAsset dialogue;

    [Tooltip("按顺序检查的条件对话：第一个标记全部满足的条目优先生效")]
    [SerializeField]
    private List<ConditionalDialogue> conditionalDialogues =
        new List<ConditionalDialogue>();

    [Tooltip("对话完整结束后自动设置的标记，留空则不设置（配合条件对话实现后续不同内容）")]
    [SerializeField]
    private string talkedFlag;

    [Tooltip("勾选后该 NPC 只能完整对话一次，之后不再提供交互")]
    [SerializeField]
    private bool playOnce;

    [Tooltip("开始对话的交互按键")]
    [SerializeField]
    private KeyCode interactKey = KeyCode.E;

    [Tooltip("准星指向本 NPC 且距离小于该值时才能开始对话（米）")]
    [Min(0.1f)]
    [SerializeField]
    private float interactDistance = 2.5f;

    [Header("提示")]

    [Tooltip("交互提示文本，留空则不显示提示")]
    [SerializeField]
    private TMP_Text promptText;

    [Tooltip("提示文本格式，{0} 会被替换为按键名")]
    [SerializeField]
    private string promptFormat = "按 {0} 对话";

    [Tooltip("NPC 尚未介绍自己时显示的提示文本")]
    [SerializeField]
    private string unknownPrompt = "？？？";

    [Tooltip("播放到该对话节点 ID 时，读取节点 Speaker Name 并更新提示")]
    [SerializeField]
    private string nameRevealNodeId;

    [Header("朝向限制")]

    [Tooltip("是否要求玩家站在 NPC 朝向的指定角度范围内才能对话（防止绕到 NPC 背后对话）")]
    [SerializeField]
    private bool requireForwardZone = true;

    [Tooltip("允许对话的半角（度）：玩家相对 NPC 正前方偏离不超过该角度时可对话，180 等于不限制")]
    [Range(0f, 180f)]
    [SerializeField]
    private float interactHalfAngle = 90f;

    private Camera cachedCamera;
    private DialogueRunner dialogueRunner;

    // 已发起对话、等待其结束（结束后设置标记）。
    private bool awaitingEnd;

    // 当前等待结束的对话资产，用于对话结束后读取 speakerName。
    private DialogueAsset awaitingDialogueAsset;

    // 是否已经完整对话过一次。
    private bool hasTalked;

    // 是否已经从对话资产中取得 NPC 名字。
    private bool hasIntroduced;

    private void Awake()
    {
        // 重新加载场景后，根据 talkedFlag 恢复已介绍状态。
        if (!string.IsNullOrWhiteSpace(talkedFlag) &&
            DialogueFlags.HasFlag(talkedFlag))
        {
            hasIntroduced = true;
        }
    }

    private void OnEnable()
    {
        EnsureDialogueRunnerSubscription();
    }

    private void OnDisable()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.DialogueNodeShown -= OnDialogueNodeShown;
            dialogueRunner = null;
        }
    }

    private void Update()
    {
        EnsureDialogueRunnerSubscription();

        // 已发起的对话结束后：记录"聊过"并设置 talkedFlag。
        // 结束序列期间状态已是 Idle，标记会立即生效。
        if (awaitingEnd &&
            (DialogueRunner.Instance == null ||
             DialogueRunner.Instance.CurrentState ==
                 DialogueRunner.State.Idle))
        {
            awaitingEnd = false;
            hasTalked = true;

            if (!string.IsNullOrWhiteSpace(talkedFlag))
            {
                DialogueFlags.SetFlag(talkedFlag, true);
            }

            awaitingDialogueAsset = null;
        }

        bool talkAvailable = IsTalkAvailable();

        // 无法对话、对话进行中、鼠标未锁定（背包打开等）时不检测。
        if (!talkAvailable ||
            (DialogueRunner.Instance != null &&
             DialogueRunner.Instance.CurrentState !=
                 DialogueRunner.State.Idle) ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            UpdatePrompt(false);
            return;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;

            if (cachedCamera == null)
            {
                return;
            }
        }

        if (CanInteract() &&
            Input.GetKeyDown(interactKey))
        {
            DialogueAsset asset = SelectDialogue();

            if (asset != null)
            {
                awaitingEnd = true;
                awaitingDialogueAsset = asset;
                DialogueRunner.StartDialogue(asset);
            }

            UpdatePrompt(false);
            return;
        }

        UpdatePrompt(CanInteract());
    }

    // 当前是否可对话：没有 playOnce 限制，且有可用资产。
    private bool IsTalkAvailable()
    {
        if (playOnce && hasTalked)
        {
            return false;
        }

        return SelectDialogue() != null;
    }

    // 选择本次使用的对话资产：条件列表从上到下，
    // 第一个标记全部满足的条目优先生效，否则用默认资产。
    private DialogueAsset SelectDialogue()
    {
        foreach (
            ConditionalDialogue conditional in
                conditionalDialogues
        )
        {
            if (conditional == null || conditional.dialogue == null)
            {
                continue;
            }

            bool allSet = true;

            if (conditional.requiredFlags != null)
            {
                foreach (string flag in conditional.requiredFlags)
                {
                    if (!DialogueFlags.HasFlag(flag))
                    {
                        allSet = false;
                        break;
                    }
                }
            }

            if (allSet)
            {
                return conditional.dialogue;
            }
        }

        return dialogue;
    }

    private void EnsureDialogueRunnerSubscription()
    {
        if (dialogueRunner != null ||
            DialogueRunner.Instance == null)
        {
            return;
        }

        dialogueRunner = DialogueRunner.Instance;
        dialogueRunner.DialogueNodeShown += OnDialogueNodeShown;
    }

    private void OnDialogueNodeShown(
        DialogueAsset sourceDialogue,
        DialogueAsset.DialogueNode node
    )
    {
        if (hasIntroduced ||
            sourceDialogue != awaitingDialogueAsset ||
            node == null ||
            string.IsNullOrWhiteSpace(nameRevealNodeId) ||
            node.nodeId != nameRevealNodeId ||
            string.IsNullOrWhiteSpace(node.speakerName))
        {
            return;
        }

        promptFormat = node.speakerName;
        hasIntroduced = true;
    }

    private bool CanInteract()
    {        Ray ray = cachedCamera.ScreenPointToRay(
            new Vector3(
                Screen.width * 0.5f,
                Screen.height * 0.5f,
                0f
            )
        );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactDistance
            ))
        {
            return false;
        }

        // 命中的碰撞体必须属于本 NPC（允许命中子物体的碰撞体）。
        NPCDialogue owner =
            hit.collider.GetComponentInParent<NPCDialogue>();

        if (owner != this)
        {
            return false;
        }

        // 朝向限制：玩家必须站在 NPC 正前方 interactHalfAngle
        // 度的扇形范围内。水平面计算，忽略高度差，
        // 避免玩家站得稍高/稍低时被误判到区域外。
        if (requireForwardZone && interactHalfAngle < 180f)
        {
            Vector3 npcForward = transform.forward;
            Vector3 toPlayer =
                cachedCamera.transform.position - transform.position;

            npcForward.y = 0f;
            toPlayer.y = 0f;

            // 水平投影接近零（玩家在正上/正下方）时方向未定义，
            // 按不通过处理。
            if (toPlayer.sqrMagnitude < 0.0001f ||
                Vector3.Angle(npcForward, toPlayer) >
                    interactHalfAngle)
            {
                return false;
            }
        }

        return true;
    }

    // 在 Scene 视图可视化对话朝向区域：半透明扇面 + 边界线 +
    // 正方向指示线，半径取对话交互距离，仅选中本物体时绘制。
    // 编辑器专用，不参与打包。
    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!requireForwardZone)
        {
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;

        // 根节点朝向垂直于水平面时退化，兜底用世界前方
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        float radius = Mathf.Max(0.5f, interactDistance);
        Vector3 center = transform.position;

        Quaternion leftEdge =
            Quaternion.AngleAxis(-interactHalfAngle, Vector3.up);

        Handles.color = new Color(1f, 0.92f, 0.35f, 0.15f);
        Handles.DrawSolidArc(
            center,
            Vector3.up,
            leftEdge * forward,
            interactHalfAngle * 2f,
            radius
        );

        Handles.color = new Color(1f, 0.92f, 0.35f, 0.9f);
        Handles.DrawWireArc(
            center,
            Vector3.up,
            leftEdge * forward,
            interactHalfAngle * 2f,
            radius
        );

        Gizmos.color = new Color(1f, 0.92f, 0.35f, 0.9f);
        Gizmos.DrawLine(
            center,
            center + leftEdge * forward * radius
        );
        Gizmos.DrawLine(
            center,
            center + Quaternion.AngleAxis(
                interactHalfAngle, Vector3.up) * forward * radius
        );

        // 正方向指示线略长一截，便于辨认扇形朝向
        Gizmos.color = Color.white;
        Gizmos.DrawLine(
            center,
            center + forward * radius * 1.15f
        );
#endif
    }

    private void UpdatePrompt(
        bool canInteract
    )
    {
        if (promptText == null)
        {
            return;
        }

        if (!canInteract)
        {
            promptText.text = string.Empty;
            return;
        }

        string activePrompt = hasIntroduced
            ? promptFormat
            : unknownPrompt;

        promptText.text = string.Format(
            activePrompt,
            interactKey
        );
    }
}
