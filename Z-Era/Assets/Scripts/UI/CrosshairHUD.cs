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

    [Header("准星外观")]
    [Tooltip("准星颜色")]
    public Color crosshairColor = Color.white;

    [Tooltip("准星线条长度")]
    [Range(5f, 50f)]
    public float lineLength = 20f;

    [Tooltip("准星线条宽度")]
    [Range(1f, 10f)]
    public float lineWidth = 2f;

    [Header("扩散设置")]
    [Tooltip("静态时准星中心到线条的距离")]
    [Range(0f, 50f)]
    public float baseSpread = 5f;

    [Tooltip("射击时最大扩散距离")]
    [Range(10f, 200f)]
    public float maxSpread = 80f;

    [Tooltip("每次射击增加的扩散量")]
    [Range(5f, 50f)]
    public float spreadPerShot = 25f;

    [Tooltip("恢复速度 (值越大越快)")]
    [Range(1f, 20f)]
    public float recoverSpeed = 8f;

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
        currentSpread = baseSpread;
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
            // 非射击状态：立即恢复
            currentSpread = Mathf.Lerp(
                currentSpread,
                baseSpread,
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

        // 根据换弹状态和弹匣空状态控制准星可见性
        bool shouldShowCrosshair = !isReloading && !isOutOfAmmo;

        // 更新准星可见性
        if (topLine != null) topLine.enabled = shouldShowCrosshair;
        if (bottomLine != null) bottomLine.enabled = shouldShowCrosshair;
        if (leftLine != null) leftLine.enabled = shouldShowCrosshair;
        if (rightLine != null) rightLine.enabled = shouldShowCrosshair;

        if (!shouldShowCrosshair)
        {
            return;
        }

        float halfLength = lineLength * 0.5f;

        topLine.rectTransform.anchoredPosition =
            Vector2.up * (currentSpread + halfLength);

        bottomLine.rectTransform.anchoredPosition =
            Vector2.down * (currentSpread + halfLength);

        leftLine.rectTransform.anchoredPosition =
            Vector2.left * (currentSpread + halfLength);

        rightLine.rectTransform.anchoredPosition =
            Vector2.right * (currentSpread + halfLength);
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
        if (topLine != null)
        {
            topLine.color = crosshairColor;
            topLine.rectTransform.sizeDelta = new Vector2(lineWidth, lineLength);
        }

        if (bottomLine != null)
        {
            bottomLine.color = crosshairColor;
            bottomLine.rectTransform.sizeDelta = new Vector2(lineWidth, lineLength);
        }

        if (leftLine != null)
        {
            leftLine.color = crosshairColor;
            leftLine.rectTransform.sizeDelta = new Vector2(lineLength, lineWidth);
        }

        if (rightLine != null)
        {
            rightLine.color = crosshairColor;
            rightLine.rectTransform.sizeDelta = new Vector2(lineLength, lineWidth);
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyCrosshairAppearance();
            UpdateCrosshairPositions();
        }
    }

    public void SetColor(Color color)
    {
        crosshairColor = color;
        ApplyCrosshairAppearance();
    }

    public void SetSize(float length, float width)
    {
        lineLength = length;
        lineWidth = width;
        ApplyCrosshairAppearance();
    }
}