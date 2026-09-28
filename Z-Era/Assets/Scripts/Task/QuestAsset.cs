using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 一个任务的数据资产，在 Project 窗口右键 Create > Task > Quest Asset 创建。
/// 任务由 QuestManager 驱动：接取时校验前置任务与标记，进行中累计目标进度，
/// 全部目标达标后自动完成，并按配置写入对话标记（DialogueFlags）实现剧情联动。
/// </summary>
[CreateAssetMenu(
    menuName = "Task/Quest Asset",
    fileName = "Quest_"
)]
public class QuestAsset : ScriptableObject
{
    [Serializable]
    public class QuestObjective
    {
        [Tooltip("目标唯一标识，其他系统上报进度时填写这里")]
        public string objectiveId;

        [TextArea(1, 3)]
        [Tooltip("目标指引文本，显示在任务 HUD 上，例如“前往教官提到的地点”")]
        public string description;

        [Tooltip("完成该目标需要的次数")]
        [Min(1)]
        public int requiredAmount = 1;
    }

    [Header("任务信息")]

    [Tooltip("任务唯一标识，整个项目内不能重复")]
    [SerializeField]
    private string questId;

    [Tooltip("任务标题，例如“靶场训练”")]
    [SerializeField]
    private string title;

    [TextArea(1, 3)]
    [Tooltip("任务描述，可作为任务日志或提示的备用文本")]
    [SerializeField]
    private string description;

    [Tooltip("目标列表：全部完成时任务自动完成")]
    [SerializeField]
    private List<QuestObjective> objectives =
        new List<QuestObjective>();

    [Header("接取条件（全部满足才可接取）")]

    [Tooltip("必须已完成的全部前置任务 id（任务链）")]
    [SerializeField]
    private string[] prerequisiteQuestIds;

    [Tooltip("必须已设置的全部对话标记（DialogueFlags）")]
    [SerializeField]
    private string[] requiredFlags;

    [Tooltip("只要设置了其中任一标记就不可接取")]
    [SerializeField]
    private string[] forbiddenFlags;

    [Header("Flag 桥接（与对话系统联动）")]

    [Tooltip("任务接取时自动设置的对话标记")]
    [SerializeField]
    private string[] flagsToSetOnAccept;

    [Tooltip("任务完成时自动设置的对话标记，供对话/NPC 条件判断使用")]
    [SerializeField]
    private string[] flagsToSetOnComplete;

    [Header("事件")]

    [Tooltip("任务接取时触发，例如播放音效、发送提示")]
    [SerializeField]
    private UnityEvent onAccept;

    [Tooltip("任务完成时触发，例如发放奖励")]
    [SerializeField]
    private UnityEvent onComplete;

    public string QuestId => questId;
    public string Title => title;
    public string Description => description;
    public IReadOnlyList<QuestObjective> Objectives => objectives;
    public string[] PrerequisiteQuestIds => prerequisiteQuestIds;
    public string[] RequiredFlags => requiredFlags;
    public string[] ForbiddenFlags => forbiddenFlags;
    public string[] FlagsToSetOnAccept => flagsToSetOnAccept;
    public string[] FlagsToSetOnComplete => flagsToSetOnComplete;
    public UnityEvent OnAcceptEvent => onAccept;
    public UnityEvent OnCompleteEvent => onComplete;

    // 按 id 查找目标，找不到返回 null。
    public QuestObjective GetObjective(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
        {
            return null;
        }

        return objectives.Find(
            objective => objective != null &&
                objective.objectiveId == objectiveId
        );
    }

    // ============ UnityEvent 接线入口 ============
    // UnityEvent 无法引用静态方法，因此把任务资产本身拖入
    // 检查器的目标槽（Object 槽），再选择下面的实例方法即可：
    // 对话选项的 onSelected、PickableItem 的 onPickedUp、
    // QuestTrigger 的 onFired 等都这样接。

    // 接取本任务。
    public void StartFromEvent()
    {
        QuestManager.StartQuest(this);
    }

    // 把指定目标直接置为完成。
    public void CompleteObjectiveFromEvent(string objectiveId)
    {
        QuestManager.SetObjectiveComplete(QuestId, objectiveId);
    }

    // 为指定目标累计进度。
    public void AddProgressFromEvent(string objectiveId, int amount)
    {
        QuestManager.AddProgress(QuestId, objectiveId, amount);
    }

    private void OnValidate()
    {
        // 提示重复 id：目标 id 重复会让进度无法对应到唯一目标。
        // 任务 id 的唯一性由 QuestManager 在运行时校验（发现冲突会告警）。
        if (string.IsNullOrWhiteSpace(questId))
        {
            Debug.LogWarning($"QuestAsset {name}：questId 为空。", this);
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjective objective = objectives[i];

            if (objective == null ||
                string.IsNullOrWhiteSpace(objective.objectiveId))
            {
                Debug.LogWarning(
                    $"QuestAsset {name}：第 {i + 1} 个目标的 objectiveId 为空。",
                    this
                );

                continue;
            }

            for (int j = i + 1; j < objectives.Count; j++)
            {
                if (objectives[j] != null &&
                    objectives[j].objectiveId ==
                        objective.objectiveId)
                {
                    Debug.LogWarning(
                        $"QuestAsset {name}：目标 id '{objective.objectiveId}' 重复。",
                        this
                    );
                }
            }
        }
    }
}
