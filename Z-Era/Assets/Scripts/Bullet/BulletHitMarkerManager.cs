using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 训练场命中标记管理器：子弹命中 TrainingGroundWall 图层的表面时，
// 在命中点生成一个红色正方体线框，用于查看弹着点分布，
// 效果类似 CS2 训练地图的命中显示。
// 在场景中放一个空物体挂载本组件即可。
public class BulletHitMarkerManager : MonoBehaviour
{
    public static BulletHitMarkerManager Instance { get; private set; }

    [Header("命中标记设置")]

    [Tooltip("线框正方体的边长（米）")]
    [SerializeField, Min(0.01f)]
    private float markerSize = 0.25f;

    [Tooltip("每个线框的存活时间（秒）")]
    [SerializeField, Min(1f)]
    private float markerLifeTime = 20f;

    [Tooltip("线框颜色")]
    [SerializeField]
    private Color markerColor = Color.red;

    [Tooltip("沿表面法线的偏移（米），保证线框整体浮在表面外不被墙体遮挡")]
    [SerializeField, Min(0f)]
    private float surfaceOffset = 0.01f;

    [Tooltip("同时存活的标记数量上限，超出时优先销毁最早的标记")]
    [SerializeField, Min(1)]
    private int maxMarkerCount = 200;

    private Material lineMaterial;
    private Mesh wireCubeMesh;
    private readonly Queue<BulletHole> liveMarkers = new Queue<BulletHole>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        lineMaterial = CreateLineMaterial();

        if (lineMaterial == null)
        {
            Debug.LogError(
                "BulletHitMarkerManager 找不到可用的线框 Shader，无法生成命中标记。",
                this
            );

            enabled = false;
            return;
        }

        // URP 的 Unlit 主色是 _BaseColor，内置管线是 _Color
        if (lineMaterial.HasProperty("_BaseColor"))
        {
            lineMaterial.SetColor("_BaseColor", markerColor);
        }
        else
        {
            lineMaterial.SetColor("_Color", markerColor);
        }

        wireCubeMesh = CreateWireCubeMesh();
    }

    // 线段拓扑的 Mesh 只会被流水线当作线元渲染，
    // 普通 Unlit Shader 即可着色，无需专门的线框 Shader。
    private static Material CreateLineMaterial()
    {
        // URP 项目优先用 URP 的 Unlit
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            // 内置管线的兜底
            shader = Shader.Find("Unlit/Color");
        }

        return shader != null ? new Material(shader) : null;
    }

    // 生成 12 条棱的立方体线框 Mesh，边长 1 米、以原点为中心，
    // 运行时通过 localScale 控制实际大小。
    private static Mesh CreateWireCubeMesh()
    {
        Mesh mesh = new Mesh { name = "Wire Cube" };

        float half = 0.5f;

        mesh.vertices = new Vector3[]
        {
            new(-half, -half, -half),
            new(half, -half, -half),
            new(half, half, -half),
            new(-half, half, -half),
            new(-half, -half, half),
            new(half, -half, half),
            new(half, half, half),
            new(-half, half, half)
        };

        int[] edges =
        {
            // 底面四条边
            0, 1, 1, 2, 2, 3, 3, 0,
            // 顶面四条边
            4, 5, 5, 6, 6, 7, 7, 4,
            // 四条竖边
            0, 4, 1, 5, 2, 6, 3, 7
        };

        mesh.SetIndices(edges, MeshTopology.Lines, 0);
        // 设置顶点不会自动更新包围盒，不重算的话
        // 线框可能被视锥剔除，表现为“明明生成了却看不见”。
        mesh.RecalculateBounds();

        return mesh;
    }

    // 由 BulletHandle 在命中 TrainingGroundWall 表面时调用。
    public static void Spawn(Vector3 position, Vector3 normal)
    {
        if (Instance == null)
        {
            // 场景中没挂本组件时给出醒目提示，而不是静默失败
            Debug.LogWarning(
                "子弹命中了 TrainingGroundWall，但场景中没有 " +
                "BulletHitMarkerManager，无法生成命中线框。" +
                "请在场景中添加挂载了 BulletHitMarkerManager 的物体。"
            );

            return;
        }

        Instance.CreateMarker(position, normal);
    }

    private void CreateMarker(Vector3 position, Vector3 normal)
    {
        GameObject marker = new GameObject("Bullet Hit Marker");

        MeshFilter meshFilter = marker.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = wireCubeMesh;

        MeshRenderer meshRenderer = marker.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = lineMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // 立方体以命中点为基准沿法线抬升半个边长，
        // 让线框整体贴在表面外，而不是一半嵌进墙体被深度测试裁掉。
        // 法线接近竖直时 LookRotation 的默认参考轴会退化，
        // 改用水平轴作参考。
        Vector3 referenceUp = Mathf.Abs(normal.y) > 0.99f
            ? Vector3.forward
            : Vector3.up;

        Quaternion rotation = Quaternion.LookRotation(normal, referenceUp);

        marker.transform.SetPositionAndRotation(
            position + normal * (markerSize * 0.5f + surfaceOffset),
            rotation
        );

        marker.transform.localScale = Vector3.one * markerSize;
        marker.transform.SetParent(transform, true);

        // 复用弹孔的到期销毁逻辑
        BulletHole bulletHole = marker.AddComponent<BulletHole>();
        bulletHole.Init(markerLifeTime);
        liveMarkers.Enqueue(bulletHole);

        while (liveMarkers.Count > maxMarkerCount)
        {
            BulletHole oldest = liveMarkers.Dequeue();

            if (oldest != null)
            {
                Destroy(oldest.gameObject);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
