using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ZombieController : MonoBehaviour, IDamageable
{
    #region 属性
    [Tooltip("生命值")]
    private float health = 100f;
    #endregion

    #region 参数
    [Tooltip("判断是否死亡")]
    private bool isDead = false;

    // 防止死亡清理逻辑重复执行。
    private bool deathCleanupStarted = false;
    #endregion

    #region 组件
    [Tooltip("动画器")]
    private Animator animator;
    [Tooltip("角色控制器")]
    private CharacterController characterController;
    #endregion

    #region 引用
    private ZombieEffects zombieEffects;
    #endregion

    void Start()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        zombieEffects = GetComponent<ZombieEffects>();
    }

    void Update()
    {
        if (health <= 0f && !isDead)
        {
            health = 0f;
            isDead = true;

            // 僵尸死亡后不再需要移动和碰撞计算。
            if (characterController != null)
            {
                characterController.enabled = false;
            }
        }

        ZombieAnimation();

        // 存活期间不再执行死亡动画和碰撞体清理逻辑，
        // 避免每只 Zombie 每帧都进行 LINQ 查询和数组分配。
        if (!isDead)
        {
            return;
        }

        ClearComponents();
    }

    // 实现接口方法
    public void TakeDamage(DamageInfo damageInfo)
    {
        // 根据命中部位取得伤害倍率，再计算这一发子弹的最终伤害
        float damageMultiplier = GetDamageMultiplier(damageInfo.hitPart);
        float finalDamage = damageInfo.damage * damageMultiplier;

        health -= finalDamage;
    }

    // 根据命中部位返回对应的伤害倍率
    private float GetDamageMultiplier(HitPart hitPart)
    {
        switch (hitPart)
        {
            // 头部伤害最高
            case HitPart.Head:
                return 2.0f;

            // 躯干作为标准伤害基准
            case HitPart.Body:
                return 1.0f;

            // 四肢和膝盖受到较低伤害
            case HitPart.Arm_L:
            case HitPart.Arm_R:
            case HitPart.Knee_L:
            case HitPart.Knee_R:
            case HitPart.Leg:
                return 0.8f;

            // 脚部受到最低伤害
            case HitPart.Feet:
                return 0.5f;

            // 未配置的部位使用标准伤害，避免漏配时伤害变成零
            default:
                return 1.0f;
        }
    }

    // 方法：zombie动画
    private void ZombieAnimation()
    {
        if (health <= 0)
        {
            animator.SetBool("die", isDead);
        }
    }

    // 方法：zombie死亡后清除碰撞体和触发器以及销毁自身
    private void ClearComponents()
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (!info.IsName("Die"))
        {
            return;
        }

        if (info.normalizedTime < 1.0f ||
            animator.IsInTransition(0))
        {
            return;
        }

        // 触发器只需要在死亡动画结束后收集一次。
        // 原先每帧执行 LINQ 和 ToArray 会产生大量 GC。
        if (!deathCleanupStarted)
        {
            deathCleanupStarted = true;

            int hearingLayer =
                LayerMask.NameToLayer("ZombieHearing");

            // 保留 ZombieHearing 上的听觉触发器。
            // 如果以后接入对象池复活 Zombie，不需要重新创建它。
            Collider[] triggers = GetComponentsInChildren<Collider>(
                includeInactive: true
            ).Where(c =>
                c != null &&
                c.isTrigger &&
                c.transform != transform &&
                (hearingLayer < 0 ||
                 c.gameObject.layer != hearingLayer)
            ).ToArray();

            foreach (Collider trigger in triggers)
            {
                if (trigger != null)
                {
                    Destroy(trigger);
                }
            }
        }

        zombieEffects.ZombieDissolve();

        if (zombieEffects.isFullyDissolved)
        {
            Destroy(gameObject);
        }
    }
}