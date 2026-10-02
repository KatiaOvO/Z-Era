using TMPro;
using UnityEngine;

/// <summary>
/// 训练场 HUD 选项牌：挂在"是"/"否"这类 3D TextMeshPro 文字物体上，
/// 物体上需带触发器碰撞体。子弹命中后由
/// TrainingGroundHUDSetting 选中本选项并应用对应设置。
/// 选中状态通过改变文字颜色反馈。
/// </summary>
public class HUDSettingSign : MonoBehaviour
{
    [Header("选项设置")]

    [Tooltip("命中本选项后是否隐藏 HUD（“是”勾选，“否”不勾）")]
    [SerializeField]
    private bool hideHud;

    [Header("选中表现")]

    [Tooltip("选中时的文字颜色")]
    [SerializeField]
    private Color selectedColor = new Color(0.25f, 1f, 0.35f);

    [Tooltip("未选中时的文字颜色")]
    [SerializeField]
    private Color normalColor = Color.white;

    public bool HideHud => hideHud;

    private TMP_Text text;
    private TrainingGroundHUDSetting setting;

    // 子弹图层号只查询一次；项目里没有 Bullet 层时为 -1，
    // 触发器兜底检测自动失效，不影响主检测路径
    private int bulletLayer = int.MinValue;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        bulletLayer = LayerMask.NameToLayer("Bullet");

        SetSelected(false);
    }

    // 高速子弹的命中由 TrainingGroundHUDSetting 通过 BulletSettled
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
            setting = GetComponentInParent<TrainingGroundHUDSetting>();
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
