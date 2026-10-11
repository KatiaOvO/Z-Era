using UnityEngine;

/// <summary>
/// 自白触发器：每帧检查自白资产的对话标记条件
/// （requiredFlags 全部已设置且 forbiddenFlags 任一未设置），
/// 条件通过时交给 HUD 上的 MonologueUIController 显示。
/// 挂在场景中任意常驻物体上（如 HUD Canvas 下），把自白资产拖入即可。
/// </summary>
public class MonologueTrigger : MonoBehaviour
{
    [Tooltip("要显示的自白资产")]
    [SerializeField]
    private MonologueAsset monologue;

    [Tooltip("触发一次后是否不再生效")]
    [SerializeField]
    private bool triggerOnce = true;

    // 标记为已触发要放在条件通过之后：
    // 门槛不满足时保持可重试（与 QuestTrigger 一致）。
    private bool fired;

    private void Update()
    {
        if (monologue == null)
        {
            return;
        }

        if (triggerOnce && fired)
        {
            return;
        }

        if (!PassesFlagConditions(monologue))
        {
            return;
        }

        fired = true;

        MonologueUIController.Show(monologue);
    }

    private bool PassesFlagConditions(MonologueAsset asset)
    {
        if (asset.RequiredFlags != null)
        {
            foreach (string flag in asset.RequiredFlags)
            {
                if (!DialogueFlags.HasFlag(flag))
                {
                    return false;
                }
            }
        }

        if (asset.ForbiddenFlags != null)
        {
            foreach (string flag in asset.ForbiddenFlags)
            {
                if (DialogueFlags.HasFlag(flag))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
