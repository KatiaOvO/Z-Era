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
public class GunData
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
    [Tooltip("开火判断参数")]
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
    [Tooltip("最大备弹子弹数")]
    private int maxCarriedAmmo;
    [Tooltip("当前弹匣子弹数")]
    private int currentMagazineAmmo;
    [Tooltip("枪支弹匣容量")]
    private int magazineSize;
    [Tooltip("当前枪支类型")]
    private GunType currentGunType;
    [Tooltip("当前射击模式")]
    private FireMode currentFireMode;
    [Tooltip("射击计时器")]
    private float lastFireTime;
    #endregion

    #region 定义10把枪的属性数组
    public GunData[] gunDatas = new GunData[10]
    {
        // Glock 20发，半自动
        new GunData
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
        new GunData
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
        new GunData
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
        new GunData
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
        new GunData
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
        new GunData
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
        new GunData
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
        new GunData
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
        new GunData
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

    void Start()
    {
        animator = GetComponent<Animator>();
        GunInitialization();
    }

    void Update()
    {
        ParameterJudgment();
        AnimatorController();
        UpdateShootingState();
        // Debuglog
        Debug.Log(currentMagazineAmmo + "/" + maxCarriedAmmo);
    }

    // 方法：参数判断
    private void ParameterJudgment()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        // 判断isWalking：按下移动键则为正在行走
        isWalk = h != 0 || v != 0;
        // 判断isHipFire：按下左键则为开火
        // isFire = Input.GetKeyDown(KeyCode.Mouse0);
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
        // 获取当前枪支的名字
        string gunName = gameObject.name;
        // 将字符串类型的枪支名称转换为对应的枚举类型值
        /* 解析：
         *  1.System.Enum.Parse - 这是一个静态方法，用于将字符串解析为枚举类型
         *  2.typeof(GunType) - 获取GunType枚举的类型信息
         *  3.gunName - 字符串变量，包含枪支的名称（比如"Glock", "AK47"等）
         *  4.(GunType) - 类型转换，将解析结果转换为GunType枚举类型
         *  5.currentGunType - 存储转换后的枚举值
         */
        currentGunType = (GunType)System.Enum.Parse(typeof(GunType), gunName);
        // 根据上述获得的枚举类型值获取对应的枪支
        /* 解析：
         *  1.currentGunType - 这是一个枚举类型的变量，存储当前枪支的类型（比如GunType.Glock）
         *  2.(int)currentGunType - 将枚举值转换为整数索引，枚举值在C#中本质上是整数，GunType.Glock 对应索引 0
         *  3.gunDatas[(int)currentGunType] - 从gunDatas数组中获取对应索引的元素
         *  4.GunData currentGunData - 声明一个GunData类型的变量，并赋值为获取到的数据
         *  综上，武器类型枚举中的索引一定要和武器数组中的索引一一对应
         */
        GunData currentGunData = gunDatas[(int)currentGunType];
        // 初始化枪支数据
        magazineSize = currentGunData.magezineSize;
        maxCarriedAmmo = currentGunData.maxCarriedAmmo;
        currentMagazineAmmo = magazineSize; // 初始满弹匣
        currentFireMode = currentGunData.fireMode;
        lastFireTime = 0.0f;
    }

    // 方法：射击状态判断 - 判断isFire
    private void UpdateShootingState()
    {
        GunData currentGunData = gunDatas[(int)currentGunType];
        // 检测鼠标左键按下事件
        if(Input.GetKeyDown(KeyCode.Mouse0))
        {
            // 根据武器类型设置射击状态
            if(currentGunData.fireMode == FireMode.SemiAuto)
            {
                // 半自动武器：单点射击
                // 只有当射击间隔满足时才允许射击
                if(Time.time >= lastFireTime + currentGunData.singleFireRate)
                {
                    isFire = true;  // 设置射击状态为true
                    lastFireTime = Time.time;   // 更新最后射击时间
                }
                else
                {
                    // 全自动武器：持续射击
                    // 按下鼠标左键就设置为true
                    isFire = true;
                }
            }

            // 检测鼠标左键释放事件
            if(Input.GetKeyUp(KeyCode.Mouse0))
            {
                // 释放鼠标左键，停止射击
                isFire = false;
            }

            // 处理持续射击（仅全自动武器）
            if(isFire && currentGunData.fireMode == FireMode.FullAuto)
            {
                // 检查射击间隔是否满足
                if(Time.time >= lastFireTime + currentGunData.singleFireRate)
                {
                    // 满足射击间隔，保持射击状态
                    // 注意：这里不执行射击，只保持isFire为true
                    lastFireTime = Time.time;   // 更新最后射击时间
                }
            }
        }
    }

    // 方法：子弹管理
    private void HandleAmmo()
    {
        
    }
}
