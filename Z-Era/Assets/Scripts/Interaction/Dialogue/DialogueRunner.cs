using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 对话推进逻辑：状态机 + 玩家控制锁 + 跳转/选项条件判断。
/// 挂在对话 Canvas 根物体上（与 DialogueUIController 同物体或子物体），
/// 对话进行中整块画布启用，结束后整体隐藏。
/// 外部通过 DialogueRunner.StartDialogue(asset) 开始对话。
/// </summary>
public class DialogueRunner : MonoBehaviour
{
    public enum State
    {
        // 未在对话中，画布整体隐藏。
        Idle,
        // 正文打字中，点击 = 立即显示全文。
        Typing,
        // 正文显示完毕，等待点击进入下一节点。
        WaitingInput,
        // 选项已显示，必须点击某个选项。
        WaitingChoice
    }

    [Header("引用")]

    [Tooltip("对话 UI 表现层，留空则取同物体上的组件")]
    [SerializeField]
    private DialogueUIController ui;

    [Tooltip("对话期间隐藏的 HUD Canvas，留空则不隐藏")]
    [SerializeField]
    private GameObject hudCanvas;

    [Tooltip("玩家控制器，留空时自动查找")]
    [SerializeField]
    private PlayerController playerController;

    [Header("推进输入")]

    [Tooltip("除鼠标左键外，可用于推进对话的按键")]
    [SerializeField]
    private KeyCode continueKey = KeyCode.Space;

    [Header("事件")]

    [Tooltip("对话开始时触发")]
    [SerializeField]
    private UnityEvent onDialogueStart;

    [Tooltip("对话结束时触发")]
    [SerializeField]
    private UnityEvent onDialogueEnd;

    public State CurrentState { get; private set; } = State.Idle;

    public static DialogueRunner Instance { get; private set; }

    // 对话结束时把结束的资产传出去，便于剧情系统接续。
    public event Action<DialogueAsset> DialogueStarted;
    public event Action DialogueEnded;

    private DialogueAsset asset;
    private DialogueAsset.DialogueNode currentNode;

    // 与 InventoryInput 相同的控制锁存模式：
    // 记录原本启用的组件，对话结束后精确恢复。
    private readonly List<Behaviour> disabledBehaviours =
        new List<Behaviour>();

    // 对话期间暂停的玩家音源（如脚步声），结束后恢复。
    private readonly List<AudioSource> pausedAudioSources =
        new List<AudioSource>();

    // 对话正在收尾（延迟一帧恢复控制）期间，
    // 拒绝重复开始新对话。
    private bool endingDialogue;

    private bool hasPreviousCursorState;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    private bool hasPreviousHudState;
    private bool previousHudActiveState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (ui == null)
        {
            ui = GetComponent<DialogueUIController>();
        }

        if (ui == null)
        {
            Debug.LogError(
                "DialogueRunner：找不到 DialogueUIController。",
                this
            );
        }

        if (ui != null)
        {
            ui.TypewriterCompleted += OnTypewriterCompleted;
        }

        // 空闲时整块画布隐藏，开始对话时再整体启用。
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 开始一段对话。对话进行中重复调用会被忽略。
    /// </summary>
    public static void StartDialogue(DialogueAsset dialogueAsset)
    {
        if (Instance == null)
        {
            Debug.LogError(
                "DialogueRunner：场景中没有 DialogueRunner，无法开始对话。"
            );
            return;
        }

        Instance.BeginDialogue(dialogueAsset);
    }

    public void BeginDialogue(DialogueAsset dialogueAsset)
    {
        if (dialogueAsset == null ||
            dialogueAsset.Nodes == null ||
            dialogueAsset.Nodes.Count == 0)
        {
            Debug.LogError(
                "DialogueRunner：对话资产为空或没有节点。",
                this
            );
            return;
        }

        if (CurrentState != State.Idle || endingDialogue)
        {
            Debug.LogWarning(
                "DialogueRunner：对话进行中，忽略重复开始。",
                this
            );
            return;
        }

        asset = dialogueAsset;

        DialogueAsset.DialogueNode startNode =
            asset.GetNode(asset.StartNodeId);

        if (startNode == null)
        {
            // 起始节点 id 未配置或写错时回退到第一个节点。
            Debug.LogWarning(
                $"DialogueRunner：起始节点 '{asset.StartNodeId}' 不存在，从第一条开始。",
                this
            );

            startNode = asset.Nodes[0];
        }

        gameObject.SetActive(true);

        LockGameplay();

        onDialogueStart?.Invoke();
        DialogueStarted?.Invoke(asset);

        ShowNode(startNode);
    }

