using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 设置面板页签按钮：挂在 SettingButtonContainer 下的每个按钮上，
/// 五个按钮的同级顺序需与 SettingPanelContainer 下的面板容器
/// 一一对应。
/// 点击播放点击音效并通知 SettingTabGroup 切换页签
/// （下划线填充动画与面板开关由组统一驱动，按钮自身不做动画）；
/// 悬停不播放音效。
/// 下划线 Image（Type = Filled，Fill Method = Horizontal，
/// Origin = Left）留空时自动取子物体上的第一个 Image。
/// </summary>
public class SettingTabButton : MonoBehaviour, IPointerClickHandler
{
    [Header("引用")]

    [Tooltip("下划线 Image（Type = Filled，Fill Method = Horizontal，" +
        "Origin = Left），留空则自动在子物体中查找")]
    [SerializeField]
    private Image underline;

    [Header("点击音效")]

    [Tooltip("点击按钮时播放一次的音效，留空则不播放")]
    [SerializeField]
    private AudioClip clickSound;

    [Tooltip("点击音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float clickSoundVolume = 1f;

    private void Awake()
    {
        if (underline == null)
        {
            underline = GetComponentInChildren<Image>(true);
        }

        // 子图形（下划线、文字等）一律退出射线检测：
        // 它们开着 Raycast Target 时，指针在按钮根与子图形之间
        // 移动会让悬停链断开重连，导致指针事件重复触发；
        // 按钮根物体自身的 Image 保持接收点击
        foreach (Graphic graphic in
            GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject != gameObject)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        SettingTabGroup group =
            GetComponentInParent<SettingTabGroup>();

        // 已选中的页签（下划线填充中或已填满）重复点击：
        // 不播放音效、不触发下划线重新填充
        if (group != null && group.IsSelected(this))
        {
            return;
        }

        PlayClickSound();

        // 组在 SettingPanelRoot 上（按钮的祖先），每次点击时查找即可，
        // 无需关心两者 Awake 的执行顺序
        group?.Select(this);
    }

    // 与 MainMenuButtonHoverFill 相同：用独立的临时物体播放，
    // 音效独立存活到播完，不会被任何面板开关截断
    private void PlayClickSound()
    {
        if (clickSound == null)
        {
            return;
        }

        GameObject soundHost = new GameObject("ClickSound");
        AudioSource source = soundHost.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = clickSoundVolume;
        source.PlayOneShot(clickSound);
        Destroy(soundHost, clickSound.length);
    }

    // 下划线填充由 SettingTabGroup 统一驱动
    public void SetUnderlineFill(float amount)
    {
        if (underline != null)
        {
            underline.fillAmount = amount;
        }
    }
}
