using System.Collections;
using System.Collections.Generic;
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
    #endregion

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if(health <= 0)
        {
            health = 0;
        }
        ZombieAnimation();
        Debug.Log(health);
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
}
