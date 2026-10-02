using TMPro;
using UnityEngine;

/// <summary>
/// 修复 3D TextMeshPro 在未完全离开视野时被提前视锥剔除的问题：
/// TMP 剔除用的是其生成网格的 bounds，在文字贴近屏幕边缘时可能
/// 早剔。本组件在网格生成后把 bounds 向外扩大一圈余量，文字要连
/// 余量一起完全离开视锥才会被剔除。
/// 挂在会移出视野的世界空间 3D 文本物体上；Canvas 上的 UGUI 文本
/// 不存在该问题，无需挂载。
/// 注意：仅适用于运行时不变文本内容的文字（本训练场的选项牌均满足）；
/// 若文字内容会动态变化，网格重建会重置 bounds，需要重新扩展。
/// </summary>
public class TMPBoundsExpander : MonoBehaviour
{
    [Tooltip("包围盒向外扩大的余量（米）")]
    [SerializeField, Min(0f)]
    private float margin = 2f;

    private void Awake()
    {
        TMP_Text text = GetComponent<TMP_Text>();

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

        TMP_MeshInfo[] meshInfos = text.textInfo.meshInfo;

        for (int i = 0; i < meshInfos.Length; i++)
        {
            Mesh mesh = meshInfos[i].mesh;

            if (mesh == null)
            {
                continue;
            }

            Bounds bounds = mesh.bounds;
            bounds.Expand(margin);
            mesh.bounds = bounds;
        }
    }
}
