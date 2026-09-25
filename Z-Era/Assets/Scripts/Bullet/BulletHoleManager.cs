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
        holeMaterial.mainTexture = bulletHoleTexture;
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
        Quaternion rotation =
            Quaternion.LookRotation(normal) *
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        hole.transform.SetPositionAndRotation(
            position + normal * surfaceOffset,
            rotation
        );

        hole.transform.localScale = Vector3.one * holeSize;
        hole.transform.SetParent(surface, true);

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
