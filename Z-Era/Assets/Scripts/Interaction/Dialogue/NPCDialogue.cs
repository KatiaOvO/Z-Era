using TMPro;
using UnityEngine;

/// <summary>
/// NPC 对话触发器，挂在人物根物体上。
/// 准星指向本 NPC 且在交互距离内时显示提示，
/// 按下交互键开始该 NPC 绑定的对话资产。
/// </summary>
public class NPCDialogue : MonoBehaviour
{
    [Header("对话")]

    [Tooltip("本 NPC 的对话资产")]
    [SerializeField]
    private DialogueAsset dialogue;

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

    private Camera cachedCamera;

    private void Update()
    {
        if (dialogue == null)
        {
            return;
        }

        // 对话进行中或鼠标未锁定（背包打开等）时不检测。
        if ((DialogueRunner.Instance != null &&
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
            DialogueRunner.StartDialogue(dialogue);
            UpdatePrompt(false);
            return;
        }

        UpdatePrompt(CanInteract());
    }

    private bool CanInteract()
    {
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

        // 命中的碰撞体必须属于本 NPC（允许命中子物体的碰撞体）。
        NPCDialogue owner =
            hit.collider.GetComponentInParent<NPCDialogue>();

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
