using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ZombieController : MonoBehaviour , IDamageable
{
    #region 属性
    [Tooltip("生命值")]
    private float health = 100f;
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
        if(health <= 0)
        {
            health = 0;
        }
        ZombieAnimation();
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
        if(health <= 0)
        {
            animator.Play("Death");
        }
    }

    // 方法：zombie死亡后清除碰撞体和触发器
    private void ClearComponents()
    {
        // zombie生命值为0时清除character controller，以免后续影响碰撞
        if (health <= 0)
        {
            Destroy(characterController);
        }
        // zombie死亡动画播放完毕后清除所有部位触发器，以免后续影响溅血粒子系统
        Collider[] triggers = GetComponentsInChildren<Collider>(includeInactive: true).Where(c => c.isTrigger && c.transform != transform).ToArray();
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName("Death")) return;
        if(info.normalizedTime >= 1.0f && !animator.IsInTransition(0))
        {
            foreach (var trigger in triggers)
            {
                Destroy(trigger);
            }
            zombieEffects.ZombieDissolve();
        }
    }
}
