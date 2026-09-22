using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponHUD : MonoBehaviour
{
    [Header("武器 UI")]
    [Tooltip("武器图标 Image 组件")]
    public Image weaponIcon;

    [Tooltip("按 GunType 枚举顺序拖入对应的武器图标 Sprite (共9个)")]
    public Sprite[] weaponSprites;

    [Tooltip("红色填充 Image 组件 (Type: Filled, Fill Method: Vertical, Fill Origin: Top)")]
    public Image weaponRedFill;

    [Header("弹药 UI")]
    [Tooltip("当前弹匣子弹数，右对齐")]
    public TMP_Text magazineAmmoText;

    [Tooltip("当前剩余备弹数，左对齐")]
    public TMP_Text carriedAmmoText;

    [Tooltip("换弹时显示旧弹匣子弹数的文本，右对齐")]
    public TMP_Text magazineOutgoingText;

    [Tooltip("换弹时显示旧剩余备弹数的文本，左对齐")]
    public TMP_Text carriedOutgoingText;

    [Header("空弹特效设置")]
    [Tooltip("空弹闪烁频率")]
    public float blinkSpeed = 4f;

    [Header("换弹文字动画")]
    [Tooltip("数字上下移动的距离")]
    public float ammoReloadMoveDistance = 18f;

    [Tooltip("数字动画速度。1为原速，越大越快")]
    [Min(0.05f)]
    public float ammoReloadAnimationSpeed = 1f;

    [Tooltip("换弹动画播放完毕后，数字切换动画的持续时间")]
    [Min(0.01f)]
    public float ammoSwapAnimationDuration = 0.2f;

    private WeaponController currentWeapon;
    private Animator currentWeaponAnimator;

    private static readonly Color32 ZeroAmmoColor =
        new Color32(0xFF, 0x33, 0x33, 0xFF);

    private Color magazineNormalColor = Color.white;
    private Color carriedNormalColor = Color.white;

    private Vector2 magazineBasePosition;
    private Vector2 carriedBasePosition;

    private int lastMagazineAmmo;
    private int lastCarriedAmmo;
    private int lastMagazineSize;

    private bool wasReloading;
    private string activeReloadStateName;
    private float reloadStartFillAmount = 1f;
    private float reloadEndFillAmount = 0f;
    private float reloadCurrentAlpha = 1f;

    private int reloadOldMagazineAmmo;
    private int reloadOldCarriedAmmo;
    private int reloadNewMagazineAmmo;
    private int reloadNewCarriedAmmo;

    private bool pendingAmmoTextSwap;

    private bool isAmmoTextAnimationActive;
    private float ammoTextAnimationProgress;
    private float ammoTextAnimationDuration = 1f;

    private void Awake()
    {
        if (magazineAmmoText != null)
        {
            magazineBasePosition =
                magazineAmmoText.rectTransform.anchoredPosition;

            magazineNormalColor = magazineAmmoText.color;
            magazineAmmoText.alpha = 1f;
        }

        if (carriedAmmoText != null)
        {
            carriedBasePosition =
                carriedAmmoText.rectTransform.anchoredPosition;

            carriedNormalColor = carriedAmmoText.color;
            carriedAmmoText.alpha = 1f;
        }

        if (magazineOutgoingText != null)
        {
            magazineOutgoingText.raycastTarget = false;
            magazineOutgoingText.alpha = 1f;
            magazineOutgoingText.gameObject.SetActive(false);
        }

        if (carriedOutgoingText != null)
        {
            carriedOutgoingText.raycastTarget = false;
            carriedOutgoingText.alpha = 1f;
            carriedOutgoingText.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        FindActiveWeapon();
    }

    private void Update()
    {
        if (currentWeapon == null ||
            !currentWeapon.gameObject.activeSelf)
        {
            FindActiveWeapon();
        }

        if (currentWeapon != null)
        {
            UpdateAmmoVisuals();
            UpdateAmmoUI();
        }
    }

    private void FindActiveWeapon()
    {
        WeaponController[] weapons =
            FindObjectsOfType<WeaponController>();

        foreach (var w in weapons)
        {
            if (w.gameObject.activeSelf)
            {
                CancelAmmoTextAnimation();

                currentWeapon = w;
                currentWeaponAnimator = w.GetComponent<Animator>();

                lastMagazineAmmo =
                    currentWeapon.currentMagazineAmmo;

                lastCarriedAmmo =
                    currentWeapon.currentCarriedAmmo;

                lastMagazineSize =
                    currentWeapon.CurrentGunData.magezineSize;

                wasReloading = false;
                activeReloadStateName = null;
                reloadStartFillAmount = 1f;
                reloadEndFillAmount = 0f;
                reloadCurrentAlpha = 1f;
                pendingAmmoTextSwap = false;

                UpdateAmmoUI();
                UpdateWeaponIcon();

                if (weaponRedFill != null)
                {
                    weaponRedFill.fillAmount = 0f;
                    weaponRedFill.enabled = false;
                }

                break;
            }
        }
    }

    private void UpdateAmmoUI()
    {
        if (currentWeapon == null)
        {
            return;
        }

        if (wasReloading ||
            pendingAmmoTextSwap ||
            isAmmoTextAnimationActive)
        {
            return;
        }

        int mag = currentWeapon.currentMagazineAmmo;
        int carried = currentWeapon.currentCarriedAmmo;

        SetAmmoTextValue(
            magazineAmmoText,
            mag,
            magazineNormalColor
        );

        SetAmmoTextValue(
            carriedAmmoText,
            carried,
            carriedNormalColor
        );

        if (magazineAmmoText != null)
        {
            magazineAmmoText.alpha = 1f;
            magazineAmmoText.rectTransform.anchoredPosition =
                magazineBasePosition;
        }

        if (carriedAmmoText != null)
        {
            carriedAmmoText.alpha = 1f;
            carriedAmmoText.rectTransform.anchoredPosition =
                carriedBasePosition;
        }
    }

    private void SetAmmoTextValue(
        TMP_Text targetText,
        int value,
        Color normalColor
    )
    {
        if (targetText == null)
        {
            return;
        }

        targetText.text = value.ToString();

        targetText.color = value == 0
            ? ZeroAmmoColor
            : normalColor;
    }

    private void UpdateAmmoVisuals()
    {
        if (currentWeapon == null)
        {
            return;
        }

        bool isReloading = false;
        string reloadStateName = null;
        float reloadStateProgress = 0f;

        if (currentWeaponAnimator != null)
        {
            int layerIndex =
                (int)currentWeapon.CurrentGunData.gunType + 1;

            AnimatorStateInfo state =
                currentWeaponAnimator
                    .GetCurrentAnimatorStateInfo(layerIndex);

            if (state.IsName("ReloadOutOfAmmo") ||
                state.IsName("ReloadLeftAmmo"))
            {
                isReloading = true;

                reloadStateName =
                    state.IsName("ReloadOutOfAmmo")
                        ? "ReloadOutOfAmmo"
                        : "ReloadLeftAmmo";

                reloadStateProgress =
                    Mathf.Clamp01(state.normalizedTime);
            }
        }

        if (isReloading)
        {
            bool startedNewReload =
                !wasReloading ||
                activeReloadStateName != reloadStateName;

            if (startedNewReload)
            {
                activeReloadStateName = reloadStateName;

                CancelAmmoTextAnimation();

                reloadCurrentAlpha =
                    Mathf.Clamp01(
                        weaponRedFill != null
                            ? weaponRedFill.color.a
                            : 1f
                    );

                reloadOldMagazineAmmo = lastMagazineAmmo;
                reloadOldCarriedAmmo = lastCarriedAmmo;

                int magazineSize =
                    lastMagazineSize > 0
                        ? lastMagazineSize
                        : currentWeapon.CurrentGunData.magezineSize;

                int neededAmmo = Mathf.Max(
                    0,
                    magazineSize - reloadOldMagazineAmmo
                );

                int movedAmmo = Mathf.Min(
                    neededAmmo,
                    Mathf.Max(0, reloadOldCarriedAmmo)
                );

                reloadNewMagazineAmmo =
                    reloadOldMagazineAmmo + movedAmmo;

                reloadNewCarriedAmmo =
                    reloadOldCarriedAmmo - movedAmmo;

                if (reloadStateName == "ReloadOutOfAmmo")
                {
                    reloadStartFillAmount = 1f;
                }
                else
                {
                    float previousMagRatio =
                        magazineSize > 0
                            ? (float)reloadOldMagazineAmmo
                              / magazineSize
                            : 0f;

                    reloadStartFillAmount =
                        Mathf.Clamp01(1f - previousMagRatio);
                }

                // 计算换弹后的最终红条填充量
                reloadEndFillAmount = magazineSize > 0
                    ? Mathf.Clamp01(1f - (float)reloadNewMagazineAmmo / magazineSize)
                    : 0f;

                SetAmmoTextValue(
                    magazineAmmoText,
                    reloadOldMagazineAmmo,
                    magazineNormalColor
                );

                SetAmmoTextValue(
                    carriedAmmoText,
                    reloadOldCarriedAmmo,
                    carriedNormalColor
                );

                if (magazineAmmoText != null)
                {
                    magazineAmmoText.alpha = 1f;
                    magazineAmmoText.rectTransform.anchoredPosition =
                        magazineBasePosition;
                }

                if (carriedAmmoText != null)
                {
                    carriedAmmoText.alpha = 1f;
                    carriedAmmoText.rectTransform.anchoredPosition =
                        carriedBasePosition;
                }

                pendingAmmoTextSwap = true;
            }

            if (weaponRedFill != null)
            {
                reloadCurrentAlpha = Mathf.MoveTowards(
                    reloadCurrentAlpha,
                    1f,
                    Mathf.Max(0f, blinkSpeed)
                    * Time.deltaTime
                );

                // 红条从起始值平滑过渡到最终值
                weaponRedFill.fillAmount = Mathf.Lerp(
                    reloadStartFillAmount,
                    reloadEndFillAmount,
                    reloadStateProgress
                );

                weaponRedFill.enabled = true;

                Color c = weaponRedFill.color;
                c.a = reloadCurrentAlpha;
                weaponRedFill.color = c;
            }

            if (pendingAmmoTextSwap &&
                reloadStateProgress >= 1f)
            {
                StartPendingAmmoTextSwap();
            }

            wasReloading = true;
        }
        else
        {
            wasReloading = false;
            activeReloadStateName = null;
            reloadStartFillAmount = 1f;
            reloadEndFillAmount = 0f;
            reloadCurrentAlpha = 1f;

            int mag = currentWeapon.currentMagazineAmmo;
            int carried = currentWeapon.currentCarriedAmmo;
            int maxMag =
                currentWeapon.CurrentGunData.magezineSize;

            lastMagazineAmmo = mag;
            lastCarriedAmmo = carried;
            lastMagazineSize = maxMag;

            if (weaponRedFill != null)
            {
                float magRatio = maxMag > 0
                    ? (float)mag / maxMag
                    : 0f;

                if (mag == 0)
                {
                    weaponRedFill.fillAmount = 1f;
                    weaponRedFill.enabled = true;

                    float alpha = Mathf.PingPong(
                        Time.time * blinkSpeed,
                        1f
                    );

                    Color c = weaponRedFill.color;
                    c.a = alpha;
                    weaponRedFill.color = c;
                }
                else
                {
                    weaponRedFill.fillAmount =
                        1f - magRatio;

                    weaponRedFill.enabled = true;

                    Color c = weaponRedFill.color;
                    c.a = 1f;
                    weaponRedFill.color = c;
                }
            }

            if (pendingAmmoTextSwap)
            {
                StartPendingAmmoTextSwap();
            }
        }

        AdvanceAmmoTextAnimation(Time.deltaTime);
    }

    private void StartPendingAmmoTextSwap()
    {
        if (!pendingAmmoTextSwap)
        {
            return;
        }

        pendingAmmoTextSwap = false;

        BeginAmmoTextAnimation(
            0f,
            ammoSwapAnimationDuration
        );
    }

    private void BeginAmmoTextAnimation(
        float initialProgress,
        float animationDuration
    )
    {
        isAmmoTextAnimationActive = true;

        ammoTextAnimationProgress =
            Mathf.Clamp01(initialProgress);

        ammoTextAnimationDuration =
            Mathf.Max(0.01f, animationDuration);

        if (magazineOutgoingText != null)
        {
            magazineOutgoingText.gameObject.SetActive(true);

            SetAmmoTextValue(
                magazineOutgoingText,
                reloadOldMagazineAmmo,
                magazineNormalColor
            );

            magazineOutgoingText.alpha = 1f;
            magazineOutgoingText.rectTransform.anchoredPosition =
                magazineBasePosition;
        }

        if (carriedOutgoingText != null)
        {
            carriedOutgoingText.gameObject.SetActive(true);

            SetAmmoTextValue(
                carriedOutgoingText,
                reloadOldCarriedAmmo,
                carriedNormalColor
            );

            carriedOutgoingText.alpha = 1f;
            carriedOutgoingText.rectTransform.anchoredPosition =
                carriedBasePosition;
        }

        if (magazineAmmoText != null)
        {
            SetAmmoTextValue(
                magazineAmmoText,
                reloadNewMagazineAmmo,
                magazineNormalColor
            );

            magazineAmmoText.alpha = 0f;
            magazineAmmoText.rectTransform.anchoredPosition =
                magazineBasePosition
                + Vector2.up * ammoReloadMoveDistance;
        }

        if (carriedAmmoText != null)
        {
            SetAmmoTextValue(
                carriedAmmoText,
                reloadNewCarriedAmmo,
                carriedNormalColor
            );

            carriedAmmoText.alpha = 0f;
            carriedAmmoText.rectTransform.anchoredPosition =
                carriedBasePosition
                + Vector2.down * ammoReloadMoveDistance;
        }

        ApplyReloadAmmoTextAnimation(
            ammoTextAnimationProgress
        );
    }

    private void AdvanceAmmoTextAnimation(float deltaTime)
    {
        if (!isAmmoTextAnimationActive)
        {
            return;
        }

        float speed =
            Mathf.Max(0.05f, ammoReloadAnimationSpeed);

        ammoTextAnimationProgress +=
            deltaTime
            / ammoTextAnimationDuration
            * speed;

        ammoTextAnimationProgress =
            Mathf.Clamp01(ammoTextAnimationProgress);

        ApplyReloadAmmoTextAnimation(
            ammoTextAnimationProgress
        );

        if (ammoTextAnimationProgress >= 1f)
        {
            FinishAmmoTextAnimation();
        }
    }

    private void ApplyReloadAmmoTextAnimation(float progress)
    {
        float t = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(progress)
        );

        float distance =
            Mathf.Max(0f, ammoReloadMoveDistance);

        if (magazineOutgoingText != null)
        {
            magazineOutgoingText.rectTransform.anchoredPosition =
                magazineBasePosition
                + Vector2.down * distance * t;

            magazineOutgoingText.alpha = 1f - t;
        }

        if (carriedOutgoingText != null)
        {
            carriedOutgoingText.rectTransform.anchoredPosition =
                carriedBasePosition
                + Vector2.up * distance * t;

            carriedOutgoingText.alpha = 1f - t;
        }

        if (magazineAmmoText != null)
        {
            magazineAmmoText.rectTransform.anchoredPosition =
                magazineBasePosition
                + Vector2.up * distance * (1f - t);

            magazineAmmoText.alpha = t;
        }

        if (carriedAmmoText != null)
        {
            carriedAmmoText.rectTransform.anchoredPosition =
                carriedBasePosition
                + Vector2.down * distance * (1f - t);

            carriedAmmoText.alpha = t;
        }
    }

    private void FinishAmmoTextAnimation()
    {
        isAmmoTextAnimationActive = false;
        ammoTextAnimationProgress = 1f;

        if (magazineOutgoingText != null)
        {
            magazineOutgoingText.alpha = 0f;
            magazineOutgoingText.gameObject.SetActive(false);
        }

        if (carriedOutgoingText != null)
        {
            carriedOutgoingText.alpha = 0f;
            carriedOutgoingText.gameObject.SetActive(false);
        }

        if (magazineAmmoText != null)
        {
            magazineAmmoText.rectTransform.anchoredPosition =
                magazineBasePosition;

            magazineAmmoText.alpha = 1f;

            SetAmmoTextValue(
                magazineAmmoText,
                reloadNewMagazineAmmo,
                magazineNormalColor
            );
        }

        if (carriedAmmoText != null)
        {
            carriedAmmoText.rectTransform.anchoredPosition =
                carriedBasePosition;

            carriedAmmoText.alpha = 1f;

            SetAmmoTextValue(
                carriedAmmoText,
                reloadNewCarriedAmmo,
                carriedNormalColor
            );
        }
    }

    private void CancelAmmoTextAnimation()
    {
        isAmmoTextAnimationActive = false;
        ammoTextAnimationProgress = 0f;

        if (magazineOutgoingText != null)
        {
            magazineOutgoingText.alpha = 0f;
            magazineOutgoingText.gameObject.SetActive(false);
        }

        if (carriedOutgoingText != null)
        {
            carriedOutgoingText.alpha = 0f;
            carriedOutgoingText.gameObject.SetActive(false);
        }

        if (magazineAmmoText != null)
        {
            magazineAmmoText.rectTransform.anchoredPosition =
                magazineBasePosition;

            magazineAmmoText.alpha = 1f;
        }

        if (carriedAmmoText != null)
        {
            carriedAmmoText.rectTransform.anchoredPosition =
                carriedBasePosition;

            carriedAmmoText.alpha = 1f;
        }
    }

    private void UpdateWeaponIcon()
    {
        if (weaponIcon == null || currentWeapon == null)
        {
            return;
        }

        if (weaponSprites == null ||
            weaponSprites.Length == 0)
        {
            return;
        }

        int index =
            (int)currentWeapon.CurrentGunData.gunType;

        if (index >= 0 &&
            index < weaponSprites.Length &&
            weaponSprites[index] != null)
        {
            Sprite targetSprite = weaponSprites[index];

            weaponIcon.sprite = targetSprite;

            if (weaponRedFill != null)
            {
                weaponRedFill.sprite = targetSprite;
            }
        }
    }
}