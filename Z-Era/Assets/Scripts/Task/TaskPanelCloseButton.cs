using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务面板的关闭按钮：点击后调用 TaskPanelController.ClosePanel
/// 关闭 Task Canvas 并恢复暂停/玩家控制/鼠标状态。
/// 挂在任务面板右上角的关闭按钮上；按钮的 onClick 无需手动接线，
/// Awake 中会自动监听本物体的 Button。
/// </summary>
[RequireComponent(typeof(Button))]
public class TaskPanelCloseButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(ClosePanel);
    }

    private void ClosePanel()
    {
        TaskPanelController.Instance?.ClosePanel();
    }
}
