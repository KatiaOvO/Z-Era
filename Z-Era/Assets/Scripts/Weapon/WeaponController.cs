using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GunType
{
    Glock,
    DesertEagle,
    Tec9,
    AK47,
    M4A4,
    Vector,
    Uzi,
    P90,
    MP5
}

public enum FireMode
{
    SemiAuto,
    FullAuto
}

[System.Serializable]
public class GunData
{
    public GunType gunType;
    public FireMode fireMode;
    public int magezineSize;
    public int maxCarriedAmmo;
    public float singleFireRate;
    public float fullAutoFireRate;
    public int damage;
    public float range;

    [Tooltip("每发增加的散布角度")]
    public float spreadPerShot = 0.2f;

    [Tooltip("最大散布角度")]
    public float maxSpread = 3f;

    [Tooltip("每秒恢复的散布角度")]
    public float spreadRecovery = 4f;

    [Tooltip("超过这个时间未射击，散布恢复为0")]
    public float spreadResetTime = 0.2f;

    [Header("后坐力")]
    public float recoilPitch = 0.5f;
    public float recoilYaw = 0.12f;
    public float recoilReturnTime = 0.2f;
}

public class WeaponController : MonoBehaviour
{
    #region 组件

    private Animator animator;
    private int previousAnimatorStateHash;
    private PlayerStamina playerStamina;

    #endregion

    #region 判断参数

    private bool isWalk;
    private bool isSingleFire;
    private bool isAutoFire;
    private bool singleFireTrigger;
    private bool autoFireTrigger;
    private bool isInspect;
    private bool isKnifeAttack;
    private bool isKnifeAttackActive;
    private bool hasEnteredKnifeAttackState;
    private bool canReload;
    private bool isReload;
    private bool isMagazineEmpty;

    #endregion

    #region 匕首候选目标

    private struct KnifeTargetCandidate
    {
        public IDamageable Damageable;
        public Collider Collider;
        public Vector3 HitPoint;
        public Vector3 Direction;
        public float Distance;
        public float RayDeviationSquared;
    }

    private readonly Dictionary<
        IDamageable,
        KnifeTargetCandidate
    > knifeTargetCandidates =
        new Dictionary<
            IDamageable,
            KnifeTargetCandidate
        >();

    #endregion

    #region 匕首攻击参数

    [Header("匕首攻击")]

    [Tooltip("匕首基础伤害")]
    [SerializeField, Min(0f)]
    private float knifeDamage = 50f;

    [Tooltip("匕首攻击距离")]
    [SerializeField, Min(0.1f)]
    private float knifeAttackRange = 1.5f;

    [Tooltip("匕首攻击扇形角度")]
    [SerializeField, Range(0f, 360f)]
    private float knifeAttackAngle = 60f;

    [Tooltip("匕首检测起点，留空时使用 WeaponCamera")]
    [SerializeField]
    private Transform knifeAttackOrigin;

    [Tooltip("可被匕首命中的层级，留空时使用 Zombie")]
    [SerializeField]
    private LayerMask zombieLayerMask;

    [Tooltip("用于遮挡检测的墙体层级，留空时自动配置")]
    [SerializeField]
    private LayerMask knifeObstacleMask;

    [Header("匕首伤害窗口")]

    [Tooltip("伤害窗口开始时的动画归一化时间")]
    [SerializeField, Range(0f, 1f)]
    private float knifeDamageWindowStart = 0.35f;

    [Tooltip("伤害窗口结束时的动画归一化时间")]
    [SerializeField, Range(0f, 1f)]
    private float knifeDamageWindowEnd = 0.55f;

    [Tooltip("范围内碰撞体检测缓冲区初始大小")]
    [SerializeField, Min(1)]
    private int knifeHitBufferSize = 64;

    private Collider[] knifeHitBuffer;

    private bool hasAppliedKnifeDamageThisAttack;
    private Transform playerRoot;

    #endregion

    #region 射击

    private int maxCarriedAmmo;
    public int currentCarriedAmmo;
    private int magazineSize;
    public int currentMagazineAmmo;
    private GunType currentGunType;
    private FireMode currentFireMode;
    private float lastFireTime;

    #endregion

    #region 武器数据

