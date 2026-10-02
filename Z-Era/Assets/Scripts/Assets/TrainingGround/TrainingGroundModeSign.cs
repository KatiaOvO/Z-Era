using TMPro;
using UnityEngine;

/// <summary>
/// 训练场模式选项牌：挂在"自由训练"等 3D TextMeshPro 文字物体上，
/// 物体上需带触发器碰撞体。子弹命中后由
/// TrainingGroundTargetMode 选中该模式并应用对应设置。
/// "模式"标题文字不挂本组件、不加触发器，子弹打上去没有反应。
/// 选中状态通过改变文字颜色反馈。
/// </summary>
public class TrainingGroundModeSign : MonoBehaviour
{
    [Header("选项设置")]

    [Tooltip("命中本选项后进入的训练模式")]
    [SerializeField]
    private TrainingGroundMode mode;

    [Header("选中表现")]

    [Tooltip("选中时的文字颜色")]
    [SerializeField]
    private Color selectedColor = new Color(0.25f, 1f, 0.35f);

    [Tooltip("未选中时的文字颜色")]
    [SerializeField]
    private Color normalColor = Color.white;

    public TrainingGroundMode Mode => mode;

    private TMP_Text text;
    private TrainingGroundTargetMode setting;

    // 子弹图层号只查询一次；项目里没有 Bullet 层时为 -1，
    // 触发器兜底检测自动失效，不影响主检测路径
    private int bulletLayer = int.MinValue;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        bulletLayer = LayerMask.NameToLayer("Bullet");

        SetSelected(false);
    }

    // 高速子弹的命中由 TrainingGroundTargetMode 通过 BulletSettled
    // 事件反查落点识别（子弹命中当帧即被回收，物理触发器事件不一定
    // 触发）；这里只兜底处理仍能触发物理事件的情况，两条路径都指向
    // 同一个 SelectSign，重复调用是幂等的。
    private void OnTriggerEnter(Collider other)
    {
        if (bulletLayer < 0 ||
            other.gameObject.layer != bulletLayer)
        {
            return;
        }

        if (setting == null)
        {
            setting = GetComponentInParent<TrainingGroundTargetMode>();
        }

        if (setting != null)
        {
            setting.SelectSign(this);
        }
    }

    public void SetSelected(bool selected)
    {
        if (text != null)
        {
            text.color = selected ? selectedColor : normalColor;
        }
    }
}
