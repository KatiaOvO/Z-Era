using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BulletHandle : MonoBehaviour
{
    [SerializeField]
    [Tooltip("子弹最大存活时间")]
    private float maxLifeTime = 5f;

    [Header("Hit Detection")]
    // 子弹命中检测使用的图层。
    // 默认只检测 Zombie 层，避免射线打到玩家、武器、场景等无关碰撞体。
    [SerializeField]
    [Tooltip("射线检测的图层，留空则默认使用 Zombie 层")]
    private LayerMask hitLayerMask;

    // 预留的子弹扫描半径。
    // 当前测试版本使用 Physics.Raycast，这个值暂时不参与检测；
    // 以后换回 SphereCast 时会用到。
    [SerializeField]
    [Tooltip("子弹扫描半径")]
    private float bulletRadius = 0.05f;

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

    // 保存子弹速度，由 Launch() 设置。
    // Rigidbody 已设为 Kinematic，所以子弹不由物理引擎自动移动，
    // 而是在 FixedUpdate() 中手动移动。
    private Vector3 bulletVelocity;

    [Header("命中特效")]
    [Tooltip("溅血粒子预制体")]
    public ParticleSystem bloodEffectPrefab;

    [Tooltip("溅血特效存活时间")]
    public float bloodEffectLifeTime = 2f;

    private void Awake()
    {
        bulletRigidbody = GetComponent<Rigidbody>();
        bulletTrail = GetComponent<TrailRenderer>();

        // 使用 Kinematic Rigidbody：
        // 1. 子弹不会被物理引擎推挤或反弹
        // 2. 由脚本手动控制移动，避免高速子弹先穿过触发器再被检测到
        if (bulletRigidbody != null)
        {
            bulletRigidbody.isKinematic = true;
            bulletRigidbody.useGravity = false;
        }

        // 没有手动配置图层时，默认检测 Zombie 层
        if (hitLayerMask.value == 0)
        {
            hitLayerMask = LayerMask.GetMask("Zombie");
        }

        // 仅用于测试，确认图层配置无误后可以删除
        Debug.Log(
            $"{name} hitLayerMask={hitLayerMask.value}, " +
            $"bulletRadius={bulletRadius}, " +
            $"zombieLayer={LayerMask.NameToLayer("Zombie")}"
        );
    }

    private void OnEnable()
    {
        // 从对象池取出时重置状态，避免复用上一发子弹的数据
        spawnTime = Time.time;
        isColliding = false;
        bulletVelocity = Vector3.zero;

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

    private void FixedUpdate()
    {
        if (isColliding)
        {
            return;
        }

        // 使用 Rigidbody.position 而不是 transform.position，
        // 因为子弹 Rigidbody 可能开启了插值，transform.position 不一定是物理位置
        Vector3 currentPosition = bulletRigidbody != null
            ? bulletRigidbody.position
            : transform.position;

        // 先计算这一物理帧会走到的位置，再在这段路径上做射线检测。
        // 顺序必须是“先检测，再移动”，否则子弹会先穿过触发器。
        Vector3 nextPosition =
            currentPosition + bulletVelocity * Time.fixedDeltaTime;

        Vector3 moveDirection = nextPosition - currentPosition;
        float moveDistance = moveDirection.magnitude;

        if (moveDistance > 0f)
        {
            if (Physics.Raycast(
                currentPosition,
                moveDirection.normalized,
                out RaycastHit hit,
                moveDistance,
                hitLayerMask,
                QueryTriggerInteraction.Collide))
            {
                // 命中后先把子弹移动到命中点，再回收。
                // 这样子弹不会停在触发器后面，也不会继续飞行。
                if (bulletRigidbody != null)
                {
                    bulletRigidbody.position = hit.point;
                }

                transform.position = hit.point;

                HandleHit(hit);
                return;
            }

            // 没有命中时手动移动到下一位置
            if (bulletRigidbody != null)
            {
                bulletRigidbody.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }
        }
    }

    private void Update()
    {
        // 超时或已经命中时回收子弹
        RecycleBullet();
    }

    private void HandleHit(RaycastHit hit)
    {
        isColliding = true;

        // 仅用于测试，确认命中后可以删除
        Debug.Log($"Bullet hit: {hit.collider.name}");

        SpawnHitEffect(hit.point, hit.normal);

        RecycleBullet();
    }

    // 物理碰撞兜底：如果以后有普通 Collider 的目标，仍然可以处理
    private void OnCollisionEnter(Collision collision)
    {
        if (isColliding)
        {
            return;
        }

        isColliding = true;
        ContactPoint contact = collision.GetContact(0);
        SpawnHitEffect(contact.point, contact.normal);
        RecycleBullet();
    }

    // 触发器兜底：慢速子弹命中触发器时仍可触发。
    // 高速子弹主要由 FixedUpdate() 中的 Raycast 负责。
    private void OnTriggerEnter(Collider other)
    {
        if (isColliding)
        {
            return;
        }

        isColliding = true;
        Vector3 hitNormal = bulletVelocity.sqrMagnitude > 0f
            ? -bulletVelocity.normalized
            : Vector3.up;

        SpawnHitEffect(transform.position, hitNormal);
        RecycleBullet();
    }

    // 在命中点生成溅血粒子效果
    private void SpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (bloodEffectPrefab == null)
        {
            return;
        }

        Quaternion effectRotation = Quaternion.LookRotation(hitNormal);
        ParticleSystem effect = Instantiate(
            bloodEffectPrefab,
            hitPoint,
            effectRotation
        );

        if (!effect.main.playOnAwake)
        {
            effect.Play();
        }

        Destroy(effect.gameObject, bloodEffectLifeTime);
    }

    public void SetPool(BulletPool pool)
    {
        bulletPool = pool;
    }

    // 由 WeaponEffects 调用，传入子弹速度和方向
    public void Launch(Vector3 velocity)
    {
        bulletVelocity = velocity;
    }

    // 回收条件：已经命中，或者超过最大存活时间
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
