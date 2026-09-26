using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 子弹孔贴花管理器：在场景中放一个空物体挂载本组件，
// 并拖入弹孔贴图即可。子弹命中非 Zombie 表面时自动生成弹孔。
public class BulletHoleManager : MonoBehaviour
{
    public static BulletHoleManager Instance { get; private set; }

    [Header("贴花设置")]

    [Tooltip("弹孔贴图（带透明通道的 PNG）")]
    [SerializeField]
    private Texture2D bulletHoleTexture;

    [Tooltip("弹孔存活时间（秒）")]
    [SerializeField, Min(1f)]
    private float holeLifeTime = 30f;

    [Tooltip("弹孔尺寸（米）")]
    [SerializeField, Min(0.01f)]
    private float holeSize = 0.12f;

    [Tooltip("沿表面法线的偏移，防止弹孔与表面深度闪烁")]
    [SerializeField, Min(0f)]
    private float surfaceOffset = 0.01f;

    [Tooltip("同时存活的弹孔数量上限，超出时优先销毁最早的弹孔")]
    [SerializeField, Min(1)]
    private int maxHoleCount = 100;

    private Material holeMaterial;
    private readonly Queue<BulletHole> liveHoles = new Queue<BulletHole>();

    // 重合弹孔的深度分层：每个弹孔沿法线额外偏移一小步，
    // 保证后打的弹孔深度更近、稳定地盖住先打的，避免排序抖动闪烁。
    private const float DepthStep = 0.001f;
    private const int DepthLayerCount = 16;

    private int holeDepthIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Shader decalShader = Shader.Find("Custom/BulletHoleDecal");

        if (decalShader == null || bulletHoleTexture == null)
        {
            Debug.LogError(
                "BulletHoleManager 缺少弹孔贴图或 Decal Shader，无法生成弹孔。",
                this
            );

            enabled = false;
            return;
        }

        holeMaterial = new Material(decalShader);
        // BulletHoleDecal.shader 的贴图属性是 _BaseMap，
        // mainTexture 只对 _MainTex 生效，必须按名字赋值。
        holeMaterial.SetTexture("_BaseMap", bulletHoleTexture);
    }

    // 由 BulletHandle 在命中非 Zombie 表面时调用。
    public static void Spawn(
        Vector3 position,
        Vector3 normal,
        Transform surface
    )
    {
        if (Instance == null)
        {
            return;
        }

        Instance.CreateHole(position, normal, surface);
    }

    private void CreateHole(
        Vector3 position,
        Vector3 normal,
        Transform surface
    )
    {
        // 可拾取道具不生成弹孔，避免贴花跟随道具进入背包。
        if (surface.GetComponentInParent<PickableItem>() != null)
        {
            return;
        }

        GameObject hole =
            GameObject.CreatePrimitive(PrimitiveType.Quad);

        Destroy(hole.GetComponent<Collider>());

        hole.name = "Bullet Hole";

        MeshRenderer holeRenderer = hole.GetComponent<MeshRenderer>();
        holeRenderer.sharedMaterial = holeMaterial;
        holeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        holeRenderer.receiveShadows = false;

        // 先在世界空间摆好位置和朝向（法线朝外 + 随机滚动角度），
        // 再挂到被击物体上跟随移动。
        // Quad 的正面朝向是局部 Z 轴，面内随机滚动必须绕局部 Z 转；
        // 绕其他轴会让 Quad 斜插进表面，被深度测试裁成变形的碎片。
        Quaternion rotation =
            Quaternion.LookRotation(normal) *
            Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        hole.transform.SetPositionAndRotation(
            position + normal * (surfaceOffset + (holeDepthIndex++ % DepthLayerCount) * DepthStep),
            rotation
        );

        hole.transform.localScale = Vector3.one * holeSize;

        // 父物体缩放不均匀且有旋转时，SetParent 无法精确补偿，
        // 贴花会被剪切变形（正方形变椭圆/平行四边形）。
        // 这种表面把弹孔挂在管理器节点下（单位缩放），不跟随移动。
        Vector3 surfaceScale = surface.lossyScale;
        float maxAxis = Mathf.Max(
            Mathf.Abs(surfaceScale.x),
            Mathf.Abs(surfaceScale.y),
            Mathf.Abs(surfaceScale.z)
        );
        float minAxis = Mathf.Min(
            Mathf.Abs(surfaceScale.x),
            Mathf.Abs(surfaceScale.y),
            Mathf.Abs(surfaceScale.z)
        );

        bool surfaceScaleUniform =
            minAxis > 0f && maxAxis / minAxis <= 1.02f;

        hole.transform.SetParent(
            surfaceScaleUniform ? surface : transform,
            true
        );

        BulletHole bulletHole = hole.AddComponent<BulletHole>();
        bulletHole.Init(holeLifeTime);
        liveHoles.Enqueue(bulletHole);

        while (liveHoles.Count > maxHoleCount)
        {
            BulletHole oldest = liveHoles.Dequeue();

            if (oldest != null)
            {
                Destroy(oldest.gameObject);
            }
        }
    }
}