    public GunData[] gunDatas = new GunData[9]
    {
        new GunData
        {
            gunType = GunType.Glock,
            fireMode = FireMode.FullAuto,
            magezineSize = 20,
            maxCarriedAmmo = 240,
            singleFireRate = 0.1f,
            fullAutoFireRate = 0f,
            damage = 15,
            range = 50f
        },
        new GunData
        {
            gunType = GunType.DesertEagle,
            fireMode = FireMode.SemiAuto,
            magezineSize = 7,
            maxCarriedAmmo = 70,
            singleFireRate = 0.5f,
            fullAutoFireRate = 0f,
            damage = 40,
            range = 70f
        },
        new GunData
        {
            gunType = GunType.Tec9,
            fireMode = FireMode.SemiAuto,
            magezineSize = 18,
            maxCarriedAmmo = 180,
            singleFireRate = 0.2f,
            fullAutoFireRate = 0f,
            damage = 20,
            range = 50f
        },
        new GunData
        {
            gunType = GunType.AK47,
            fireMode = FireMode.FullAuto,
            magezineSize = 30,
            maxCarriedAmmo = 240,
            singleFireRate = 0.3f,
            fullAutoFireRate = 0.2f,
            damage = 25,
            range = 60f
        },
        new GunData
        {
            gunType = GunType.M4A4,
            fireMode = FireMode.FullAuto,
            magezineSize = 30,
            maxCarriedAmmo = 240,
            singleFireRate = 0.2f,
            fullAutoFireRate = 0.15f,
            damage = 20,
            range = 65f
        },
        new GunData
        {
            gunType = GunType.Vector,
            fireMode = FireMode.FullAuto,
            magezineSize = 25,
            maxCarriedAmmo = 250,
            singleFireRate = 0.05f,
            fullAutoFireRate = 0.05f,
            damage = 15,
            range = 35f
        },
        new GunData
        {
            gunType = GunType.Uzi,
            fireMode = FireMode.FullAuto,
            magezineSize = 25,
            maxCarriedAmmo = 300,
            singleFireRate = 0.12f,
            fullAutoFireRate = 0.12f,
            damage = 18,
            range = 45f
        },
        new GunData
        {
            gunType = GunType.P90,
            fireMode = FireMode.FullAuto,
            magezineSize = 50,
            maxCarriedAmmo = 300,
            singleFireRate = 0.1f,
            fullAutoFireRate = 0.1f,
            damage = 20,
            range = 50f
        },
        new GunData
        {
            gunType = GunType.MP5,
            fireMode = FireMode.FullAuto,
            magezineSize = 30,
            maxCarriedAmmo = 300,
            singleFireRate = 0.15f,
            fullAutoFireRate = 0.15f,
            damage = 23,
            range = 50f
        }
    };

    #endregion

    #region 引用

    private WeaponEffects weaponEffects;

    public GunData CurrentGunData =>
        gunDatas[(int)currentGunType];

    #endregion

    private void Awake()
    {
        WeaponInitialization();
    }

    private void Start()
    {
        animator = GetComponent<Animator>();
        weaponEffects = GetComponent<WeaponEffects>();

        playerStamina =
            GetComponentInParent<PlayerStamina>();

        if (playerStamina == null)
        {
            Debug.LogError(
                "PlayerStamina component not found in parent objects!",
                this
            );
        }

        ResolveKnifeAttackSettings();
    }

    private void OnDisable()
    {
        EndKnifeAttackTracking(true);
    }

    private void Update()
    {
        ParameterJudgment();
        AnimatorController();
        ShootingState();
        HandleAmmo();
    }

    private void ResolveKnifeAttackSettings()
    {
        if (zombieLayerMask.value == 0)
        {
            zombieLayerMask =
                LayerMask.GetMask("Zombie");
        }

        if (knifeObstacleMask.value == 0)
        {
            int excludedLayers =
                LayerMask.GetMask(
                    "Zombie",
                    "Player",
                    "Weapon",
                    "ZombieHearing"
                );

            knifeObstacleMask =
                Physics.DefaultRaycastLayers &
                ~excludedLayers;
        }

        if (knifeAttackOrigin == null &&
            weaponEffects != null &&
            weaponEffects.weaponCamera != null)
        {
            knifeAttackOrigin =
                weaponEffects.weaponCamera.transform;
        }

        if (knifeAttackOrigin == null)
        {
            Camera rootCamera =
                transform.root
                    .GetComponentInChildren<Camera>();

            if (rootCamera != null)
            {
                knifeAttackOrigin =
                    rootCamera.transform;
            }
        }

        if (knifeHitBuffer == null ||
            knifeHitBuffer.Length !=
            Mathf.Max(1, knifeHitBufferSize))
        {
            knifeHitBuffer =
                new Collider[
                    Mathf.Max(
                        1,
                        knifeHitBufferSize
                    )
                ];
        }

        playerRoot = playerStamina != null
            ? playerStamina.transform
            : transform.root;
    }

