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

    private void Awake()
    {
        bulletRigidbody = GetComponent<Rigidbody>();
        bulletTrail = GetComponent<TrailRenderer>();
    }

    private void OnEnable()
    {
        // 每次从对象池取出时重新初始化状态
        spawnTime = Time.time;
        isColliding = false;
        if (bulletRigidbody != null)
        {
            bulletRigidbody.velocity = Vector3.zero;
            bulletRigidbody.angularVelocity = Vector3.zero;
        }
        // 清除上一次使用时留下的拖尾
        if (bulletTrail != null)
        {
            bulletTrail.Clear();
        }
    }

    void Start()
    {
        
    }

    void Update()
    {
        RecycleBullet();
    }

    // 方法：碰撞时调用
    private void OnCollisionEnter(Collision collision)
    {
        isColliding = true;
    }

    // 方法：
    public void SetPool(BulletPool pool)
    {
        bulletPool = pool;
    }

    // 方法：射出子弹，即为子弹赋予速度
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
        // 子弹碰撞或者未碰撞但超出最大存活时间
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
            Destroy(gameObject);    // 此处为兜底销毁，正常情况下不会走到这个else，目的是防止子弹在对象池引用异常后永远不会消失
        }
    }
}
