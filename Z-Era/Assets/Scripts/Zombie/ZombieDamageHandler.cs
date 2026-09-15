using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieDamageHandler : MonoBehaviour , IDamageable
{
    #region 属性
    [Tooltip("生命值")]
    private float health = 100f;
    #endregion

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    // 实现接口方法
    public void TakeDamage(DamageInfo damageInfo)
    {

    }
}