    private void ParameterJudgment()
    {
        float h =
            Input.GetAxisRaw("Horizontal");

        float v =
            Input.GetAxisRaw("Vertical");

        isWalk = h != 0f || v != 0f;

        isSingleFire =
            Input.GetKeyDown(KeyCode.Mouse0);

        isAutoFire =
            Input.GetKey(KeyCode.Mouse0);

        isReload =
            Input.GetKeyDown(KeyCode.R);

        isInspect =
            Input.GetKeyDown(KeyCode.V);

        isKnifeAttack =
            Input.GetKeyDown(KeyCode.F);
    }

    private void AnimatorController()
    {
        int currentWeaponIndex =
            (int)currentGunType;

        int currentAnimatorLayer =
            currentWeaponIndex + 1;

        for (int i = 0; i < gunDatas.Length; i++)
        {
            animator.SetLayerWeight(
                i + 1,
                i == currentWeaponIndex ? 1f : 0f
            );
        }

        AnimatorStateInfo currentState =
            animator.GetCurrentAnimatorStateInfo(
                currentAnimatorLayer
            );

        AnimatorStateInfo nextState =
            animator.GetNextAnimatorStateInfo(
                currentAnimatorLayer
            );

        UpdateKnifeAttackState(currentState);

        AnimatorStateInfo actionState =
            currentState;

        if (animator.IsInTransition(
            currentAnimatorLayer))
        {
            if (nextState.IsName("TakeOutWeapon") ||
                nextState.IsName("HolsterWeapon"))
            {
                actionState = nextState;
            }
        }

        if (actionState.fullPathHash !=
            previousAnimatorStateHash)
        {
            if (actionState.IsName("TakeOutWeapon"))
            {
                weaponEffects
                    .PlayWeaponActionSound(
                        "takeout"
                    );
            }
            else if (
                actionState.IsName("HolsterWeapon"))
            {
                weaponEffects
                    .PlayWeaponActionSound(
                        "holster"
                    );
            }

            previousAnimatorStateHash =
                actionState.fullPathHash;
        }

        if (currentState.IsName("KnifeAttack") ||
            currentState.IsName("ReloadOutOfAmmo") ||
            currentState.IsName("ReloadLeftAmmo") ||
            currentState.IsName("TakeOutWeapon") ||
            currentState.IsName("HolsterWeapon"))
        {
            // 当前动画正在播放，清除此帧被忽略的输入。
            singleFireTrigger = false;
            autoFireTrigger = false;
            isSingleFire = false;
            isAutoFire = false;
            isReload = false;
            isInspect = false;
            isKnifeAttack = false;
            return;
        }

        animator.SetBool("walk", isWalk);

        if (isInspect)
        {
            animator.Play(
                "Inspect",
                currentAnimatorLayer
            );
        }

        if (isKnifeAttack)
        {
            TryStartKnifeAttack(
                currentAnimatorLayer
            );
        }

        if (singleFireTrigger)
        {
            animator.Play(
                "Fire",
                currentAnimatorLayer
            );

            weaponEffects.ShootEffects();
            currentMagazineAmmo--;
            singleFireTrigger = false;
        }
        else if (autoFireTrigger)
        {
            animator.Play(
                "Fire",
                currentAnimatorLayer
            );

            weaponEffects.ShootEffects();
            currentMagazineAmmo--;
            autoFireTrigger = false;
        }

        if (canReload &&
            isReload &&
            currentMagazineAmmo == 0)
        {
            animator.Play(
                "ReloadOutOfAmmo",
                currentAnimatorLayer
            );

            weaponEffects.ReloadEffects();
        }
        else if (
            canReload &&
            isReload &&
            currentMagazineAmmo > 0)
        {
            animator.Play(
                "ReloadLeftAmmo",
                currentAnimatorLayer
            );

            weaponEffects.ReloadEffects();
        }
    }