    private void Update()
    {
        // 选项状态下必须点按钮，全局点击不推进。
        if (CurrentState != State.Typing &&
            CurrentState != State.WaitingInput)
        {
            return;
        }

        bool clicked = Input.GetMouseButtonDown(0);
        bool pressed =
            continueKey != KeyCode.None &&
            Input.GetKeyDown(continueKey);

        if (clicked || pressed)
        {
            Advance();
        }
    }

    private void Advance()
    {
        if (CurrentState == State.Typing)
        {
            // 第一次点击：补完打字，状态由打字完成回调切换。
            ui.CompleteTypewriter();
            return;
        }

        if (CurrentState != State.WaitingInput)
        {
            return;
        }

        DialogueAsset.DialogueNode next =
            GetNextNode(currentNode, null);

        if (next == null)
        {
            EndDialogue();
        }
        else
        {
            ShowNode(next);
        }
    }

    private void SelectChoice(
        DialogueAsset.DialogueChoice choice
    )
    {
        if (CurrentState != State.WaitingChoice)
        {
            return;
        }

        // 先执行副作用（可能设置标记）再跳转，
        // 后续节点的条件判断能立刻读到新标记。
        choice.onSelected?.Invoke();

        ui.HideChoices();

        DialogueAsset.DialogueNode next =
            GetNextNode(currentNode, choice.nextNodeId);

        if (next == null)
        {
            EndDialogue();
        }
        else
        {
            ShowNode(next);
        }
    }

    private void ShowNode(
        DialogueAsset.DialogueNode node
    )
    {
        currentNode = node;
        CurrentState = State.Typing;
        ui.ShowNode(node);
    }

    private void OnTypewriterCompleted()
    {
        List<DialogueAsset.DialogueChoice> visibleChoices =
            CollectVisibleChoices(currentNode);

        if (visibleChoices.Count > 0)
        {
            CurrentState = State.WaitingChoice;
            ui.ShowChoices(visibleChoices, SelectChoice);
        }
        else
        {
            CurrentState = State.WaitingInput;
        }
    }

    // 筛选通过标记条件的选项。
    private List<DialogueAsset.DialogueChoice>
        CollectVisibleChoices(
            DialogueAsset.DialogueNode node
        )
    {
        var result = new List<DialogueAsset.DialogueChoice>();

        if (node == null || node.choices == null)
        {
            return result;
        }

        foreach (
            DialogueAsset.DialogueChoice choice in node.choices
        )
        {
            if (choice == null ||
                string.IsNullOrWhiteSpace(choice.buttonText))
            {
                continue;
            }

            if (PassesFlagConditions(choice))
            {
                result.Add(choice);
            }
        }

        return result;
    }

