using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 通用任务触发器：无需写代码即可接取任务或上报目标进度。
/// 两种激活方式：
/// - OnTriggerEnter：玩家进入本物体的触发器碰撞体（需要 is Trigger）；
/// - OnInteract：准星指向本物体碰撞体且在交互距离内，按下交互键。
/// 两种模式：
/// - StartQuest：接取配置的任务资产；
/// - AddProgress：为指定任务目标累计进度。
/// 可用对话标记做接取门槛（requiredFlags 全部已设置才生效）。
/// </summary>
public class QuestTrigger : MonoBehaviour
{
    public enum Mode
    {
        StartQuest,
        AddProgress
    }

    public enum Activation
    {
        OnTriggerEnter,
        OnInteract
    }

    [Header("触发模式")]

    [Tooltip("本触发器做的事情：接取任务 或 上报目标进度")]
    [SerializeField]
    private Mode mode = Mode.AddProgress;

    [Tooltip("激活方式：进入触发器 或 准星交互")]
    [SerializeField]
    private Activation activation = Activation.OnTriggerEnter;

    [Header("目标")]

    [Tooltip("StartQuest 模式：要接取的任务资产")]
    [SerializeField]
    private QuestAsset quest;

    [Tooltip("AddProgress 模式：要上报进度的任务 id")]
    [SerializeField]
    private string questId;

    [Tooltip("AddProgress 模式：要上报进度的目标 id")]
    [SerializeField]
    private string objectiveId;

    [Tooltip("AddProgress 模式：每次触发的进度增量")]
    [Min(1)]
    [SerializeField]
    private int amount = 1;

    [Header("条件与次数")]

    [Tooltip("全部已设置时才触发（对话标记门槛），留空则不限制")]
    [SerializeField]
    private string[] requiredFlags;

    [Tooltip("触发一次后是否不再生效")]
    [SerializeField]
    private bool triggerOnce = true;

    [Header("触发器模式（OnTriggerEnter）")]

    [Tooltip("进入触发器的物体必须具有该 Tag，留空则不限")]
    [SerializeField]
    private string triggerTag = "Player";

    [Header("交互模式（OnInteract）")]

    [Tooltip("开始交互的按键")]
    [SerializeField]
    private KeyCode interactKey = KeyCode.E;

    [Tooltip("准星指向本物体且距离小于该值时才能交互（米）")]
    [Min(0.1f)]
    [SerializeField]
    private float interactDistance = 2.5f;

    [Tooltip("交互提示文本，留空则不显示提示")]
    [SerializeField]
    private TMP_Text promptText;

    [Tooltip("提示文本格式，{0} 会被替换为按键名")]
    [SerializeField]
    private string promptFormat = "按 {0} 交互";

    [Header("事件")]

    [Tooltip("触发成功时调用")]
    [SerializeField]
    private UnityEvent onFired;

    private bool fired;
    private Camera cachedCamera;

    private void OnTriggerEnter(Collider other)
    {
        if (activation != Activation.OnTriggerEnter)
        {
            return;
        }

        if (!string.IsNullOrEmpty(triggerTag) &&
            !other.CompareTag(triggerTag))
        {
            return;
        }

        Fire();
    }

    private void Update()
    {
        if (activation != Activation.OnInteract)
        {
            return;
        }

        // 鼠标未锁定（背包/对话打开等）时不检测交互。
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            UpdatePrompt(false);
            return;
        }

        bool canInteract = CanInteract();

        if (canInteract && Input.GetKeyDown(interactKey))
        {
            Fire();
            UpdatePrompt(false);
            return;
        }

        UpdatePrompt(canInteract);
    }

    private void Fire()
    {
        if (triggerOnce && fired)
        {
            return;
        }

        if (requiredFlags != null)
        {
            foreach (string flag in requiredFlags)
            {
                if (!DialogueFlags.HasFlag(flag))
                {
                    return;
                }
            }
        }

        // 标记为已触发要放在条件通过之后：
        // 门槛不满足时保持可重试。
        fired = true;

        if (mode == Mode.StartQuest)
        {
            QuestManager.StartQuest(quest);
        }
        else
        {
            QuestManager.AddProgress(questId, objectiveId, amount);
        }

        onFired?.Invoke();

        // 只触发一次的交互点触发后不再显示提示。
        if (triggerOnce)
        {
            UpdatePrompt(false);
        }
    }

    private bool CanInteract()
    {
        if (triggerOnce && fired)
        {
            return false;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;

            if (cachedCamera == null)
            {
                return false;
            }
        }

        Ray ray = cachedCamera.ScreenPointToRay(
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

        // 命中的碰撞体必须属于本物体（允许命中子物体的碰撞体）。
        QuestTrigger owner =
            hit.collider.GetComponentInParent<QuestTrigger>();

        return owner == this;
    }

    private void UpdatePrompt(bool canInteract)
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

        promptText.text = string.Format(
            promptFormat,
            interactKey
        );
    }
}
