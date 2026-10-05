using UnityEngine;
using UnityEngine.UI;

public class CrosshairHUD : MonoBehaviour
{
    [Header("准星部件")]
    [Tooltip("准星上方横线")]
    public Image topLine;

    [Tooltip("准星下方横线")]
    public Image bottomLine;

    [Tooltip("准星左方横线")]
    public Image leftLine;

    [Tooltip("准星右方横线")]
    public Image rightLine;

    [Tooltip("准星中心点（设置面板可开关）")]
    public Image dot;

    [Header("换弹图标")]
    [Tooltip("换弹时显示的图标")]
    public Image reloadIcon;

    [Tooltip("换弹图标旋转速度 (度/秒)")]
    [Range(0f, 360f)]
    public float reloadIconRotationSpeed = 360f;

    [Header("弹匣空状态")]
    [Tooltip("弹匣空时显示的图标")]
    public Image outOfAmmoIcon;

    [Tooltip("弹匣空时闪烁频率")]
    [Range(1f, 10f)]
    public float outOfAmmoBlinkSpeed = 3f;

    [Header("扩散设置")]
    [Tooltip("射击时最大扩散距离")]
    [Range(10f, 200f)]
    public float maxSpread = 80f;

    [Tooltip("每次射击增加的扩散量")]
    [Range(5f, 50f)]
    public float spreadPerShot = 25f;

    [Tooltip("恢复速度 (值越大越快)")]
    [Range(1f, 20f)]
    public float recoverSpeed = 8f;

    // 准星外观（颜色/线长/线宽/间隙/透明度/各部件开关）全部来自
    // SettingsManager 的全局准星配置，由设置面板控制并持久化，
    // 本组件不再持有这些字段。外观变化靠配置版本号检测，
    // 避免每帧无差别刷新 Image 造成界面反复重建
    private int appliedCrosshairVersion = -1;

    private WeaponController currentWeapon;
    private Animator currentWeaponAnimator;
    private float currentSpread;
    private bool isReloading;
    private bool isOutOfAmmo;
    private float reloadIconRotation;
    private float outOfAmmoBlinkTime;
    private float outOfAmmoBlinkAlpha = 1f;

    private void Start()
    {
        currentSpread = SettingsManager.Crosshair.gap;
        isReloading = false;
        isOutOfAmmo = false;
        reloadIconRotation = 0f;
        outOfAmmoBlinkTime = 0f;

        ApplyCrosshairAppearance();
        UpdateCrosshairPositions();

        if (reloadIcon != null)
        {
            reloadIcon.gameObject.SetActive(false);
        }

        if (outOfAmmoIcon != null)
        {
            outOfAmmoIcon.gameObject.SetActive(false);
        }

        FindActiveWeapon();
    }

    private void Update()
    {
        if (currentWeapon == null || !currentWeapon.gameObject.activeSelf)
        {
            FindActiveWeapon();
        }

        // 设置面板改动了准星外观：重新应用
        if (appliedCrosshairVersion != SettingsManager.CrosshairVersion)
        {
            ApplyCrosshairAppearance();
        }

        UpdateReloadState();
        UpdateOutOfAmmoState();
        UpdateSpread();
        UpdateCrosshairPositions();
        UpdateReloadIcon();
        UpdateOutOfAmmoIcon();
    }

    private void FindActiveWeapon()
    {
        WeaponController[] weapons = FindObjectsOfType<WeaponController>();

        foreach (var w in weapons)
        {
            if (w.gameObject.activeSelf)
            {
                currentWeapon = w;
                currentWeaponAnimator = w.GetComponent<Animator>();
                break;
            }
        }
    }

    private void UpdateReloadState()
    {
        if (currentWeapon == null || currentWeaponAnimator == null)
        {
            isReloading = false;
            return;
        }

        int layerIndex = (int)currentWeapon.CurrentGunData.gunType + 1;
        AnimatorStateInfo state = currentWeaponAnimator.GetCurrentAnimatorStateInfo(layerIndex);

        // 检测换弹状态
        bool isReloadOut = state.IsName("ReloadOutOfAmmo");
        bool isReloadLeft = state.IsName("ReloadLeftAmmo");

        // 更新换弹状态
        isReloading = isReloadOut || isReloadLeft;
    }

    private void UpdateOutOfAmmoState()
    {
        // 只有在有武器且武器未换弹时才检查弹匣状态
        if (currentWeapon != null && !isReloading)
        {
            isOutOfAmmo = currentWeapon.currentMagazineAmmo <= 0;
        }
        else
        {
            isOutOfAmmo = false;
        }
    }

