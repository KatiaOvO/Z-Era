using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponEffects : MonoBehaviour
{
    #region 武器效果
    [Tooltip("枪声音效")]
    private AudioClip shootSound;
    [Tooltip("换弹音效")]
    public AudioClip reloadSound;
    [Tooltip("子弹实例化位置（枪口）")]
    public Transform bulletSpawnPoint;
    [Tooltip("枪焰")]
    public ParticleSystem muzzleFlash;
    #endregion

    #region 当前武器
    [Tooltip("当前武器索引")]
    #endregion

    [System.Serializable]
    public class WeaponEffectConfig
    {
        [Tooltip("武器类型")]
        public GunType gunType;
        [Tooltip("枪声音效")]
        public AudioClip shootSound;
        [Tooltip("换弹音效")]
        public AudioClip reloadSound;
        [Tooltip("子弹实例化位置（枪口）")]
        public Transform bulletSpawnPoint;
        [Tooltip("枪焰")]
        public ParticleSystem muzzleFlash;
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

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void ShootEffects()
    {
        // 获取当前武器在枚举中的索引
        string gunName = gameObject.name;
         GunType currentGunType = (GunType)System.Enum.Parse(typeof(GunType), gunName);
        int currentWeaponIndex = (int)currentGunType;
        // 
        WeaponEffectConfig currentWeaponConfig = weaponEffectConfigs[currentWeaponIndex];
    }
}
