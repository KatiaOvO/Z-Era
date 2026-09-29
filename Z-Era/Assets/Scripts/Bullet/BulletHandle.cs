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
    [Tooltip("射线检测的图层，留空则默认检测除 Player/Weapon/Bullet/UI/Ignore Raycast/ZombieHearing/TaskTrigger 外的所有表面")]
    private LayerMask hitLayerMask;

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

    // 当前子弹伤害和攻击者，由 WeaponEffects 在发射时写入。
    private float bulletDamage;
    private GameObject bulletAttacker;

    public float BulletDamage => bulletDamage;

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

        // 没有手动配置图层时，默认命中自身层之外的一切表面：
        // 排除玩家、武器、子弹、UI 和 Ignore Raycast，以及不应被命中的
        // 僵尸听觉范围触发器；Zombie 层正常受伤，其余表面留下弹孔。
        if (hitLayerMask.value == 0)
        {
            hitLayerMask = ~LayerMask.GetMask(
                "Player",
                "Weapon",
                "Bullet",
                "UI",
                "Ignore Raycast",
                "ZombieHearing",
                "TaskTrigger"
            );
        }

        // 任务触发器图层只用于任务检测，子弹完全忽略：
        // 射线不命中、物理不碰撞，命中处理也不会生成弹孔。
        int taskTriggerLayer = LayerMask.NameToLayer("TaskTrigger");
        if (taskTriggerLayer >= 0)
        {
            hitLayerMask &= ~(1 << taskTriggerLayer);
            Physics.IgnoreLayerCollision(
                gameObject.layer,
                taskTriggerLayer,
                true
            );
        }

        // Player/Weapon 图层子弹完全无碰撞直接穿过：
        // 射线检测已排除这两层，物理碰撞也一并关掉。
        // 出膛瞬间子弹碰撞体贴着玩家 CharacterController 与
        // 武器碰撞体，CharacterController 的出穿解算会把玩家
        // 推开（朝下开枪时尤其明显），必须彻底免碰撞。
        int playerLayerIndex = LayerMask.NameToLayer("Player");
        if (playerLayerIndex >= 0)
        {
            Physics.IgnoreLayerCollision(
                gameObject.layer,
                playerLayerIndex,
                true
            );
        }

        int weaponLayerIndex = LayerMask.NameToLayer("Weapon");
        if (weaponLayerIndex >= 0)
        {
            Physics.IgnoreLayerCollision(
                gameObject.layer,
                weaponLayerIndex,
                true
            );
        }

        // 仅用于测试，确认图层配置无误后可以删除
    }

    private void OnEnable()
    {
        // 从对象池取出时重置状态，避免复用上一发子弹的数据
        spawnTime = Time.time;
        isColliding = false;
        bulletVelocity = Vector3.zero;
        bulletDamage = 0f;
        bulletAttacker = null;

        if (bulletRigidbody != null && !bulletRigidbody.isKinematic)
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

                HandleHit(hit.collider, hit.point, hit.normal);
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

    private void HandleHit(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsOnTaskTriggerLayer(hitCollider.gameObject.layer))
        {
            return;
        }

        isColliding = true;

        // 仅用于测试，确认命中后可以删除

        // 训练场墙面：额外留下红色正方体线框，方便查看弹着点分布，
        // 线框跟随被击中的物体移动
        if (IsOnTrainingGroundWallLayer(hitCollider.gameObject.layer))
        {
            BulletHitMarkerManager.Spawn(
                hitPoint,
                hitNormal,
                hitCollider.transform
            );
        }

        // 训练场靶子：统计子弹命中次数，达到需求后由
        // PrologueController 通过事件驱动靶子倒下
        PrologueTarget prologueTarget =
            hitCollider.GetComponentInParent<PrologueTarget>();

        if (prologueTarget != null)
        {
            prologueTarget.RegisterHit(hitCollider);
        }

        ApplyDamage(hitCollider, hitPoint);

        ZombieEffects zombieEffects =
            hitCollider.GetComponentInParent<ZombieEffects>();

        if (zombieEffects != null)
        {
            zombieEffects.PlayHitEffect(hitPoint, hitNormal);
        }
        else if (!IsOnNpcLayer(hitCollider.gameObject.layer) &&
                 !IsOnTrainingGroundWallLayer(hitCollider.gameObject.layer))
        {
            // 非 Zombie 表面不造成伤害，在命中点留下弹孔贴花。
            // NPC 层的角色例外：角色会移动，贴花跟着走很违和；
            // 训练场墙面例外：只留命中线框，不生成弹孔。
            BulletHoleManager.Spawn(
                hitPoint,
                hitNormal,
                hitCollider.transform
            );
        }

        RecycleBullet();
    }

    // 命中物是否处于 NPC 层。层号只查询一次并缓存；
    // 项目里没有 NPC 层时恒为 false，不影响其他表面。
    private static int npcLayer = int.MinValue;

    private static bool IsOnNpcLayer(int layer)
    {
        if (npcLayer == int.MinValue)
        {
            npcLayer = LayerMask.NameToLayer("NPC");
        }

        return npcLayer >= 0 && layer == npcLayer;
    }

    // 命中物是否处于训练场墙面层。层号只查询一次并缓存；
    // 项目里没有该层时恒为 false，不影响其他表面的命中表现。
    private static int trainingGroundWallLayer = int.MinValue;

    private static bool IsOnTrainingGroundWallLayer(int layer)
    {
        if (trainingGroundWallLayer == int.MinValue)
        {
            trainingGroundWallLayer =
                LayerMask.NameToLayer("TrainingGroundWall");
        }

        return trainingGroundWallLayer >= 0 && layer == trainingGroundWallLayer;
    }

    // 任务触发器图层只查询一次并缓存；项目里没有该层时恒为 false。
    private static int taskTriggerLayer = int.MinValue;

    private static bool IsOnTaskTriggerLayer(int layer)
    {
        if (taskTriggerLayer == int.MinValue)
        {
            taskTriggerLayer = LayerMask.NameToLayer("TaskTrigger");
        }

        return taskTriggerLayer >= 0 && layer == taskTriggerLayer;
    }

    // 物理碰撞兜底：如果以后有普通 Collider 的目标，仍然可以处理
    private void OnCollisionEnter(Collision collision)
    {
        if (isColliding ||
            IsOnTaskTriggerLayer(collision.collider.gameObject.layer))
        {
            return;
        }

        ContactPoint contact = collision.GetContact(0);
        HandleHit(collision.collider, contact.point, contact.normal);
    }

    // 触发器兜底：慢速子弹命中触发器时仍可触发。
    // 高速子弹主要由 FixedUpdate() 中的 Raycast 负责。
    private void OnTriggerEnter(Collider other)
    {
        if (isColliding ||
            IsOnTaskTriggerLayer(other.gameObject.layer))
        {
            return;
        }

        Vector3 hitNormal = bulletVelocity.sqrMagnitude > 0f
            ? -bulletVelocity.normalized
            : Vector3.up;

        HandleHit(other, transform.position, hitNormal);
    }

    // 对带有 IDamageable 的命中目标造成伤害
    private void ApplyDamage(Collider hitCollider, Vector3 hitPoint)
    {
        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            return;
        }

        // 从命中的 Collider 向上查找部位标记。
        // 没找到时回退为躯干，保证未配置标记的目标仍能正常受伤。
        HitPartMarker hitPartMarker =
            hitCollider.GetComponentInParent<HitPartMarker>();

        HitPart hitPart = hitPartMarker != null
            ? hitPartMarker.Part
            : HitPart.Body;

        DamageInfo damageInfo = new DamageInfo
        {
            damage = bulletDamage,
            hitPart = hitPart,
            hitPoint = hitPoint,
            source = DamageSource.Bullet,
            attacker = bulletAttacker
        };

        damageable.TakeDamage(damageInfo);
    }

    public void SetPool(BulletPool pool)
    {
        bulletPool = pool;
    }

    // 由 WeaponEffects 调用，传入子弹速度、伤害和攻击者
    public void Launch(Vector3 velocity, float damage, GameObject attacker)
    {
        bulletVelocity = velocity;
        bulletDamage = damage;
        bulletAttacker = attacker;
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
