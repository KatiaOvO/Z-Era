using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(ZombieAnger))]
public class ZombieAI : MonoBehaviour, INoiseListener
{
    private enum MovementMode
    {
        Idle,
        Walk,
        Run
    }

    [Header("Player Detection")]
    [Tooltip("僵尸最远视野距离")]
    [SerializeField, Min(0f)]
    private float viewDistance = 20f;

    [Tooltip("僵尸的完整视野角度")]
    [SerializeField, Range(0f, 180f)]
    private float viewAngle = 110f;

    [Tooltip("Player 所在图层")]
    [SerializeField]
    private LayerMask playerLayer;

    [Tooltip("阻挡视野和穿透声音检测的图层")]
    [SerializeField]
    private LayerMask obstacleLayer;

    [Tooltip("视线检测起点，留空时使用僵尸自身位置")]
    [SerializeField]
    private Transform eyeTransform;

    [Tooltip("没有手动指定眼睛物体时的检测高度偏移")]
    [SerializeField]
    private Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("视觉检测间隔")]
    [SerializeField, Min(0.02f)]
    private float detectionInterval = 0.1f;

    [Header("Movement")]
    [Tooltip("僵尸每秒旋转角度")]
    [SerializeField, Min(0f)]
    private float rotationSpeed = 540f;

    [Tooltip("行动前是否先面向目标")]
    [SerializeField]
    private bool rotateBeforeMoving = true;

    [Tooltip("允许行动的最大朝向误差角度")]
    [SerializeField, Range(0f, 180f)]
    private float maxMoveAngle = 60f;

    [Header("Attack")]
    [Tooltip("进入攻击状态的距离")]
    [SerializeField, Min(0f)]
    private float attackDistance = 2f;

    [Tooltip("每次攻击伤害")]
    [SerializeField, Min(0f)]
    private float attackDamage = 20f;

    [Tooltip("攻击动画伤害窗口开始比例")]
    [SerializeField, Range(0f, 1f)]
    private float attackDamageWindowStart = 0.35f;

    [Tooltip("攻击动画伤害窗口结束比例")]
    [SerializeField, Range(0f, 1f)]
    private float attackDamageWindowEnd = 0.55f;

    [Header("Hearing")]
    [Tooltip("到达声音位置的距离")]
    [SerializeField, Min(0f)]
    private float noiseInvestigationStopDistance = 0.8f;

    [Tooltip("最后听到声音后的调查记忆时间")]
    [SerializeField, Min(0f)]
    private float noiseMemoryDuration = 6f;

    [Header("Gizmos")]
    [SerializeField]
    private bool drawGizmos = true;

    private static readonly int WalkHash =
        Animator.StringToHash("walk");

    private static readonly int RunHash =
        Animator.StringToHash("run");

    private static readonly int AttackHash =
        Animator.StringToHash("attack");

    private static readonly string AttackStateName = "Attack";

    private const float AttackExitPadding = 0.25f;

    private readonly Collider[] detectionBuffer = new Collider[8];

    private Animator animator;
    private CharacterController characterController;
    private ZombieAnger zombieAnger;

    private Transform currentTarget;
    private float nextDetectionTime;

    private Vector3 lastHeardPosition;
    private float lastHeardTime = float.NegativeInfinity;
    private float lastHeardPriority;
    private bool hasNoiseTarget;

    private bool hasWalkParameter;
    private bool hasRunParameter;
    private bool hasAttackParameter;

    private bool isWalking;
    private bool isRunning;
    private bool isAttacking;
    private bool hasHitThisAttack;

    private int currentAttackCycle = int.MinValue;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        zombieAnger = GetComponent<ZombieAnger>();

        if (eyeTransform == null)
        {
            eyeTransform = transform;
        }

        ResolveLayerMasks();

        hasWalkParameter = HasAnimatorParameter(
            "walk",
            AnimatorControllerParameterType.Bool
        );

        hasRunParameter = HasAnimatorParameter(
            "run",
            AnimatorControllerParameterType.Bool
        );

        hasAttackParameter = HasAnimatorParameter(
            "attack",
            AnimatorControllerParameterType.Bool
        );

        if (!hasWalkParameter)
        {
            Debug.LogError("ZombieAI：Animator 缺少 walk 参数。", this);
        }

        if (!hasRunParameter)
        {
            Debug.LogError("ZombieAI：Animator 缺少 run 参数。", this);
        }

        if (!hasAttackParameter)
        {
            Debug.LogError("ZombieAI：Animator 缺少 attack 参数。", this);
        }

        animator.applyRootMotion = true;
    }

    private void Update()
    {
        if (IsDyingOrDead())
        {
            currentTarget = null;
            ClearNoiseTarget();
            SetMovementMode(MovementMode.Idle);
            SetAttacking(false);
            return;
        }

        if (Time.time >= nextDetectionTime)
        {
            nextDetectionTime = Time.time + detectionInterval;
            currentTarget = FindVisiblePlayer();
        }

        if (currentTarget == null)
        {
            if (TryInvestigateNoise())
            {
                return;
            }

            SetMovementMode(MovementMode.Idle);
            SetAttacking(false);
            return;
        }

        Vector3 targetPosition = currentTarget.position;

        float horizontalDistance = GetHorizontalDistance(
            transform.position,
            targetPosition
        );

        float currentAttackDistance = isAttacking
            ? attackDistance + AttackExitPadding
            : attackDistance;

        FaceTarget(targetPosition);

        if (rotateBeforeMoving &&
            !IsFacingTarget(targetPosition, maxMoveAngle))
        {
            SetMovementMode(MovementMode.Idle);
            SetAttacking(false);
            return;
        }

        if (horizontalDistance <= currentAttackDistance)
        {
            SetMovementMode(MovementMode.Idle);
            SetAttacking(true);
            TryApplyAttackDamage();
            return;
        }

        SetAttacking(false);
        SetMovementMode(GetPreferredMovementMode());
    }

    public void HearNoise(NoiseEvent noiseEvent)
    {
        if (IsDyingOrDead() || noiseEvent.radius <= 0f)
        {
            return;
        }

        if (!noiseEvent.throughWalls &&
            HasObstacleBetween(
                GetEyePosition(),
                noiseEvent.position
            ))
        {
            return;
        }

        /*
         * 无论声音能否覆盖调查位置，都先增加怒气。
         * 这样低优先级脚步不能覆盖枪声调查位置，但仍然可以累积激怒。
         */
        if (zombieAnger != null)
        {
            zombieAnger.AddNoiseAnger(noiseEvent);
        }

        bool hasActiveNoiseMemory =
            hasNoiseTarget &&
            Time.time - lastHeardTime <= noiseMemoryDuration;

        if (hasActiveNoiseMemory &&
            noiseEvent.priority < lastHeardPriority)
        {
            return;
        }

        lastHeardPosition = noiseEvent.position;
        lastHeardTime = Time.time;
        lastHeardPriority = noiseEvent.priority;
        hasNoiseTarget = true;
    }

    private bool TryInvestigateNoise()
    {
        if (!hasNoiseTarget)
        {
            return false;
        }

        if (Time.time - lastHeardTime > noiseMemoryDuration)
        {
            ClearNoiseTarget();
            return false;
        }

        float distanceToNoise = GetHorizontalDistance(
            transform.position,
            lastHeardPosition
        );

        if (distanceToNoise <= noiseInvestigationStopDistance)
        {
            ClearNoiseTarget();
            SetMovementMode(MovementMode.Idle);
            SetAttacking(false);
            return true;
        }

        FaceTarget(lastHeardPosition);

        if (rotateBeforeMoving &&
            !IsFacingTarget(lastHeardPosition, maxMoveAngle))
        {
            SetMovementMode(MovementMode.Idle);
            SetAttacking(false);
            return true;
        }

        SetAttacking(false);
        SetMovementMode(GetPreferredMovementMode());
        return true;
    }

    private MovementMode GetPreferredMovementMode()
    {
        if (zombieAnger != null &&
            zombieAnger.IsEnraged &&
            hasRunParameter)
        {
            return MovementMode.Run;
        }

        return MovementMode.Walk;
    }

    private void SetMovementMode(MovementMode mode)
    {
        // 没有 run 参数时，Run 自动回退到 Walk。
        if (mode == MovementMode.Run && !hasRunParameter)
        {
            mode = MovementMode.Walk;
        }

        bool shouldWalk = mode == MovementMode.Walk;
        bool shouldRun = mode == MovementMode.Run;

        if (isWalking != shouldWalk)
        {
            isWalking = shouldWalk;

            if (hasWalkParameter)
            {
                animator.SetBool(WalkHash, shouldWalk);
            }
        }

        if (isRunning != shouldRun)
        {
            isRunning = shouldRun;

            if (hasRunParameter)
            {
                animator.SetBool(RunHash, shouldRun);
            }
        }
    }

    private void SetAttacking(bool value)
    {
        if (isAttacking == value)
        {
            return;
        }

        isAttacking = value;

        if (value)
        {
            hasHitThisAttack = false;
            currentAttackCycle = int.MinValue;
        }
        else
        {
            currentAttackCycle = int.MinValue;
        }

        if (hasAttackParameter)
        {
            animator.SetBool(AttackHash, value);
        }
    }

    private void ClearNoiseTarget()
    {
        hasNoiseTarget = false;
        lastHeardPriority = 0f;
        lastHeardTime = float.NegativeInfinity;
    }

    private bool HasObstacleBetween(
        Vector3 startPosition,
        Vector3 endPosition
    )
    {
        if (obstacleLayer.value == 0)
        {
            return false;
        }

        return Physics.Linecast(
            startPosition,
            endPosition,
            obstacleLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnAnimatorMove()
    {
        if ((!isWalking && !isRunning && !isAttacking) ||
            IsDyingOrDead())
        {
            return;
        }

        Vector3 deltaPosition = animator.deltaPosition;

        if (deltaPosition.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        if (characterController != null &&
            characterController.enabled)
        {
            characterController.Move(deltaPosition);
        }
        else
        {
            transform.position += deltaPosition;
        }
    }

    private Transform FindVisiblePlayer()
    {
        if (playerLayer.value == 0)
        {
            return null;
        }

        Vector3 detectionOrigin = GetEyePosition();

        int hitCount = Physics.OverlapSphereNonAlloc(
            detectionOrigin,
            viewDistance,
            detectionBuffer,
            playerLayer,
            QueryTriggerInteraction.Ignore
        );

        Transform nearestTarget = null;
        float nearestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = detectionBuffer[i];

            if (candidate == null)
            {
                continue;
            }

            Vector3 targetPoint = candidate.bounds.center;
            Vector3 directionToTarget = targetPoint - detectionOrigin;
            float distanceSqr = directionToTarget.sqrMagnitude;

            if (distanceSqr > viewDistance * viewDistance ||
                !IsInsideViewAngle(targetPoint) ||
                !HasLineOfSight(targetPoint))
            {
                continue;
            }

            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestTarget = candidate.transform;
            }
        }

        return nearestTarget;
    }

    private bool IsInsideViewAngle(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            return true;
        }

        float angle = Vector3.Angle(transform.forward, direction);
        return angle <= viewAngle * 0.5f;
    }

    private bool HasLineOfSight(Vector3 targetPoint)
    {
        return !HasObstacleBetween(
            GetEyePosition(),
            targetPoint
        );
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(
            direction,
            Vector3.up
        );

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private bool IsFacingTarget(
        Vector3 targetPosition,
        float allowedAngle
    )
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            return true;
        }

        float angle = Vector3.Angle(transform.forward, direction);
        return angle <= allowedAngle;
    }

    private void TryApplyAttackDamage()
    {
        if (!isAttacking ||
            !IsInsideAttackDamageWindow() ||
            hasHitThisAttack ||
            IsDyingOrDead() ||
            currentTarget == null)
        {
            return;
        }

        float horizontalDistance = GetHorizontalDistance(
            transform.position,
            currentTarget.position
        );

        if (horizontalDistance > attackDistance)
        {
            return;
        }

        IDamageable damageable =
            currentTarget.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            return;
        }

        DamageInfo damageInfo = new DamageInfo
        {
            damage = attackDamage,
            hitPart = HitPart.Body,
            hitPoint = currentTarget.position,
            souece = DamageSource.ZombieAttack,
            attacker = gameObject
        };

        damageable.TakeDamage(damageInfo);
        hasHitThisAttack = true;
    }

    private bool IsInsideAttackDamageWindow()
    {
        AnimatorStateInfo attackState =
            animator.GetCurrentAnimatorStateInfo(0);

        if (!attackState.IsName(AttackStateName))
        {
            return false;
        }

        int attackCycle =
            Mathf.FloorToInt(attackState.normalizedTime);

        if (attackCycle != currentAttackCycle)
        {
            currentAttackCycle = attackCycle;
            hasHitThisAttack = false;
        }

        float normalizedTime = Mathf.Repeat(
            attackState.normalizedTime,
            1f
        );

        return normalizedTime >= attackDamageWindowStart &&
               normalizedTime <= attackDamageWindowEnd;
    }

    private bool IsDyingOrDead()
    {
        if (animator == null)
        {
            return true;
        }

        AnimatorStateInfo currentState =
            animator.GetCurrentAnimatorStateInfo(0);

        if (currentState.IsName("Die"))
        {
            return true;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState =
                animator.GetNextAnimatorStateInfo(0);

            if (nextState.IsName("Die"))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetEyePosition()
    {
        if (eyeTransform == transform)
        {
            return transform.position + eyeOffset;
        }

        return eyeTransform.position;
    }

    private float GetHorizontalDistance(
        Vector3 from,
        Vector3 to
    )
    {
        Vector3 offset = to - from;
        offset.y = 0f;
        return offset.magnitude;
    }

    private bool HasAnimatorParameter(
        string parameterName,
        AnimatorControllerParameterType parameterType
    )
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName &&
                parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveLayerMasks()
    {
        int playerLayerIndex = LayerMask.NameToLayer("Player");

        if (playerLayer.value == 0 && playerLayerIndex >= 0)
        {
            playerLayer = 1 << playerLayerIndex;
        }

        if (obstacleLayer.value == 0)
        {
            int mask = Physics.DefaultRaycastLayers;

            if (playerLayerIndex >= 0)
            {
                mask &= ~(1 << playerLayerIndex);
            }

            int zombieLayerIndex = LayerMask.NameToLayer("Zombie");

            if (zombieLayerIndex >= 0)
            {
                mask &= ~(1 << zombieLayerIndex);
            }

            obstacleLayer = mask;
        }

        if (playerLayer.value == 0)
        {
            Debug.LogWarning(
                "ZombieAI：没有找到 Player 图层。",
                this
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 origin = eyeTransform == null || eyeTransform == transform
            ? transform.position + eyeOffset
            : eyeTransform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, viewDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        if (hasNoiseTarget)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(
                lastHeardPosition,
                noiseInvestigationStopDistance
            );

            Gizmos.DrawLine(
                transform.position,
                lastHeardPosition
            );
        }

        Vector3 leftDirection =
            Quaternion.Euler(
                0f,
                -viewAngle * 0.5f,
                0f
            ) * transform.forward;

        Vector3 rightDirection =
            Quaternion.Euler(
                0f,
                viewAngle * 0.5f,
                0f
            ) * transform.forward;

        Gizmos.DrawLine(
            origin,
            origin + leftDirection * viewDistance
        );

        Gizmos.DrawLine(
            origin,
            origin + rightDirection * viewDistance
        );
    }
}