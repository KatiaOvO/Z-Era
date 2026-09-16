using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BulletHandle : MonoBehaviour
{
    [SerializeField]
    [Tooltip("子弹最大存活时间")]
    private float maxLifeTime = 5f;

    [Tooltip("子弹对象池")]
    private BulletPool bulletPool;
    [Tooltip("子弹刚体")]
    private Rigidbody bulletRigidbody;
    [Tooltip("拖尾特效")]
    private TrailRenderer bulletTrail;
    [Tooltip("子弹实例化时间")]
    private float spawnTime;
    [Tooltip("子弹是否碰撞")]
    private bool isColliding = false;

    [Header("水花标记")]
    [Tooltip("红色线框立方体的材质")]
    public Material waterMarkMaterial;
    [Tooltip("线框立方体的存活时间")]
    public float waterMarkLifeTime = 10f;

    private void Awake()
    {
        bulletRigidbody = GetComponent<Rigidbody>();
        bulletTrail = GetComponent<TrailRenderer>();
    }

    private void OnEnable()
    {
        spawnTime = Time.time;
        isColliding = false;
        if (bulletRigidbody != null)
        {
            bulletRigidbody.velocity = Vector3.zero;
            bulletRigidbody.angularVelocity = Vector3.zero;
        }
        if (bulletTrail != null)
        {
            bulletTrail.Clear();
        }
    }

    void Update()
    {
        RecycleBullet();
    }

    // 方法：碰撞时调用（实体碰撞体）
    private void OnCollisionEnter(Collision collision)
    {
        isColliding = true;
        TrySpawnWaterMark(collision.gameObject, collision.GetContact(0).point);
    }

    // 方法：触发时调用（Trigger 碰撞体）
    private void OnTriggerEnter(Collider other)
    {
        isColliding = true;
        TrySpawnWaterMark(other.gameObject, transform.position);
    }

    // 方法：如果碰到 Water 层，生成红色线框立方体
    private void TrySpawnWaterMark(GameObject hitObject, Vector3 hitPoint)
    {
        if (hitObject.layer != LayerMask.NameToLayer("Water"))
        {
            return;
        }

        // 创建立方体
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = hitPoint;
        cube.transform.localScale = Vector3.one * 0.05f;

        // 移除碰撞体，避免影响其他物理检测
        Destroy(cube.GetComponent<Collider>());

        // 替换为线框材质（如果没有指定材质，就用默认材质的红色变体）
        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        if (waterMarkMaterial != null)
        {
            renderer.material = waterMarkMaterial;
        }
        else
        {
            // 兜底：用默认材质的红色半透明版本
            renderer.material.color = new Color(1f, 0f, 0f, 0.3f);
        }

        // 10 秒后销毁
        Destroy(cube, waterMarkLifeTime);
    }

    // 方法：设置对象池
    public void SetPool(BulletPool pool)
    {
        bulletPool = pool;
    }

    // 方法：射出子弹
    public void Launch(Vector3 velocity)
    {
        if (bulletRigidbody != null)
        {
            bulletRigidbody.velocity = velocity;
        }
    }

    // 方法：回收子弹
    private void RecycleBullet()
    {
        if (!isColliding && Time.time - spawnTime < maxLifeTime)
        {
            return;
        }
        if (bulletPool != null)
        {
            bulletPool.Release(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}