    private void TryStartKnifeAttack(
        int animatorLayer
    )
    {
        if (isKnifeAttackActive)
        {
            return;
        }

        if (playerStamina == null)
        {
            Debug.LogError(
                "Cannot start knife attack: " +
                "PlayerStamina is missing!",
                this
            );

            return;
        }

        if (!playerStamina
            .TryConsumeKnifeAttackStamina())
        {
            return;
        }

        isKnifeAttackActive = true;
        hasEnteredKnifeAttackState = false;
        hasAppliedKnifeDamageThisAttack = false;
        knifeTargetCandidates.Clear();

        animator.Play(
            "KnifeAttack",
            animatorLayer
        );
    }

    private void UpdateKnifeAttackState(
        AnimatorStateInfo currentState
    )
    {
        if (!isKnifeAttackActive)
        {
            return;
        }

        if (currentState.IsName("KnifeAttack"))
        {
            hasEnteredKnifeAttackState = true;

            float normalizedTime =
                currentState.normalizedTime;

            if (!hasAppliedKnifeDamageThisAttack &&
                normalizedTime >=
                    knifeDamageWindowStart &&
                normalizedTime <=
                    knifeDamageWindowEnd)
            {
                hasAppliedKnifeDamageThisAttack =
                    true;

                ApplyKnifeAttackDamage();
            }

            return;
        }

        if (hasEnteredKnifeAttackState)
        {
            EndKnifeAttackTracking(false);
        }
    }

    private void ApplyKnifeAttackDamage()
    {
        knifeTargetCandidates.Clear();

        if (knifeAttackOrigin == null)
        {
            Debug.LogError(
                "Cannot apply knife damage: " +
                "knifeAttackOrigin is missing!",
                this
            );

            return;
        }

        if (knifeHitBuffer == null)
        {
            ResolveKnifeAttackSettings();
        }

        Vector3 origin =
            knifeAttackOrigin.position;

        Vector3 forward =
            knifeAttackOrigin.forward;

        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        forward.Normalize();

        Ray aimRay =
            new Ray(origin, forward);

        int hitCount =
            QueryKnifeHitColliders(origin);

        float halfAngle =
            Mathf.Clamp(
                knifeAttackAngle,
                0f,
                360f
            ) * 0.5f;

        float minimumDot =
            Mathf.Cos(
                halfAngle * Mathf.Deg2Rad
            );

        // 第一遍：为每个 Zombie 选择最接近准星射线的身体 Collider。
        for (int i = 0; i < hitCount; i++)
        {
            Collider targetCollider =
                knifeHitBuffer[i];

            if (targetCollider == null)
            {
                continue;
            }

            float rayDeviationSquared;

            Vector3 targetPoint =
                GetKnifeCandidateHitPoint(
                    targetCollider,
                    aimRay,
                    origin,
                    knifeAttackRange,
                    out rayDeviationSquared
                );

            Vector3 delta =
                targetPoint - origin;

            float distance =
                delta.magnitude;

            if (distance > knifeAttackRange)
            {
                continue;
            }

            Vector3 direction =
                distance > 0.0001f
                    ? delta / distance
                    : forward;

            if (Vector3.Dot(
                forward,
                direction
            ) < minimumDot)
            {
                continue;
            }

            IDamageable damageable =
                targetCollider
                    .GetComponentInParent<
                        IDamageable
                    >();

            if (damageable == null)
            {
                continue;
            }

            ZombieController zombie =
                damageable as ZombieController;

            if (zombie != null &&
                !zombie.CanTakeDamage)
            {
                continue;
            }

            if (IsKnifePathBlocked(
                origin,
                targetPoint,
                distance
            ))
            {
                continue;
            }

            KnifeTargetCandidate candidate =
                new KnifeTargetCandidate
                {
                    Damageable = damageable,
                    Collider = targetCollider,
                    HitPoint = targetPoint,
                    Direction = direction,
                    Distance = distance,
                    RayDeviationSquared =
                        rayDeviationSquared
                };

            if (!knifeTargetCandidates.TryGetValue(
                damageable,
                out KnifeTargetCandidate currentBest
                ))
            {
                knifeTargetCandidates.Add(
                    damageable,
                    candidate
                );

                continue;
            }

            if (IsBetterKnifeCandidate(
                candidate,
                currentBest
                ))
            {
                knifeTargetCandidates[
                    damageable
                ] = candidate;
            }
        }

        // 第二遍：每个 Zombie 只使用最佳 Collider 造成一次伤害。
        foreach (
            KnifeTargetCandidate candidate in
            knifeTargetCandidates.Values
        )
        {
            HitPartMarker hitPartMarker =
                candidate.Collider
                    .GetComponentInParent<
                        HitPartMarker
                    >();

            HitPart hitPart =
                hitPartMarker != null
                    ? hitPartMarker.Part
                    : HitPart.Body;

            DamageInfo damageInfo =
                new DamageInfo
                {
                    damage = knifeDamage,
                    hitPart = hitPart,
                    hitPoint = candidate.HitPoint,
                    souece =
                        DamageSource.KnifeAttack,
                    attacker =
                        playerRoot != null
                            ? playerRoot.gameObject
                            : gameObject
                };

            candidate.Damageable.TakeDamage(
                damageInfo
            );

            ZombieEffects hitEffects =
                candidate.Collider
                    .GetComponentInParent<
                        ZombieEffects
                    >();

            if (hitEffects != null)
            {
                hitEffects.PlayKnifeHitEffect(
                    damageInfo.hitPoint,
                    candidate.Direction
                );
            }
        }

        knifeTargetCandidates.Clear();
    }

