using System.Collections;
using UnityEngine;

/// <summary>
/// 设置面板页签组：挂在 SettingPanelRoot 上。
/// SettingButtonContainer 下的每个按钮挂 SettingTabButton
/// （同级顺序即页签顺序），SettingPanelContainer 下的每个子物体
/// 是一个页签的面板容器，按同级顺序与按钮一一对应。
/// 鼠标移入按钮的悬停音效由 SettingTabButton 自己处理；点击按钮后：
/// 其余按钮的下划线填充立即归零，当前按钮的下划线从 0 填充到 1
/// （时长在检查器设置），同时启用对应面板、禁用其余面板。
/// 重复点击已选中的页签不做任何事（按钮侧通过 IsSelected 跳过）。
/// 运行开始时默认选中第一个页签（下划线直接填满，不播放动画）。
/// 两个容器引用留空时按名字在自身子物体中查找。
/// </summary>
public class SettingTabGroup : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("页签按钮容器（其下每个子物体挂 SettingTabButton）")]
    [SerializeField]
    private Transform buttonContainer;

    [Tooltip("面板容器（其下每个子物体是一个页签的 UI 根，" +
        "顺序与按钮一致）")]
    [SerializeField]
    private Transform panelContainer;

    [Header("表现")]

    [Tooltip("下划线从 0 填充到 1 的时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float fillDuration = 1f;

    private SettingTabButton[] tabs;
    private GameObject[] panels;

    // 当前选中页签的索引，-1 表示尚未选中任何页签
    private int selectedIndex = -1;

    private Coroutine fillCoroutine;

    private void Awake()
    {
        if (buttonContainer == null)
        {
            buttonContainer = transform.Find("SettingButtonContainer");
        }

        if (panelContainer == null)
        {
            panelContainer = transform.Find("SettingPanelContainer");
        }

        if (buttonContainer == null || panelContainer == null)
        {
            Debug.LogError(
                "SettingTabGroup 缺少按钮容器或面板容器引用，" +
                "且无法按名字（SettingButtonContainer / " +
                "SettingPanelContainer）在子物体中找到。",
                this
            );
        }
    }

    // 面板每次被激活（打开）都回到第一个页签（常规）：
    // 关闭再打开不应停留在上次离开的页签。
    // 首次激活时 Awake 先于 OnEnable 执行，容器引用已就绪
    private void OnEnable()
    {
        CollectTabsAndPanels();

        // 运行开始默认选中第一个页签，初始状态即完整可见
        if (tabs != null && tabs.Length > 0 && tabs[0] != null)
        {
            SelectImmediate(tabs[0]);
        }
    }

    // 按钮按组件收集（包含未激活的），面板按容器子物体收集，
    // 两者的同级顺序即一一对应关系
    private void CollectTabsAndPanels()
    {
        if (buttonContainer == null || panelContainer == null)
        {
            return;
        }

        tabs = buttonContainer
            .GetComponentsInChildren<SettingTabButton>(true);

        panels = new GameObject[panelContainer.childCount];

        for (int i = 0; i < panels.Length; i++)
        {
            panels[i] = panelContainer.GetChild(i).gameObject;
        }

        if (tabs.Length != panels.Length)
        {
            Debug.LogWarning(
                $"SettingTabGroup：按钮数量（{tabs.Length}）与面板数量" +
                $"（{panels.Length}）不一致，多出的部分不参与切换。",
                this
            );
        }
    }

    // 该按钮是否为当前选中页签：按钮重复点击时据此
    // 跳过点击音效与下划线重新填充
    public bool IsSelected(SettingTabButton tab)
    {
        return IndexOf(tab) == selectedIndex;
    }

    // 页签切换入口（SettingTabButton 点击时调用）：
    // 全部下划线归零 → 面板开关 → 选中按钮的下划线播放填充动画
    public void Select(SettingTabButton tab)
    {
        int index = IndexOf(tab);

        if (index < 0)
        {
            return;
        }

        selectedIndex = index;

        StopFill();

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
            {
                tabs[i].SetUnderlineFill(0f);
            }
        }

        ApplyPanelState(index);

        fillCoroutine = StartCoroutine(
            FillRoutine(tabs[index])
        );
    }

    // 不播放动画的直接选中：运行开始时建立初始状态用
    private void SelectImmediate(SettingTabButton tab)
    {
        int index = IndexOf(tab);

        if (index < 0)
        {
            return;
        }

        selectedIndex = index;

        StopFill();

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
            {
                tabs[i].SetUnderlineFill(i == index ? 1f : 0f);
            }
        }

        ApplyPanelState(index);
    }

    private void ApplyPanelState(int selectedIndex)
    {
        int count = Mathf.Min(tabs.Length, panels.Length);

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
            {
                // 数量不一致时多出的面板保持禁用，
                // 不受任意页签控制
                panels[i].SetActive(
                    i < count && i == selectedIndex
                );
            }
        }
    }

    private IEnumerator FillRoutine(SettingTabButton target)
    {
        float time = 0f;

        while (time < fillDuration)
        {
            time += Time.unscaledDeltaTime;

            target.SetUnderlineFill(
                Mathf.Clamp01(time / fillDuration));

            yield return null;
        }

        target.SetUnderlineFill(1f);
        fillCoroutine = null;
    }

    private int IndexOf(SettingTabButton tab)
    {
        if (tabs == null)
        {
            CollectTabsAndPanels();
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] == tab)
            {
                return i;
            }
        }

        return -1;
    }

    private void StopFill()
    {
        if (fillCoroutine == null)
        {
            return;
        }

        StopCoroutine(fillCoroutine);
        fillCoroutine = null;
    }
}
