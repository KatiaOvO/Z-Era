using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂载在退出按钮上：点击后关闭游戏程序。
/// 与 MainMenuButtonHoverFill 一样走 IPointerClickHandler，
/// 无需在 Button 的 onClick 中手动接线，两个组件可挂同一按钮。
/// </summary>
public class QuitGame : MonoBehaviour, IPointerClickHandler
{
    // Application.Quit() 只在打包后的程序里生效；编辑器中
    // 会自动转成退出运行模式，方便在 Editor 里测试按钮。
    public void OnPointerClick(PointerEventData eventData)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
