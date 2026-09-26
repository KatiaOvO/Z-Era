using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 一段完整对话的数据资产，在 Project 窗口右键创建。
/// 节点按列表顺序排列；没有选项的节点自动推进到下一条，
/// 跳转目标用 nodeId 字符串表示，避免插入删除节点后下标错位。
/// </summary>
[CreateAssetMenu(
    menuName = "Dialogue/Dialogue Asset",
    fileName = "Dialogue_"
)]
public class DialogueAsset : ScriptableObject
{
    [Serializable]
    public class DialogueChoice
    {
        [Tooltip("选项按钮上显示的文字")]
        public string buttonText;

        [Tooltip("必须已设置的全部标记，缺一则不显示该选项")]
        public string[] requiredFlags;

        [Tooltip("只要设置了其中任一标记就不显示该选项")]
        public string[] forbiddenFlags;

        [Tooltip("选择后跳转到的节点 id，留空则顺序推进到下一条")]
        public string nextNodeId;

        [Tooltip("选中该选项时触发的副作用，例如设置标记、发放物品")]
        public UnityEvent onSelected;
    }

    [Serializable]
    public class DialogueNode
    {
        [Tooltip("节点唯一标识，其他节点的跳转目标填写这里")]
        public string nodeId;

        [Tooltip("说话人名称，留空则不显示名称行")]
        public string speakerName;

        [Tooltip("说话人名称的颜色")]
        public Color speakerColor = Color.white;

        [Tooltip("说话人头像，留空则不显示头像")]
        public Sprite portrait;

        [TextArea(3, 10)]
        [Tooltip("正文，支持 TMP 富文本")]
        public string text;

        [Tooltip("选项列表，留空则点击继续下一条")]
        public List<DialogueChoice> choices = new List<DialogueChoice>();
    }

    [Tooltip("对话开始时进入的节点 id，留空则从第一条开始")]
    [SerializeField]
    private string startNodeId;

    [SerializeField]
    private List<DialogueNode> nodes = new List<DialogueNode>();

    public IReadOnlyList<DialogueNode> Nodes => nodes;

    public string StartNodeId => startNodeId;

    // 按 id 查找节点，找不到返回 null。
    public DialogueNode GetNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        return nodes.Find(
            node => node != null && node.nodeId == nodeId
        );
    }

    // 返回节点下标，找不到返回 -1。
    public int GetIndex(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return -1;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].nodeId == nodeId)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnValidate()
    {
        // 提示重复的节点 id：重复 id 会让跳转目标指向错误节点。
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null ||
                string.IsNullOrWhiteSpace(nodes[i].nodeId))
            {
                continue;
            }

            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (nodes[j] != null &&
                    nodes[j].nodeId == nodes[i].nodeId)
                {
                    Debug.LogWarning(
                        $"DialogueAsset {name}：节点 id '{nodes[i].nodeId}' 重复。",
                        this
                    );
                }
            }
        }
    }
}
