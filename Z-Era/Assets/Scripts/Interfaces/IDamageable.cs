using UnityEngine;

// 伤害接收接口
public interface IDamageable
{
    void TakeDamage(DamageInfo damageInfo);
}

// 身体部位枚举
public enum BodyPart
{
    Head,   // 头部
    Body,   // 身体
    Arm_L,  // 左臂
    Arm_R,  // 右臂
    Knee_L, // 左膝盖
    Knee_R, // 右膝盖
    Leg // 腿（不需要区分左右）
}

// 伤害信息结构
public struct DamageInfo
{
    public float damage;           // 基础伤害（来自子弹）
    public BodyPart hitPart;       // 击中部位
    public Vector3 hitPoint;       // 击中位置（世界坐标）
    public Vector3 hitDirection;   // 击中方向（子弹飞来的方向，用于受击动画）
}

