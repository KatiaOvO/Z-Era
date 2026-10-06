using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 任务面板列表：把当前场景的进行中/已完成任务填充到面板上。
/// 挂在 Task Canvas 下（画布或 TaskInfoRoot 均可），模板与容器
/// 引用由检查器拖入。
/// 数据过滤：QuestAsset 的 SceneName 留空表示全局任务（所有场景
/// 可见），否则只在所属场景的任务面板中显示。
///
/// 区块结构（UI 由 TaskInfoRoot 承载）：
/// - 已完成区：FinishedScrollView 的 Content 下，每个已完成任务
///   克隆一个 FinishedEntryTemplate（√图 + 标题）；
/// - 进行中区：InProgressScrollView 的 Content 下，每个任务克隆一个
///   QuestBlockTemplate（标题 + 描述 + 目标行容器），再按目标数量
///   克隆 ObjectiveLineTemplate（圆图 + 目标文本）进容器；
///   已完成的目标用 TMP 富文本 &lt;s&gt; 标签画删除线（程序绘制，
///   不依赖字体字形），圆图保持原样。
///
/// 模板放在 Templates 节点下并保持禁用；运行时发现模板处于启用
/// 状态会自动禁用。条目克隆后激活，布局交给各容器上的
/// Vertical Layout Group（滚动区 Content 另有 Content Size Fitter
/// 自动撑开高度）。
///
/// 由 TaskPanelController 在面板打开/关闭时调用 OnPanelOpened/
/// OnPanelClosed；打开期间订阅 QuestManager 事件实时刷新，
/// 本组件销毁时自动退订，切场景不会残留订阅。
/// </summary>
public class TaskPanelQuestList : MonoBehaviour
{
    [Header("已完成区")]

    [Tooltip("已完成条目模板（√图 + 标题文本，保持禁用）")]
    [SerializeField]
    private GameObject finishedEntryTemplate;

    [Tooltip("已完成模板中的标题文本")]
    [SerializeField]
    private TMP_Text finishedTitleText;

    [Tooltip("已完成区的滚动列表 Content（挂 Vertical Layout Group " +
        "+ Content Size Fitter）")]
    [SerializeField]
    private Transform finishedContent;

    [Header("进行中区")]

    [Tooltip("任务块模板（标题 + 描述 + 目标行容器，保持禁用）")]
    [SerializeField]
    private GameObject questBlockTemplate;

    [Tooltip("任务块模板中的标题文本")]
    [SerializeField]
    private TMP_Text blockTitleText;

    [Tooltip("任务块模板中的描述文本（任务描述为空时该文本自动隐藏）")]
    [SerializeField]
    private TMP_Text blockDescriptionText;

    [Tooltip("任务块模板中的目标行容器（挂 Vertical Layout Group）")]
    [SerializeField]
    private Transform blockObjectivesContainer;

    [Tooltip("进行中区滚动列表的 Content（挂 Vertical Layout Group " +
        "+ Content Size Fitter）")]
    [SerializeField]
    private Transform inProgressContent;

    [Header("目标行")]

    [Tooltip("目标行模板（圆图 + 目标文本，保持禁用）")]
    [SerializeField]
    private GameObject objectiveLineTemplate;

    [Tooltip("目标行模板中的文本（需勾选 Rich Text，删除线用 <s> 标签实现）")]
    [SerializeField]
    private TMP_Text objectiveText;

    [Header("文本格式")]

    [Tooltip("目标行格式：{0}=目标描述，{1}=当前进度，{2}=需求次数；需求次数为 1 时不显示进度")]
    [SerializeField]
    private string objectiveFormat = "{0}（{1}/{2}）";

    // 订阅事件用的管理器引用（事件是实例成员，挂在单例对象上）。
    // 缓存引用也保证销毁路径的退订不会触发管理器的自动创建
    private QuestManager manager;

    private void OnDestroy()
    {
        // 面板随场景销毁时收不到 OnPanelClosed，这里兜底退订；
        // 未订阅时退订是无害的空操作
        UnsubscribeEvents();
    }

    // ===== 由 TaskPanelController 调用 =====

    public void OnPanelOpened()
    {
        // 访问 Instance 会在首次访问时自动创建常驻管理器
        // （同 QuestHUDController 的取用方式）
        manager = QuestManager.Instance;

        manager.QuestAccepted += OnQuestAccepted;
        manager.ObjectiveUpdated += OnObjectiveUpdated;
        manager.QuestCompleted += OnQuestCompleted;

        WarnIfMisconfigured();
        Rebuild();
    }

    public void OnPanelClosed()
    {
        UnsubscribeEvents();
    }

    private void UnsubscribeEvents()
    {
        // 管理器未取用过或已销毁时无事可退订；
        // 管理器销毁时其事件随之失效，也不需要退订
        if (manager == null)
        {
            return;
        }

        manager.QuestAccepted -= OnQuestAccepted;
        manager.ObjectiveUpdated -= OnObjectiveUpdated;
        manager.QuestCompleted -= OnQuestCompleted;
    }

