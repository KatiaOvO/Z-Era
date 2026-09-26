using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话标记存储：记录剧情进度，驱动选项与节点的条件显隐。
/// 静态方法供代码调用（HasFlag / SetFlag）；
/// 实例方法供选项的 UnityEvent 在检查器里接线（SetFlagTrue 等）。
/// 首次访问时自动创建常驻物体，无需手动摆进场景。
/// </summary>
public class DialogueFlags : MonoBehaviour
{
    private static DialogueFlags instance;

    private readonly HashSet<string> flags = new HashSet<string>();

    public static DialogueFlags Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject("DialogueFlags")
                    .AddComponent<DialogueFlags>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // 标记是否已设置。
    public static bool HasFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return false;
        }

        return Instance.flags.Contains(flagName);
    }

    // 设置或清除标记。
    public static void SetFlag(string flagName, bool value)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return;
        }

        if (value)
        {
            Instance.flags.Add(flagName);
        }
        else
        {
            Instance.flags.Remove(flagName);
        }
    }

    // 清空全部标记，例如游戏重新开局时调用。
    public static void ClearAll()
    {
        Instance.flags.Clear();
    }

    // 以下实例方法供 UnityEvent 在检查器中动态接线。

    public void SetFlagTrue(string flagName)
    {
        SetFlag(flagName, true);
    }

    public void SetFlagFalse(string flagName)
    {
        SetFlag(flagName, false);
    }

    public void ClearAllFlags()
    {
        ClearAll();
    }
}