    private void UpdateSpread()
    {
        bool isFiring = IsWeaponFiring();

        // 动态准星关闭时四条线始终停在静态间隙位置
        if (!SettingsManager.Crosshair.dynamicCrosshairEnabled)
        {
            currentSpread = SettingsManager.Crosshair.gap;
            return;
        }

        if (isFiring)
        {
            // 射击中：扩散增加
            currentSpread = Mathf.Min(
                currentSpread + spreadPerShot * Time.deltaTime,
                maxSpread
            );
        }
        else
        {
            // 非射击状态：向设置的静态间隙恢复
            currentSpread = Mathf.Lerp(
                currentSpread,
                SettingsManager.Crosshair.gap,
                recoverSpeed * Time.deltaTime
            );
        }
    }

    private bool IsWeaponFiring()
    {
        // 条件1：必须按住鼠标左键
        if (!Input.GetKey(KeyCode.Mouse0))
            return false;

        // 条件2：动画必须是 Fire 状态
        if (currentWeapon == null || currentWeaponAnimator == null)
            return false;

        int layerIndex = (int)currentWeapon.CurrentGunData.gunType + 1;
        AnimatorStateInfo state = currentWeaponAnimator.GetCurrentAnimatorStateInfo(layerIndex);

        return state.IsName("Fire");
    }

    private void UpdateCrosshairPositions()
    {
        if (topLine == null || bottomLine == null ||
            leftLine == null || rightLine == null)
            return;

        CrosshairConfig config = SettingsManager.Crosshair;

        // 根据换弹状态和弹匣空状态控制准星可见性；
        // 四条线始终全部显示，只有中心点受设置开关控制
        bool shouldShowCrosshair = !isReloading && !isOutOfAmmo;

        if (topLine != null)
            topLine.enabled = shouldShowCrosshair;
        if (bottomLine != null)
            bottomLine.enabled = shouldShowCrosshair;
        if (leftLine != null)
            leftLine.enabled = shouldShowCrosshair;
        if (rightLine != null)
            rightLine.enabled = shouldShowCrosshair;

        if (dot != null)
            dot.enabled = shouldShowCrosshair && config.dotEnabled;

        if (!shouldShowCrosshair)
        {
            return;
        }

        float halfLength = config.lineLength * 0.5f;

        topLine.rectTransform.anchoredPosition =
            Vector2.up * (currentSpread + halfLength);

        bottomLine.rectTransform.anchoredPosition =
            Vector2.down * (currentSpread + halfLength);

        leftLine.rectTransform.anchoredPosition =
            Vector2.left * (currentSpread + halfLength);

        rightLine.rectTransform.anchoredPosition =
            Vector2.right * (currentSpread + halfLength);

        if (dot != null && config.dotEnabled)
        {
            dot.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    private void UpdateReloadIcon()
    {
        if (reloadIcon == null)
            return;

        // 更新换弹图标可见性
        reloadIcon.gameObject.SetActive(isReloading);

        // 旋转图标（仅在显示时更新）
        if (isReloading)
        {
            reloadIconRotation += reloadIconRotationSpeed * Time.deltaTime;
            // 通过使用负角度实现顺时针旋转
            reloadIcon.transform.rotation =
                Quaternion.Euler(0, 0, -reloadIconRotation);
        }
    }

    private void UpdateOutOfAmmoIcon()
    {
        if (outOfAmmoIcon == null)
            return;

        // 更新弹匣空图标可见性
        outOfAmmoIcon.gameObject.SetActive(isOutOfAmmo);

        // 闪烁逻辑
        if (isOutOfAmmo)
        {
            outOfAmmoBlinkTime += Time.deltaTime;
            outOfAmmoBlinkAlpha = Mathf.PingPong(
                outOfAmmoBlinkTime * outOfAmmoBlinkSpeed,
                1f
            );

            // 设置图标透明度
            Color c = outOfAmmoIcon.color;
            c.a = outOfAmmoBlinkAlpha;
            outOfAmmoIcon.color = c;
        }
    }

    private void ApplyCrosshairAppearance()
    {
        appliedCrosshairVersion = SettingsManager.CrosshairVersion;

        CrosshairConfig config = SettingsManager.Crosshair;

        // 透明度并入颜色，四条线与中心点统一应用
        Color color = config.color;
        color.a = config.opacity;

        if (topLine != null)
        {
            topLine.color = color;
            topLine.rectTransform.sizeDelta =
                new Vector2(config.lineWidth, config.lineLength);
        }

        if (bottomLine != null)
        {
            bottomLine.color = color;
            bottomLine.rectTransform.sizeDelta =
                new Vector2(config.lineWidth, config.lineLength);
        }

        if (leftLine != null)
        {
            leftLine.color = color;
            leftLine.rectTransform.sizeDelta =
                new Vector2(config.lineLength, config.lineWidth);
        }

        if (rightLine != null)
        {
            rightLine.color = color;
            rightLine.rectTransform.sizeDelta =
                new Vector2(config.lineLength, config.lineWidth);
        }

        if (dot != null)
        {
            dot.color = color;
            // 中心点大小与线条粗细一致
            dot.rectTransform.sizeDelta =
                Vector2.one * config.lineWidth;
        }
    }
}