    private Vector3 GetKnifeCandidateHitPoint(
        Collider targetCollider,
        Ray aimRay,
        Vector3 origin,
        float maxRange,
        out float rayDeviationSquared
    )
    {
        // 如果中心射线直接命中该 Collider，
        // 使用真实表面交点，并给予最高优先级。
        if (targetCollider.Raycast(
            aimRay,
            out RaycastHit rayHit,
            maxRange
        ))
        {
            rayDeviationSquared = 0f;
            return rayHit.point;
        }

        // 没有直接命中时，寻找 Collider 上
        // 距离摄像机中心射线最近的点。
        Vector3 colliderCenter =
            targetCollider.bounds.center;

        float axialDistance =
            Vector3.Dot(
                colliderCenter - origin,
                aimRay.direction
            );

        axialDistance = Mathf.Clamp(
            axialDistance,
            0f,
            maxRange
        );

        Vector3 closestRayPoint =
            aimRay.GetPoint(axialDistance);

        Vector3 closestColliderPoint =
            targetCollider.ClosestPoint(
                closestRayPoint
            );

        if ((closestColliderPoint -
             closestRayPoint).sqrMagnitude <
            0.000001f)
        {
            closestColliderPoint =
                targetCollider.ClosestPoint(origin);
        }

        if ((closestColliderPoint - origin)
                .sqrMagnitude <
            0.000001f)
        {
            closestColliderPoint =
                targetCollider.bounds.center;
        }

        if ((closestColliderPoint - origin)
                .sqrMagnitude <
            0.000001f)
        {
            closestColliderPoint =
                targetCollider.transform.position;
        }

        rayDeviationSquared =
            (closestColliderPoint -
             closestRayPoint).sqrMagnitude;

        return closestColliderPoint;
    }

    private bool IsBetterKnifeCandidate(
        KnifeTargetCandidate candidate,
        KnifeTargetCandidate currentBest
    )
    {
        const float comparisonTolerance =
            0.0001f;

        if (candidate.RayDeviationSquared <
            currentBest.RayDeviationSquared -
            comparisonTolerance)
        {
            return true;
        }

        if (Mathf.Abs(
            candidate.RayDeviationSquared -
            currentBest.RayDeviationSquared
        ) <= comparisonTolerance)
        {
            return candidate.Distance <
                currentBest.Distance;
        }

        return false;
    }

    private int QueryKnifeHitColliders(
        Vector3 origin
    )
    {
        if (knifeHitBuffer == null ||
            knifeHitBuffer.Length == 0)
        {
            knifeHitBuffer =
                new Collider[
                    Mathf.Max(
                        1,
                        knifeHitBufferSize
                    )
                ];
        }

        while (true)
        {
            int hitCount =
                Physics.OverlapSphereNonAlloc(
                    origin,
                    Mathf.Max(
                        0.1f,
                        knifeAttackRange
                    ),
                    knifeHitBuffer,
                    zombieLayerMask,
                    QueryTriggerInteraction.Ignore
                );

            if (hitCount <
                knifeHitBuffer.Length)
            {
                return hitCount;
            }

            System.Array.Resize(
                ref knifeHitBuffer,
                knifeHitBuffer.Length * 2
            );
        }
    }