    private void OnQuestAccepted(QuestAsset quest)
    {
        Rebuild();
    }

    private void OnObjectiveUpdated(
        QuestAsset quest,
        QuestAsset.QuestObjective objective,
        int current,
        int required
    )
    {
        Rebuild();
    }

    private void OnQuestCompleted(QuestAsset quest)
    {
        Rebuild();
    }

    // 引用缺失或模板状态不对时在 Console 提示；模板忘了禁用这种
    // 情况直接运行时自动禁用——启用的模板会把占位内容画在面板上，
    // 盖住或混淆真实条目
    private void WarnIfMisconfigured()
    {
        if (finishedEntryTemplate == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：已完成条目模板未拖引用，" +
                    "已完成任务不会显示。",
                this
            );
        }
        else if (finishedEntryTemplate.activeSelf)
        {
            finishedEntryTemplate.SetActive(false);

            Debug.Log(
                "TaskPanelQuestList：已完成条目模板处于启用状态，" +
                    "已自动禁用（建议在编辑器中直接禁用并保存场景）。",
                this
            );
        }

        if (questBlockTemplate == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：任务块模板未拖引用，" +
                    "进行中任务不会显示。",
                this
            );
        }
        else if (questBlockTemplate.activeSelf)
        {
            questBlockTemplate.SetActive(false);

            Debug.Log(
                "TaskPanelQuestList：任务块模板处于启用状态，" +
                    "已自动禁用（建议在编辑器中直接禁用并保存场景）。",
                this
            );
        }

        if (objectiveLineTemplate == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：目标行模板未拖引用，" +
                    "进行中任务不会显示目标列表。",
                this
            );
        }
        else if (objectiveLineTemplate.activeSelf)
        {
            objectiveLineTemplate.SetActive(false);

            Debug.Log(
                "TaskPanelQuestList：目标行模板处于启用状态，" +
                    "已自动禁用（建议在编辑器中直接禁用并保存场景）。",
                this
            );
        }

        if (finishedContent == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：已完成区 Content 未拖引用。",
                this
            );
        }

        if (inProgressContent == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：进行中区 Content 未拖引用。",
                this
            );
        }

        if (finishedEntryTemplate != null &&
            finishedTitleText == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：已完成模板的标题文本未拖引用，" +
                    "条目将没有任何文字（看起来像没有生成）。",
                this
            );
        }

        if (questBlockTemplate != null && blockTitleText == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：任务块模板的标题文本未拖引用，" +
                    "块内将没有标题。",
                this
            );
        }

        if (questBlockTemplate != null &&
            blockObjectivesContainer == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：任务块模板的目标行容器未拖引用，" +
                    "块内不会显示目标列表。",
                this
            );
        }

        if (objectiveLineTemplate != null && objectiveText == null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：目标行模板的文本未拖引用，" +
                    "目标行将没有任何文字（看起来像没有生成）。",
                this
            );
        }
    }

    // ===== 列表构建 =====

    // 清空两个 Content 后按当前场景重建全部条目。
    // 模板未配置时静默跳过对应区块（引用缺失另有告警）。
    private void Rebuild()
    {
        ClearContainer(finishedContent);
        ClearContainer(inProgressContent);

        string sceneName = SceneManager.GetActiveScene().name;

        int finishedCount = 0;

        if (finishedEntryTemplate != null)
        {
            foreach (QuestAsset quest in QuestManager.CompletedQuests)
            {
                if (!BelongsToCurrentScene(quest, sceneName))
                {
                    continue;
                }

                SpawnFinishedEntry(quest);
                finishedCount++;
            }
        }

        int inProgressCount = 0;

        if (questBlockTemplate != null)
        {
            foreach (QuestAsset quest in QuestManager.ActiveQuests)
            {
                if (!BelongsToCurrentScene(quest, sceneName))
                {
                    continue;
                }

                SpawnQuestBlock(quest);
                inProgressCount++;
            }
        }

        // 重建诊断日志：面板打开期间世界已暂停，进度事件几乎
        // 不会触发，实际输出频率基本就是每次打开面板一条
        Debug.Log(
            $"TaskPanelQuestList：重建任务列表（场景 '{sceneName}'）——" +
                $"进行中 {inProgressCount} 块，已完成 {finishedCount} 条。",
            this
        );
    }

    // SceneName 留空表示全局任务，所有场景都显示
    private static bool BelongsToCurrentScene(
        QuestAsset quest,
        string sceneName
    )
    {
        return string.IsNullOrEmpty(quest.SceneName) ||
            quest.SceneName == sceneName;
    }

    private void SpawnFinishedEntry(QuestAsset quest)
    {
        GameObject clone =
            Instantiate(finishedEntryTemplate, finishedContent);

        clone.SetActive(true);

        TMP_Text title = FindInClone(finishedTitleText, clone);

        if (title != null)
        {
            title.text = GetQuestDisplayName(quest);
        }
        else if (finishedTitleText != null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：克隆的已完成条目里找不到" +
                    $"名为 '{finishedTitleText.name}' 的文本组件，" +
                    "标题未写入（检查引用拖的是否为模板内的文本）。",
                this
            );
        }
    }

    private void SpawnQuestBlock(QuestAsset quest)
    {
        GameObject clone =
            Instantiate(questBlockTemplate, inProgressContent);

        clone.SetActive(true);

        TMP_Text title = FindInClone(blockTitleText, clone);

        if (title != null)
        {
            title.text = GetQuestDisplayName(quest);
        }
        else if (blockTitleText != null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：克隆的任务块里找不到" +
                    $"名为 '{blockTitleText.name}' 的文本组件，" +
                    "标题未写入（检查引用拖的是否为模板内的文本）。",
                this
            );
        }

        TMP_Text description =
            FindInClone(blockDescriptionText, clone);

        if (description != null)
        {
            bool hasDescription =
                !string.IsNullOrWhiteSpace(quest.Description);

            description.gameObject.SetActive(hasDescription);

            if (hasDescription)
            {
                description.text = quest.Description;
            }
        }

        Transform objectivesContainer =
            FindInClone(blockObjectivesContainer, clone);

        if (objectivesContainer == null)
        {
            if (blockObjectivesContainer != null)
            {
                Debug.LogWarning(
                    "TaskPanelQuestList：克隆的任务块里找不到" +
                        $"名为 '{blockObjectivesContainer.name}' 的容器，" +
                        "目标列表未生成（检查引用拖的是否为模板内的容器）。",
                    this
                );
            }

            return;
        }

        foreach (
            QuestAsset.QuestObjective objective in quest.Objectives
        )
        {
            if (objective == null)
            {
                continue;
            }

            SpawnObjectiveLine(quest, objective, objectivesContainer);
        }
    }

    private void SpawnObjectiveLine(
        QuestAsset quest,
        QuestAsset.QuestObjective objective,
        Transform container
    )
    {
        GameObject line =
            Instantiate(objectiveLineTemplate, container);

        line.SetActive(true);

        TMP_Text text = FindInClone(objectiveText, line);

        if (text != null)
        {
            text.text = BuildObjectiveLine(quest, objective);
        }
        else if (objectiveText != null)
        {
            Debug.LogWarning(
                "TaskPanelQuestList：克隆的目标行里找不到" +
                    $"名为 '{objectiveText.name}' 的文本组件，" +
                    "目标文本未写入（检查引用拖的是否为模板内的文本）。",
                this
            );
        }
    }

    // 单个目标行的显示文本：
    // - 已达标：删除线包裹的纯描述（<s> 富文本标签）；
    // - 未达标且需求次数 > 1：描述（当前/需求）；
    // - 未达标且需求次数为 1：纯描述。
    // 描述为空时退回用 objectiveId，保证行不会全空
    private string BuildObjectiveLine(
        QuestAsset quest,
        QuestAsset.QuestObjective objective
    )
    {
        string text = string.IsNullOrWhiteSpace(objective.description)
            ? objective.objectiveId
            : objective.description;

        int current = QuestManager.GetObjectiveCurrent(
            quest.QuestId,
            objective.objectiveId
        );

        bool done = current >= objective.requiredAmount;

        if (done)
        {
            return $"<s>{text}</s>";
        }

        if (objective.requiredAmount > 1)
        {
            return string.Format(
                objectiveFormat,
                text,
                current,
                objective.requiredAmount
            );
        }

        return text;
    }

    // 标题显示文本：任务资产没填 Title 时退回用资产文件名，
    // 保证条目不会因为空标题整条隐身
    private static string GetQuestDisplayName(QuestAsset quest)
    {
        return string.IsNullOrWhiteSpace(quest.Title)
            ? quest.name
            : quest.Title;
    }

    // 在克隆体中按模板引用的名字查找对应组件（文本、容器通用），
    // 免去为每个条目手动接线
    private static T FindInClone<T>(
        T templateRef,
        GameObject clone
    ) where T : Component
    {
        if (templateRef == null || clone == null)
        {
            return null;
        }

        foreach (T component in
            clone.GetComponentsInChildren<T>(true))
        {
            if (component.name == templateRef.name)
            {
                return component;
            }
        }

        return null;
    }

    private static void ClearContainer(Transform container)
    {
        if (container == null)
        {
            return;
        }

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);

            // 跳过禁用子物体：模板可能被直接放在容器下，
            // 不能把模板当垃圾清掉
            if (!child.gameObject.activeSelf)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }
}
