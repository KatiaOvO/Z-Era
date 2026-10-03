using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 修复 3D TextMeshPro 在未完全离开视野时被提前视锥剔除的问题：
/// TMP 剔除用的是其生成网格的 bounds，在文字贴近屏幕边缘时可能
/// 早剔。本组件把 bounds 向外扩大一圈余量，文字要连余量一起完全
/// 离开视锥才会被剔除。
/// 注意 TMP 修改颜色等内容属性会触发网格重建并重置 bounds，
/// 因此除了初始扩展外，LateUpdate 中还会检测 bounds 被重置并
/// 重新套用，保证高亮变色后的文字依然有效。
/// 挂在会移出视野的世界空间 3D 文本物体上；Canvas 上的 UGUI 文本
/// 不存在该问题，无需挂载。
/// </summary>
public class TMPBoundsExpander : MonoBehaviour
{
    [Tooltip("包围盒向外扩大的余量（米）")]
    [SerializeField, Min(0f)]
    private float margin = 2f;

    // 每个网格对应的原始（未扩展）bounds：
    // TMP 网格重建后 bounds 会被重置，用它与余量重新计算扩展值
    private readonly Dictionary<Mesh, Bounds> originalBounds =
        new Dictionary<Mesh, Bounds>();

    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();

        if (text == null)
        {
            Debug.LogWarning(
                "TMPBoundsExpander 所在物体没有 TMP_Text 组件，无法扩展包围盒。",
                this
            );
            return;
        }

        // 确保网格已按当前文本生成，再修改其 bounds
        text.ForceMeshUpdate(false, false);

        ExpandAll();
    }

    // TMP 的颜色变化等操作会触发网格重建并重置 bounds，
    // 每帧检查一次，被重置就重新套用扩展值
    private void LateUpdate()
    {
        if (text == null)
        {
            return;
        }

        ExpandAll();
    }

    private void ExpandAll()
    {
        TMP_MeshInfo[] meshInfos = text.textInfo.meshInfo;

        for (int i = 0; i < meshInfos.Length; i++)
        {
            Mesh mesh = meshInfos[i].mesh;

            if (mesh == null)
            {
                continue;
            }

            // 首次遇到该网格（初始或重建后换新网格实例）时，
            // 记录其原始 bounds 作为扩展基准
            if (!originalBounds.TryGetValue(
                mesh,
                out Bounds original))
            {
                original = mesh.bounds;
                originalBounds[mesh] = original;
            }

            Bounds expanded = original;
            expanded.Expand(margin);

            // 只在 bounds 被重置时写入，避免逐帧标记网格变更
            if (mesh.bounds != expanded)
            {
                mesh.bounds = expanded;
            }
        }
    }
}