    private bool IsKnifePathBlocked(
        Vector3 origin,
        Vector3 targetPoint,
        float targetDistance
    )
    {
        if (knifeObstacleMask.value == 0 ||
            targetDistance <= 0.02f)
        {
            return false;
        }

        Vector3 direction =
            (targetPoint - origin).normalized;

        float castDistance =
            Mathf.Max(
                0f,
                targetDistance - 0.02f
            );

        return Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            castDistance,
            knifeObstacleMask,
            QueryTriggerInteraction.Ignore
        ) && hit.distance < targetDistance;
    }

    private void EndKnifeAttackTracking(
        bool weaponWasDisabled
    )
    {
        knifeTargetCandidates.Clear();

        if (!isKnifeAttackActive)
        {
            return;
        }

        isKnifeAttackActive = false;
        hasEnteredKnifeAttackState = false;
        hasAppliedKnifeDamageThisAttack = false;

        if (playerStamina == null)
        {
            return;
        }

        if (weaponWasDisabled)
        {
            playerStamina
                .CancelKnifeAttackRecoveryPause();
        }
        else
        {
            playerStamina
                .NotifyKnifeAttackEnded();
        }
    }

    private void WeaponInitialization()
    {
        string weaponName =
            gameObject.name;

        currentGunType =
            (GunType)System.Enum.Parse(
                typeof(GunType),
                weaponName
            );

        GunData currentGunData =
            gunDatas[(int)currentGunType];

        maxCarriedAmmo =
            currentGunData.maxCarriedAmmo;

        currentCarriedAmmo =
            maxCarriedAmmo;

        magazineSize =
            currentGunData.magezineSize;

        currentMagazineAmmo =
            magazineSize;

        currentFireMode =
            currentGunData.fireMode;

        lastFireTime = 0f;
    }

    private void ShootingState()
    {
        GunData currentGunData =
            gunDatas[(int)currentGunType];

        if (isSingleFire &&
            currentMagazineAmmo > 0 &&
            Time.time >=
                lastFireTime +
                currentGunData.singleFireRate)
        {
            singleFireTrigger = true;
            lastFireTime = Time.time;
        }

        if (currentGunData.fireMode ==
                FireMode.FullAuto &&
            isAutoFire &&
            currentMagazineAmmo > 0 &&
            Time.time >=
                lastFireTime +
                currentGunData.fullAutoFireRate)
        {
            autoFireTrigger = true;
            lastFireTime = Time.time;
        }

        if (Input.GetKeyUp(KeyCode.Mouse0))
        {
            isSingleFire = false;
            isAutoFire = false;
        }
    }

    private void HandleAmmo()
    {
        if (currentCarriedAmmo > maxCarriedAmmo)
        {
            currentCarriedAmmo =
                maxCarriedAmmo;
        }

        canReload =
            currentMagazineAmmo !=
                magazineSize &&
            currentCarriedAmmo != 0;

        if (!canReload || !isReload)
        {
            return;
        }

        if (currentMagazineAmmo +
                currentCarriedAmmo >
            magazineSize)
        {
            currentCarriedAmmo -=
                magazineSize -
                currentMagazineAmmo;

            currentMagazineAmmo =
                magazineSize;
        }
        else
        {
            currentMagazineAmmo +=
                currentCarriedAmmo;

            currentCarriedAmmo = 0;
        }
    }

    private void OnValidate()
    {
        knifeDamage =
            Mathf.Max(0f, knifeDamage);

        knifeAttackRange =
            Mathf.Max(
                0.1f,
                knifeAttackRange
            );

        knifeAttackAngle =
            Mathf.Clamp(
                knifeAttackAngle,
                0f,
                360f
            );

        knifeDamageWindowStart =
            Mathf.Clamp01(
                knifeDamageWindowStart
            );

        knifeDamageWindowEnd =
            Mathf.Clamp01(
                knifeDamageWindowEnd
            );

        if (knifeDamageWindowEnd <
            knifeDamageWindowStart)
        {
            knifeDamageWindowEnd =
                knifeDamageWindowStart;
        }

        knifeHitBufferSize =
            Mathf.Max(
                1,
                knifeHitBufferSize
            );
    }
}