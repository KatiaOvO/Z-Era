using System;
using System.Collections;
using System.Collections.Generic;
using Migration.UI;
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
        // 画布已启用，溶解入场动画播放中，不响应推进输入。
        Opening,
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

    [Header("溶解入场")]

    [Tooltip("对话背景的溶解组件；同一 Canvas 下的其他溶解元素会一起播放，留空则不做溶解")]
    [SerializeField]
    private UIDissolveImage backgroundDissolve;

    [Tooltip("背景溶解进行到该比例（0~1）时就显示说话人与正文，1 表示等完全显现")]
    [Range(0f, 1f)]
    [SerializeField]
    private float textRevealProgress = 0.5f;

    [Tooltip("背景溶解消失进行到该比例（0~1）时就清空说话人与正文，0 表示立即清空")]
    [Range(0f, 1f)]
    [SerializeField]
    private float textHideProgress = 0.5f;

    [Header("推进输入")]

    [Tooltip("除鼠标左键外，可用于推进对话的按键")]
    [SerializeField]
    private KeyCode continueKey = KeyCode.Space;

    [Header("对话音效")]

    [Tooltip("切换节点（含结束对话的那次点击）时播放的音效，留空则不播放")]
    [SerializeField]
    private AudioClip skipSound;

    [Tooltip("音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float skipSoundVolume = 1f;

    [Tooltip("鼠标悬停到选项按钮上时播放的音效，留空则不播放")]
    [SerializeField]
    private AudioClip choiceHoverSound;

    [Tooltip("悬停音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float choiceHoverSoundVolume = 1f;

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
    public event Action<DialogueAsset, DialogueAsset.DialogueNode>
        DialogueNodeShown;
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

    // 本次打开收集到的溶解元素，溶解完成后开始第一条对话。
    private readonly List<IUIDissolveEffect>
        dialogueDissolveElements = new List<IUIDissolveEffect>();

    // 跳过打字音效的专属音源，配置了音效时在 Awake 创建。
    private AudioSource skipAudioSource;

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

        if (skipSound != null)
        {
            // 专属音源，代码自动创建，无需在场景中手动添加。
            skipAudioSource = gameObject.AddComponent<AudioSource>();
            skipAudioSource.playOnAwake = false;
            skipAudioSource.spatialBlend = 0f;
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

        // 先清空预制体里的占位内容，溶解期间面板保持空白，
        // 溶解完成后 ShowNode 才填入真正的对话文本。
        ui.ResetDisplay();

        LockGameplay();

        onDialogueStart?.Invoke();
        DialogueStarted?.Invoke(asset);

        // 播放溶解入场；完成后才开始第一条对话，
        // 避免文字出现在还在溶解的背景上。
        PlayDissolveShow();

        if (dialogueDissolveElements.Count > 0)
        {
            CurrentState = State.Opening;
            StartCoroutine(
                StartFirstNodeAfterDissolve(startNode)
            );
        }
        else
        {
            ShowNode(startNode);
        }
    }

    // 与 InventoryInput.OpenInventory 相同的溶解驱动：
    // 先归位到完全消失再播放，同一 Canvas 下的
    // 其他溶解元素（头像框、选项框等）一起播放。
    private void PlayDissolveShow()
    {
        dialogueDissolveElements.Clear();

        if (backgroundDissolve == null)
        {
            return;
        }

        dialogueDissolveElements.Add(backgroundDissolve);

        backgroundDissolve.SetLocation(1f);
        backgroundDissolve.Show();

        Canvas dissolveCanvas =
            backgroundDissolve.graphic != null
                ? backgroundDissolve.graphic.canvas
                : null;

        if (dissolveCanvas == null)
        {
            return;
        }

        foreach (
            IUIDissolveEffect dissolve in
            dissolveCanvas.GetComponentsInChildren<
                IUIDissolveEffect
            >(true)
        )
        {
            // 背景已在上面单独驱动。
            if (dissolve is UIDissolveImage image &&
                image == backgroundDissolve)
            {
                continue;
            }

            dissolve.SetLocation(1f);
            dissolve.Show();

            dialogueDissolveElements.Add(dissolve);
        }
    }

    private IEnumerator StartFirstNodeAfterDissolve(
        DialogueAsset.DialogueNode startNode
    )
    {
        // 背景溶解进行到设定比例时就把文本显示出来，
        // 不等背景完全显现；之后继续播完剩余溶解。
        while (backgroundDissolve != null &&
               1f - backgroundDissolve.location <
                   textRevealProgress)
        {
            yield return null;
        }

        // 等待期间对话被结束或画布被禁用则放弃。
        if (CurrentState != State.Opening)
        {
            yield break;
        }

        ShowNode(startNode);
    }

    private bool AllDissolvesComplete()
    {
        foreach (
            IUIDissolveEffect dissolve in
                dialogueDissolveElements
        )
        {
            Component component = dissolve as Component;

            if (dissolve == null ||
                component == null ||
                !component.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!dissolve.isShowComplete)
            {
                return false;
            }
        }

        return true;
    }

    private void Update()
    {
        // 打字期间不响应任何推进输入，必须等文本打印完；
        // 选项状态下必须点按钮，全局点击不推进。
        if (CurrentState != State.WaitingInput)
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
        if (CurrentState != State.WaitingInput)
        {
            return;
        }

        DialogueAsset.DialogueNode next =
            GetNextNode(currentNode, null);

        // 无论切换到下一节点还是结束对话，都播一次音效；
        // 结束路径的音效由结束序列等待播完后再关画布。
        PlaySkipSound();

        if (next == null)
        {
            EndDialogue();
        }
        else
        {
            ShowNode(next);
        }
    }

    private void PlaySkipSound()
    {
        if (skipSound == null || skipAudioSource == null)
        {
            return;
        }

        skipAudioSource.PlayOneShot(
            skipSound,
            skipSoundVolume
        );
    }

    // 由 DialogueChoiceButton 在鼠标悬停进入选项时调用。
    public void PlayChoiceHoverSound()
    {
        if (choiceHoverSound == null || skipAudioSource == null)
        {
            return;
        }

        skipAudioSource.PlayOneShot(
            choiceHoverSound,
            choiceHoverSoundVolume
        );
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

        // 无论跳转到下一节点还是结束对话，都播一次音效。
        PlaySkipSound();

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
        DialogueNodeShown?.Invoke(asset, node);
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

        // 背景溶解消失同时开始（与输入无关）；
        // 文本在溶解消失过半时由结束协程清空，前半程保留。

        foreach (
            IUIDissolveEffect dissolve in
                dialogueDissolveElements
        )
        {
            dissolve?.Hide();
        }

        StartCoroutine(EndDialogueSequence());
    }

    private IEnumerator EndDialogueSequence()
    {
        // 背景溶解消失进行到设定比例时才清空说话人与正文，
        // 前半程面板带着文字一起退场。
        while (backgroundDissolve != null &&
               backgroundDissolve.location < textHideProgress)
        {
            yield return null;
        }

        ui.ResetDisplay();

        // 结束对话的点击通常按住多帧，而武器的持续开火判定是
        // Input.GetKey(Mouse0)——恢复时若按住未松开，
        // 全自动武器会立刻开火。等这次输入完全释放后再恢复。
        while (Input.GetMouseButton(0) ||
               (continueKey != KeyCode.None &&
                   Input.GetKey(continueKey)))
        {
            yield return null;
        }

        // 等背景完全溶解消失后，才恢复 HUD 与玩家控制。
        while (!AllDissolvesHidden())
        {
            yield return null;
        }

        endingDialogue = false;

        RestoreGameplay();

        onDialogueEnd?.Invoke();
        DialogueEnded?.Invoke();

        // 跳过音效若还在播，等它播完再隐藏画布，避免被掐断。
        while (skipAudioSource != null && skipAudioSource.isPlaying)
        {
            yield return null;
        }

        // 选项按钮的溶解消失动画也要播完再关画布。
        while (ui != null && ui.IsChoicesHiding)
        {
            yield return null;
        }

        gameObject.SetActive(false);
    }

    // 是否所有溶解元素都完成了"溶解消失"。
    private bool AllDissolvesHidden()
    {
        foreach (
            IUIDissolveEffect dissolve in
                dialogueDissolveElements
        )
        {
            Component component = dissolve as Component;

            if (dissolve == null ||
                component == null ||
                !component.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!dissolve.isHideComplete)
            {
                return false;
            }
        }

        return true;
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

            // 注意：武器相机（WeaponCamera）不禁用，
            // 对话期间武器保持可见，只锁操作。
        }

        foreach (
            Behaviour behaviour in disabledBehaviours
        )
        {
            behaviour.enabled = false;
        }

        // 行走中开启对话时，玩家动画控制器（由 WeaponController
        // 每帧驱动）已被禁用，walk 参数会停留在 true，
        // 人物会一直保持 Walk 状态；这里主动切回 Idle。
        SetPlayerWalkState(false);

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

    // 把玩家动画的 walk 参数复位（对话结束后由
    // WeaponController 恢复每帧驱动）。
    private void SetPlayerWalkState(bool walking)
    {
        if (playerController == null)
        {
            return;
        }

        Transform controlRoot = playerController.transform;

        foreach (
            Animator animator in
            controlRoot.GetComponentsInChildren<
                Animator
            >(true)
        )
        {
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                !animator.isActiveAndEnabled)
            {
                continue;
            }

            foreach (
                AnimatorControllerParameter parameter in
                    animator.parameters
            )
            {
                if (parameter.name == "walk" &&
                    parameter.type ==
                        AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool("walk", walking);
                    break;
                }
            }
        }
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
