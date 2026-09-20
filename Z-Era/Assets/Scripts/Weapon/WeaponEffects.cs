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

    [Tooltip("子弹预制体")]
    private GameObject bullet;

    [Tooltip("弹壳抛出位置")]
    private Transform casingEjectionPoint;

    [Tooltip("弹壳预制体")]
    private GameObject casing;

    [Tooltip("子弹速度")]
    public float bulletSpeed = 500f;

    #endregion

    #region 当前武器

    private WeaponEffectConfig currentWeaponConfig;

    #endregion

    #region 组件

    private AudioSource audioSource;

    public Camera weaponCamera;

    #endregion

    #region 音频

    public AudioClip takeOutWeaponSound;
    public AudioClip holsterWeaponSound;

    #endregion

    #region 玩法噪声

    [Header("玩法噪声")]
    [Tooltip("玩家身上的玩法噪声发射器，留空时自动向父物体查找")]
    [SerializeField]
    private NoiseEmitter noiseEmitter;

    [Tooltip("开枪时，Zombie 能听到的最大距离")]
    [SerializeField, Min(0f)]
    private float shootNoiseRadius = 35f;

    [Tooltip("每次开枪增加的怒气值")]
    [SerializeField, Min(0f)]
    private float shootAngerValue = 30f;

    [Tooltip("换弹时，Zombie 能听到的最大距离")]
    [SerializeField, Min(0f)]
    private float reloadNoiseRadius = 12f;

    [Tooltip("每次换弹增加的怒气值")]
    [SerializeField, Min(0f)]
    private float reloadAngerValue = 10f;

    #endregion

    #region 引用

    private WeaponController weaponController;
    private CameraRecoil cameraRecoil;
    private WeaponSpread weaponSpread;

    #endregion

    #region 对象池

    [SerializeField]
    private BulletPool bulletPool;

    #endregion

    [System.Serializable]
    public class WeaponEffectConfig
    {
        public GunType gunType;
        public AudioClip shootSound;
        public AudioClip reloadSound_LeftAmmo;
        public AudioClip reloadSound_OutOfAmmo;
        public Transform bulletSpawnPoint;
        public ParticleSystem muzzleFlash;
        public GameObject bullet;
        public Transform casingEjectionPoint;
        public GameObject casing;
    }

    public WeaponEffectConfig[] weaponEffectConfigs =
        new WeaponEffectConfig[9]
    {
        new WeaponEffectConfig { gunType = GunType.Glock },
        new WeaponEffectConfig { gunType = GunType.DesertEagle },
        new WeaponEffectConfig { gunType = GunType.Tec9 },
        new WeaponEffectConfig { gunType = GunType.AK47 },
        new WeaponEffectConfig { gunType = GunType.M4A4 },
        new WeaponEffectConfig { gunType = GunType.Vector },
        new WeaponEffectConfig { gunType = GunType.Uzi },
        new WeaponEffectConfig { gunType = GunType.P90 },
        new WeaponEffectConfig { gunType = GunType.MP5 }
    };

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        HandleEffects();
    }

    void Start()
    {
        weaponController = GetComponent<WeaponController>();
        cameraRecoil = GetComponentInParent<CameraRecoil>();
        weaponSpread = GetComponent<WeaponSpread>();

        if (noiseEmitter == null)
        {
            noiseEmitter = GetComponentInParent<NoiseEmitter>();
        }
    }

    void Update()
    {
        if (weaponController == null || cameraRecoil == null)
        {
            return;
        }

        bool isFiring =
            weaponController.CurrentGunData.fireMode == FireMode.FullAuto
            && Input.GetKey(KeyCode.Mouse0)
            && weaponController.currentMagazineAmmo > 0;

        cameraRecoil.SetFiring(isFiring);
    }

    private void HandleEffects()
    {
        string gunName = gameObject.name;

        GunType currentGunType =
            (GunType)System.Enum.Parse(typeof(GunType), gunName);

        int currentWeaponIndex = (int)currentGunType;

        currentWeaponConfig = weaponEffectConfigs[currentWeaponIndex];
        shootSound = currentWeaponConfig.shootSound;
        reloadSound_LeftAmmo = currentWeaponConfig.reloadSound_LeftAmmo;
        reloadSound_OutOfAmmo = currentWeaponConfig.reloadSound_OutOfAmmo;
        bulletSpawnPoint = currentWeaponConfig.bulletSpawnPoint;
        casingEjectionPoint = currentWeaponConfig.casingEjectionPoint;
        casing = currentWeaponConfig.casing;
        muzzleFlash = currentWeaponConfig.muzzleFlash;
        bullet = currentWeaponConfig.bullet;
    }

    public void ShootEffects()
    {
        audioSource.clip = shootSound;
        audioSource.Play();

        muzzleFlash.Emit(1);

        float spreadAngle =
            weaponSpread != null ? weaponSpread.ConsumeSpread() : 0f;

        Vector2 screenCenter = new Vector2(
            Screen.width * 0.5f,
            Screen.height * 0.5f
        );

        float halfFovRad =
            weaponCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;

        float focalLength =
            (Screen.height * 0.5f) / Mathf.Tan(halfFovRad);

        float spreadRadiusPixels =
            Mathf.Tan(spreadAngle * Mathf.Deg2Rad) * focalLength;

        Vector2 randomOffset =
            Random.insideUnitCircle * spreadRadiusPixels;

        Vector2 screenPoint = screenCenter + randomOffset;

        Ray ray = weaponCamera.ScreenPointToRay(
            new Vector3(screenPoint.x, screenPoint.y, 0f)
        );

        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(100f);
        }

        Vector3 shootDirection =
            (targetPoint - bulletSpawnPoint.position).normalized;

        GunData gunData = weaponController.CurrentGunData;

        Quaternion bulletRotation =
            Quaternion.LookRotation(shootDirection)
            * Quaternion.Euler(90f, 0f, 0f);

        BulletHandle bulletInstance = bulletPool.Get(
            bullet,
            bulletSpawnPoint.position,
            bulletRotation
        );

        if (bulletInstance != null)
        {
            bulletInstance.Launch(
                shootDirection * bulletSpeed,
                gunData.damage,
                weaponController.gameObject
            );
        }

        if (noiseEmitter != null)
        {
            noiseEmitter.EmitGunshot(
                bulletSpawnPoint.position,
                shootNoiseRadius,
                shootAngerValue
            );
        }

        cameraRecoil.PlayRecoil(
            gunData.recoilPitch,
            gunData.recoilYaw,
            gunData.recoilReturnTime
        );

        if (casing == null)
        {
            Debug.LogWarning($"{gameObject.name} 没有设置弹壳预制体。");
            return;
        }

        if (casingEjectionPoint == null)
        {
            Debug.LogWarning($"{gameObject.name} 没有设置弹壳抛出位置。");
            return;
        }

        Instantiate(
            casing,
            casingEjectionPoint.position,
            casingEjectionPoint.rotation
        );
    }

    public void ReloadEffects()
    {
        if (weaponController.currentMagazineAmmo != 0)
        {
            audioSource.clip = reloadSound_LeftAmmo;
            audioSource.Play();
        }
        else
        {
            audioSource.clip = reloadSound_OutOfAmmo;
            audioSource.Play();
        }

        if (noiseEmitter != null)
        {
            noiseEmitter.EmitReload(
                transform.position,
                reloadNoiseRadius,
                reloadAngerValue
            );
        }
    }

    public void PlayWeaponActionSound(string action)
    {
        if (action == "takeout")
        {
            audioSource.clip = takeOutWeaponSound;
            audioSource.Play();
        }
        else if (action == "holster")
        {
            audioSource.clip = holsterWeaponSound;
            audioSource.Play();
        }
    }
}