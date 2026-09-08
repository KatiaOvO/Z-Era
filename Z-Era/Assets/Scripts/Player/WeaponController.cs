using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 枪支类型枚举
public enum GunType
{
    Glock,
    DesertEagle,
    Tec9,
    AK47,
    M4A4,
    XM1014,
    Vector,
    Uzi,
    P90,
    MP5
}

// 开火类型枚举
public enum FireMode
{
    SemiAuto,   // 半自动
    FullAuto    // 全自动
}

[System.Serializable]
// 枪支数据类
public class GunDate
{
    public GunType gunType;
    public FireMode fireMode;
    public int magezineSize;    // 弹匣容量
    public int maxCarriedAmmo;  // 最大备弹数
    public float singleFireRate;    // 单点射击间隔
    public float fullAutoFireRate;  //持续射击间隔
    public int damage;  // 伤害
    public float range; // 射程
}

public class WeaponController : MonoBehaviour
{
    #region 组件
    [Tooltip("武器动画器")]
    private Animator animator;
    #endregion

    #region 判断参数
    [Tooltip("行走判断参数")]
    private bool isWalk;
    [Tooltip("腰射开火判断参数")]
    private bool isFire;
    [Tooltip("检视判断参数")]
    private bool isInspect;
    [Tooltip("匕首攻击判断参数")]
    private bool isKnifeAttack;
    [Tooltip("是否能开火判断参数")]
    private bool isCanFire;
    [Tooltip("弹匣是否完全打空")]
    private bool isMagazineEmpty;
    #endregion

    #region 射击
    [Tooltip("当前备弹子弹数")]
    private int carriedAmmo = 120;
    [Tooltip("当前弹匣子弹数")]
    private int magazineAmmo = 20;
    [Tooltip("枪支弹匣容量")]
    private int magazineSize = 20;
    [Tooltip("当前枪支类型")]
    private GunType gunType;
    [Tooltip("当前射击模式")]
    private FireMode fireMode;
    #endregion

    #region 枪支编号
    [Tooltip("当前枪支编号")]
    private int currentGunNum = 999;    // 占位符
    #endregion

    #region 定义10把枪的属性数组
    public GunDate[] gunDates = new GunDate[10]
    {
        // Glock 20发，半自动
        new GunDate
        {
            gunType = GunType.Glock,
            fireMode = FireMode.SemiAuto,
            magezineSize = 20,
            maxCarriedAmmo = 240,
            singleFireRate = 0.2f,
            fullAutoFireRate = 0.0f,
            damage = 15,
            range = 50f
        },
        // Desert Eagle 7发，半自动
        new GunDate
        {
            gunType = GunType.DesertEagle,
            fireMode = FireMode.SemiAuto,
            magezineSize = 7,
            maxCarriedAmmo = 70,
            singleFireRate = 0.5f,
            fullAutoFireRate = 0.0f,
            damage = 40,
            range = 70f
        },
        // Tec9 18发，半自动
        new GunDate
        {
            gunType = GunType.Tec9,
            fireMode = FireMode.SemiAuto,
            magezineSize = 18,
            maxCarriedAmmo = 180,
            singleFireRate = 0.2f,
            fullAutoFireRate = 0.0f,
            damage = 20,
            range = 50f
        },
        // AK47 30发，全自动
        new GunDate
        {
            gunType = GunType.AK47,
            fireMode = FireMode.FullAuto,
            magezineSize = 30,
            maxCarriedAmmo = 180,
            singleFireRate = 0.3f,
            fullAutoFireRate = 0.2f,
            damage = 25,
            range = 60f
        },
        // M4A4 30发，全自动
        new GunDate
        {
            gunType = GunType.M4A4,
            fireMode = FireMode.FullAuto,
            magezineSize = 30,
            maxCarriedAmmo = 240,
            singleFireRate = 0.3f,
            fullAutoFireRate = 0.15f,
            damage = 20,
            range = 65f
        },
        // XM1014 7发，半自动
        new GunDate
        {
            gunType = GunType.XM1014,
            fireMode = FireMode.SemiAuto,
            magezineSize = 7,
            maxCarriedAmmo = 42,
            singleFireRate = 1.5f,
            fullAutoFireRate = 0.0f,
            damage = 100,
            range = 30f
        },
        // Vector 20发，全自动
        new GunDate
        {
            gunType = GunType.Vector,
            fireMode = FireMode.FullAuto,
            magezineSize = 20,
            maxCarriedAmmo = 240,
            singleFireRate = 0.05f,
            fullAutoFireRate = 0.05f,
            damage = 15,
            range = 35f
        },
        // Uzi 25发，全自动
        new GunDate
        {
            gunType = GunType.Uzi,
            fireMode = FireMode.FullAuto,
            magezineSize = 25,
            maxCarriedAmmo = 300,
            singleFireRate = 0.12f,
            damage = 18,
            range = 45f
        },
        // P90 50发，全自动
        new GunDate
        {
            gunType = GunType.P90,
            fireMode = FireMode.FullAuto,
            magezineSize = 50,
            maxCarriedAmmo = 400,
            singleFireRate = 0.1f,
            fullAutoFireRate = 0.1f,
            damage = 20,
            range = 50f
        },
        // MP5 30发，全自动
        new GunDate
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

    void Start()
    {
        animator = GetComponent<Animator>();
        GetGunNum();
    }

    void Update()
    {
        ParameterJudgment();
        AnimatorController();
        Reload();
        // Debuglog
        Debug.Log(magazineAmmo + "/" + carriedAmmo);
    }

    // 方法：参数判断
    private void ParameterJudgment()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        // 判断isWalking：按下移动键则为正在行走
        isWalk = h != 0 || v != 0;
        // 判断isHipFire：按下左键则为开火
        isFire = Input.GetKeyDown(KeyCode.Mouse0);
        // 判断isInspecting：按下V键则为检视武器
        isInspect = Input.GetKeyDown(KeyCode.V);
        // 判断isKnifeAttack：按下F键则为匕首攻击
        isKnifeAttack = Input.GetKeyDown(KeyCode.F);
    }

    // 方法：武器动画控制器
    private void AnimatorController()
    {
        animator.SetBool("walk", isWalk);
        if (isFire) animator.Play("Fire", 1, 0.0f);   // **修改**：若无后两个参数，即层级和从头（0%）开始播放，则无法实现快速连点射击
        if (isInspect) animator.Play("Inspect", 1, 0.0f);
        if (isKnifeAttack) animator.Play("KnifeAttack", 1, 0.0f);
    }

    // 方法：枪支初始化
    private void GunInitialization()
    {
        // 获取当前枪支类型
        string gunName = gameObject.name;
    }

    // 方法：换弹逻辑
    private void Reload()
    {
        
    }

    // 方法：获取当前枪支编号
    private void GetGunNum()
    {
        // 由于部分枪支的逻辑相同，且枪支种类有限，故使用编号分类
        string gunName = gameObject.name;
        switch(gunName)
        {
            case "Glock":
                currentGunNum = 0;
                break;
            case "DesertEagle":
                currentGunNum = 1;
                break;
            case "Tec9":
                currentGunNum = 2;
                break;
            case "AK47":
                currentGunNum = 3;
                break;
            case "A4M4":
                currentGunNum = 4;
                break;
            case "XM1014":
                currentGunNum = 5;
                break;
            case "Vector":
                currentGunNum = 6;
                break;
            case "Uzi":
                currentGunNum = 7;
                break;
            case "P90":
                currentGunNum = 8;
                break;
            case "MP5":
                currentGunNum = 9;
                break;
        }
    }
}
