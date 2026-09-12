using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponEffects : MonoBehaviour
{
    #region 武器效果
    [Tooltip("枪声音效")]
    private AudioClip shootSound;
    [Tooltip("换弹音效_不为空")]
    private AudioClip reloadSound_LeftAmmo;
    [Tooltip("换弹音效_为空")]
    private AudioClip reloadSound_OutOfAmmo;
    [Tooltip("子弹实例化位置（枪口）")]
    private Transform bulletSpawnPoint;
    [Tooltip("枪焰")]
    private ParticleSystem muzzleFlash;
    [Tooltip("子弹类型")]
    private GameObject bullet;
    [Tooltip("弹壳抛出位置")]
    private Transform casingEjectionPoint;
    [Tooltip("子弹速度")]
    public float bulletSpeed = 100f;
    #endregion

    #region 当前武器
    [Tooltip("当前武器索引")]
    private WeaponEffectConfig currentWeaponConfig;
    #endregion

    #region 组件
    [Tooltip("音源")]
    private AudioSource audioSource;
    [Tooltip("武器相机")]
    public Camera weaponCamera;
    #endregion

    #region 脚本
    private WeaponController weaponController;
    #endregion

    [System.Serializable]
    public class WeaponEffectConfig
    {
        [Tooltip("武器类型")]
        public GunType gunType;
        [Tooltip("枪声音效")]
        public AudioClip shootSound;
        [Tooltip("换弹音效_不为空")]
        public AudioClip reloadSound_LeftAmmo;
        [Tooltip("换弹音效_为空")]
        public AudioClip reloadSound_OutOfAmmo;
        [Tooltip("子弹实例化位置（枪口）")]
        public Transform bulletSpawnPoint;
        [Tooltip("枪焰")]
        public ParticleSystem muzzleFlash;
        [Tooltip("子弹类型")]
        public GameObject bullet;
        [Tooltip("弹壳抛出位置")]
        public Transform casingEjectionPoint;
    }

    // 定义10把武器的效果
    public WeaponEffectConfig[] weaponEffectConfigs = new WeaponEffectConfig[10]
    {
        new WeaponEffectConfig
        {
            gunType = GunType.Glock
        },
        new WeaponEffectConfig
        {
            gunType = GunType.DesertEagle
        },
        new WeaponEffectConfig
        {
            gunType = GunType.Tec9
        },
        new WeaponEffectConfig
        {
            gunType = GunType.AK47
        },
        new WeaponEffectConfig
        {
            gunType = GunType.M4A4
        },
        new WeaponEffectConfig
        {
            gunType = GunType.XM1014
        },
        new WeaponEffectConfig
        {
            gunType = GunType.Vector
        },
        new WeaponEffectConfig
        {
            gunType = GunType.Uzi
        },
        new WeaponEffectConfig
        {
            gunType = GunType.P90
        },
        new WeaponEffectConfig
        {
            gunType = GunType.MP5
        }
    };

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        HandleEffects();
    }

    void Start()
    {
        weaponController = GetComponent<WeaponController>();
    }

    void Update()
    {

    }

    // 方法：武器效果初始化
    private void HandleEffects()
    {
        // 获取当前武器在枚举中的索引
        string gunName = gameObject.name;
         GunType currentGunType = (GunType)System.Enum.Parse(typeof(GunType), gunName);
        int currentWeaponIndex = (int)currentGunType;
        // 根据武器索引给对应的属性赋值
        currentWeaponConfig = weaponEffectConfigs[currentWeaponIndex];
        shootSound = currentWeaponConfig.shootSound;
        reloadSound_LeftAmmo = currentWeaponConfig.reloadSound_LeftAmmo;
        reloadSound_OutOfAmmo = currentWeaponConfig.reloadSound_OutOfAmmo;
        bulletSpawnPoint = currentWeaponConfig.bulletSpawnPoint;
        casingEjectionPoint = currentWeaponConfig.casingEjectionPoint;
        muzzleFlash = currentWeaponConfig.muzzleFlash;
        bullet = currentWeaponConfig.bullet;
    }

    // 方法：射击特效
    public void ShootEffects()
    {
        // 射击音效
        audioSource.clip = shootSound;
        audioSource.Play();
        // 枪焰特效
        muzzleFlash.Emit(1);
        // 子弹实例化
        // FPS游戏中由于枪口位置和相机位置不一致，所以子弹不能直接朝相机的前方发射
        // 做法：先从相机中心发射一条射线找到准星瞄准点再让子弹从枪口朝那个点飞
        // 1.从相机中心发射射线，找到准星瞄准的世界坐标
        Ray ray = weaponCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint;
        if(Physics.Raycast(ray,out RaycastHit hit,100f))
        {
            targetPoint = hit.point;    // 射线命中物体，用命中点
        }
        else
        {
            targetPoint = ray.GetPoint(100f);  // 没命中，用射线远端的点
        }
        // 2.从枪口到瞄准点的方向
        Vector3 shootDirection = (targetPoint - bulletSpawnPoint.position).normalized;
        // 3.实例化子弹并朝向目标方向
        Quaternion bulletRotation = Quaternion.LookRotation(shootDirection) * Quaternion.Euler(90f, 0f, 0f);    // 调整子弹为横向
        GameObject bulletInstance = Instantiate(bullet, bulletSpawnPoint.position, bulletRotation);
        // 4.赋予子弹速度
        bulletInstance.GetComponent<Rigidbody>().velocity = shootDirection * bulletSpeed;
    }

    // 方法：换弹特效
    public void ReloadEffects()
    {
        if(weaponController.currentMagazineAmmo != 0)
        {
            audioSource.clip = reloadSound_LeftAmmo;
            audioSource.Play();
        }
        else
        {
            audioSource.clip = reloadSound_OutOfAmmo;
            audioSource.Play();
        }
    }
}