    private bool PassesFlagConditions(
        DialogueAsset.DialogueChoice choice
    )
    {
        if (choice.requiredFlags != null)
        {
            foreach (string flag in choice.requiredFlags)
            {
                if (!DialogueFlags.HasFlag(flag))
                {
                    return false;
                }
            }
        }

        if (choice.forbiddenFlags != null)
        {
            foreach (string flag in choice.forbiddenFlags)
            {
                if (DialogueFlags.HasFlag(flag))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // 解析下一节点：优先显式跳转 id，否则顺序推进；
    // 两者都无（或显式 id 写错）返回 null = 结束对话。
    private DialogueAsset.DialogueNode GetNextNode(
        DialogueAsset.DialogueNode current,
        string explicitNodeId
    )
    {
        if (!string.IsNullOrWhiteSpace(explicitNodeId))
        {
            DialogueAsset.DialogueNode next =
                asset.GetNode(explicitNodeId);

            if (next == null)
            {
                Debug.LogError(
                    $"DialogueRunner：跳转目标节点 '{explicitNodeId}' 不存在，对话结束。",
                    this
                );
            }

            return next;
        }

        int index = asset.GetIndex(current.nodeId);

        if (index < 0 ||
            index + 1 >= asset.Nodes.Count)
        {
            return null;
        }

        return asset.Nodes[index + 1];
    }

    private void EndDialogue()
    {
        CurrentState = State.Idle;
        asset = null;
        currentNode = null;
        endingDialogue = true;

        // 不能在本帧立即恢复玩家组件：结束对话的这次点击/按键
        // 对同帧所有组件都可见，WeaponEffects 会立刻开枪、
        // PlayerController 会起跳。延迟一帧恢复，
        // 那一帧玩家组件仍是禁用的，本次输入就被吞掉了。
        StartCoroutine(EndDialogueNextFrame());
    }

    private IEnumerator EndDialogueNextFrame()
    {
        // 等一帧，让触发结束的这次输入过期。
        yield return null;

        endingDialogue = false;

        RestoreGameplay();

        onDialogueEnd?.Invoke();
        DialogueEnded?.Invoke();

        gameObject.SetActive(false);
    }

    // 与 InventoryInput 相同的控制锁存模式，
    // 额外禁用拾取交互并隐藏 HUD。
    private void LockGameplay()
    {
        if (playerController == null)
        {
            playerController =
                FindObjectOfType<PlayerController>();
        }

        disabledBehaviours.Clear();

        AddControlIfEnabled(playerController);

        Transform controlRoot =
            playerController != null
                ? playerController.transform
                : null;

        if (controlRoot != null)
        {
            foreach (
                WeaponController controller in
                controlRoot.GetComponentsInChildren<
                    WeaponController
                >(true)
            )
            {
                AddControlIfEnabled(controller);
            }

            foreach (
                WeaponEffects effects in
                controlRoot.GetComponentsInChildren<
                    WeaponEffects
                >(true)
            )
            {
                AddControlIfEnabled(effects);
            }

            // 对话期间禁止拾取，避免和对话输入打架。
            foreach (
                PickupController pickup in
                controlRoot.GetComponentsInChildren<
                    PickupController
                >(true)
            )
            {
                AddControlIfEnabled(pickup);
            }

            CameraRecoil recoil =
                controlRoot.GetComponent<CameraRecoil>();

            AddControlIfEnabled(recoil);

            Transform weaponCameraTransform =
                controlRoot.Find("WeaponCamera");

            if (weaponCameraTransform != null)
            {
                AddControlIfEnabled(
                    weaponCameraTransform
                        .GetComponent<Camera>()
                );
            }
        }

        foreach (
            Behaviour behaviour in disabledBehaviours
        )
        {
            behaviour.enabled = false;
        }

        // 禁用脚本不会停止已经在播放的 Audio Source
        // （例如按住行走时开始对话，脚步声会一直响），
        // 把玩家身上正在播放的音源统一暂停，结束后恢复。
        if (controlRoot != null)
        {
            foreach (
                AudioSource source in
                controlRoot.GetComponentsInChildren<
                    AudioSource
                >(true)
            )
            {
                if (source != null && source.isPlaying)
                {
                    source.Pause();
                    pausedAudioSources.Add(source);
                }
            }
        }

        // 隐藏 HUD。
        if (hudCanvas != null)
        {
            hasPreviousHudState = true;
            previousHudActiveState = hudCanvas.activeSelf;
            hudCanvas.SetActive(false);
        }
        else
        {
            hasPreviousHudState = false;
        }

        // 解锁鼠标。
        hasPreviousCursorState = true;
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreGameplay()
    {
        foreach (
            Behaviour behaviour in disabledBehaviours
        )
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledBehaviours.Clear();

        foreach (
            AudioSource source in pausedAudioSources
        )
        {
            if (source != null)
            {
                source.UnPause();
            }
        }

        pausedAudioSources.Clear();

        if (hasPreviousHudState && hudCanvas != null)
        {
            hudCanvas.SetActive(previousHudActiveState);
            hasPreviousHudState = false;
        }

        if (hasPreviousCursorState)
        {
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
            hasPreviousCursorState = false;
        }
    }

    private void AddControlIfEnabled(
        Behaviour behaviour
    )
    {
        if (behaviour == null || !behaviour.enabled)
        {
            return;
        }

        if (!disabledBehaviours.Contains(behaviour))
        {
            disabledBehaviours.Add(behaviour);
        }
    }

    private void OnDisable()
    {
        // 画布被外部禁用（例如切场景）时兜底恢复控制，
        // 避免玩家操作和鼠标被永久锁住。
        if (CurrentState != State.Idle || endingDialogue)
        {
            CurrentState = State.Idle;
            endingDialogue = false;
            asset = null;
            currentNode = null;

            RestoreGameplay();
        }
    }
}
