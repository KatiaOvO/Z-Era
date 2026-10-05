using UnityEngine;

/// <summary>
/// 设置面板会话：挂在 SettingPanelRoot 上。
/// 面板被任意方式激活（主菜单按钮的 MainMenuPanelOpener、
/// Esc 键、代码）时 OnEnable 触发会话开始——锁定玩家控制、
/// 释放鼠标、暂停世界；被任意方式禁用或销毁（面板关闭按钮、
/// Esc 键、场景卸载）时 OnDisable 触发会话结束——全部还原。
/// 面板的显隐动画与流程完全交给现有的统一开/关组件
/// （MainMenuPanelOpener / MainMenuPanelCloser），
/// 会话状态只是自动跟随面板的激活状态。
/// 注意：把面板迁移到其他场景时，该物体必须以禁用状态开始，
/// 否则场景一加载就会锁定玩家并暂停世界。
/// </summary>
public class SettingsPanelSession : MonoBehaviour
{
    private void OnEnable()
    {
        SettingsUIRoot.Instance?.BeginSession(gameObject);
    }

    private void OnDisable()
    {
        // 场景卸载销毁面板时也会触发，保证暂停/锁定不残留
        SettingsUIRoot.Instance?.EndSession();
    }
}
