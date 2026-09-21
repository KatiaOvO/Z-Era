using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("武器 UI")]
    [Tooltip("武器图标 Image 组件")]
    public Image weaponIcon;

    [Tooltip("显示弹药的 TextMeshPro 组件")]
    public TMP_Text ammoText;

    [Tooltip("按 GunType 枚举顺序拖入对应的武器图标 Sprite (共9个)")]
    public Sprite[] weaponSprites;

    [Tooltip("红色填充 Image 组件 (Type: Filled, Fill Method: Vertical, Fill Origin: Top)")]
    public Image weaponRedFill;

    [Header("生命 UI")]
    [Tooltip("生命图标 Image 组件")]
    public Image healthIcon;

    [Tooltip("血条填充 Image 组件 (Type需设为Filled, Fill Method为Horizontal, Fill Origin为Left)")]
    public Image healthBarFill;

    [Tooltip("显示当前血量的 TextMeshPro 组件 (建议作为血条填充物体的子物体，锚定在右侧)")]
    public TMP_Text healthText;

    [Header("空弹特效设置")]
    [Tooltip("空弹闪烁频率")]
    public float blinkSpeed = 4f;

    [Header("换弹文字动画")]
    [Tooltip("换弹时数字上下移动的距离")]
    public float ammoReloadMoveDistance = 18f;

    [Tooltip("换弹数字动画速度。1为随换弹动画原速播放，越大越快")]
    [Min(0.05f)]
    public float ammoReloadAnimationSpeed = 1f;

    private PlayerHealth playerHealth;
    private WeaponController currentWeapon;
    private Animator currentWeaponAnimator;

    private const string ZeroAmmoColor = "#FF3333";

    private int lastMagazineAmmo;
    private int lastCarriedAmmo;
    private int lastMagazineSize;

    private bool wasReloading;
    private string activeReloadStateName;
    private float reloadStartFillAmount = 1f;
    private float reloadCurrentAlpha = 1f;

    private int reloadOldMagazineAmmo;
    private int reloadOldCarriedAmmo;
    private int reloadNewMagazineAmmo;
    private int reloadNewCarriedAmmo;

    private TMP_Text reloadOutgoingAmmoText;

    // 换弹数字动画使用独立进度，
    // 因此可以在 Inspector 中单独控制速度。
    private bool isAmmoTextAnimationActive;
    private float ammoTextAnimationProgress;
    private float ammoTextAnimationDuration = 1f;

    private void Start()
    {
        playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.HealthChanged += OnHealthChanged;
            OnHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        if (ammoText != null)
        {
            ammoText.richText = true;
        }

        EnsureReloadOutgoingAmmoText();
        FindActiveWeapon();
    }

    private void Update()
    {
        if (currentWeapon == null || !currentWeapon.gameObject.activeSelf)
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
        WeaponController[] weapons = FindObjectsOfType<WeaponController>();

        foreach (var w in weapons)
        {
            if (w.gameObject.activeSelf)
            {
                currentWeapon = w;
                currentWeaponAnimator = w.GetComponent<Animator>();

                lastMagazineAmmo = currentWeapon.currentMagazineAmmo;
                lastCarriedAmmo = currentWeapon.currentCarriedAmmo;
                lastMagazineSize = currentWeapon.CurrentGunData.magezineSize;

                wasReloading = false;
                activeReloadStateName = null;
                reloadStartFillAmount = 1f;
                reloadCurrentAlpha = 1f;

                isAmmoTextAnimationActive = false;
                ammoTextAnimationProgress = 0f;
                ammoTextAnimationDuration = 1f;

                if (reloadOutgoingAmmoText != null)
                {
                    reloadOutgoingAmmoText.text = string.Empty;
                    reloadOutgoingAmmoText.gameObject.SetActive(false);
                }

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

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        if (healthBarFill != null)
        {
            float fillAmount = maxHealth > 0f
                ? currentHealth / maxHealth
                : 0f;

            healthBarFill.fillAmount = fillAmount;
        }

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(currentHealth).ToString();
        }
    }

    private void UpdateAmmoUI()
    {
        if (ammoText == null || currentWeapon == null)
        {
            return;
        }

        // 文字动画播放期间，不允许普通文本刷新覆盖动画。
        if (isAmmoTextAnimationActive)
        {
            return;
        }

        int mag = currentWeapon.currentMagazineAmmo;
        int carried = currentWeapon.currentCarriedAmmo;

        ammoText.text = BuildAmmoText(mag, carried);
    }

    private string BuildAmmoText(int mag, int carried)
    {
        string magText = mag == 0
            ? $"<color={ZeroAmmoColor}>{mag}</color>"
            : mag.ToString();

        string carriedText = carried == 0
            ? $"<color={ZeroAmmoColor}>{carried}</color>"
            : carried.ToString();

        return $"{magText}/{carriedText}";
    }

    private void EnsureReloadOutgoingAmmoText()
    {
        if (reloadOutgoingAmmoText != null || ammoText == null)
        {
            return;
        }

        GameObject clone = Instantiate(
            ammoText.gameObject,
            ammoText.transform.parent
        );

        clone.name = ammoText.name + "_ReloadOutgoing";

        clone.transform.SetSiblingIndex(
            ammoText.transform.GetSiblingIndex() + 1
        );

        reloadOutgoingAmmoText = clone.GetComponent<TMP_Text>();

        if (reloadOutgoingAmmoText == null)
        {
            Destroy(clone);
            return;
        }

        reloadOutgoingAmmoText.raycastTarget = false;
        reloadOutgoingAmmoText.richText = true;
        reloadOutgoingAmmoText.text = string.Empty;
        reloadOutgoingAmmoText.gameObject.SetActive(false);
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
        float reloadStateDuration = 1f;

        if (currentWeaponAnimator != null)
        {
            int layerIndex = (int)currentWeapon.CurrentGunData.gunType + 1;

            AnimatorStateInfo state =
                currentWeaponAnimator.GetCurrentAnimatorStateInfo(layerIndex);

            if (state.IsName("ReloadOutOfAmmo") ||
                state.IsName("ReloadLeftAmmo"))
            {
                isReloading = true;

                reloadStateName = state.IsName("ReloadOutOfAmmo")
                    ? "ReloadOutOfAmmo"
                    : "ReloadLeftAmmo";

                reloadStateProgress =
                    Mathf.Clamp01(state.normalizedTime);

                float stateSpeed = Mathf.Max(0.01f, state.speed);

                // state.length 是动画片段长度，
                // 除以其播放速度后得到本次状态实际持续时间。
                reloadStateDuration = Mathf.Max(
                    0.01f,
                    state.length / stateSpeed
                );
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

                reloadCurrentAlpha =
                    Mathf.Clamp01(weaponRedFill != null
                        ? weaponRedFill.color.a
                        : 1f);

                reloadOldMagazineAmmo = lastMagazineAmmo;
                reloadOldCarriedAmmo = lastCarriedAmmo;

                int magazineSize = lastMagazineSize > 0
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
                    float previousMagRatio = magazineSize > 0
                        ? (float)reloadOldMagazineAmmo / magazineSize
                        : 0f;

                    reloadStartFillAmount =
                        Mathf.Clamp01(1f - previousMagRatio);
                }

                // 启动独立文字动画。
                isAmmoTextAnimationActive = true;
                ammoTextAnimationProgress =
                    Mathf.Clamp01(reloadStateProgress);
                ammoTextAnimationDuration = reloadStateDuration;
            }

            if (weaponRedFill != null)
            {
                reloadCurrentAlpha = Mathf.MoveTowards(
                    reloadCurrentAlpha,
                    1f,
                    Mathf.Max(0f, blinkSpeed) * Time.deltaTime
                );

                weaponRedFill.fillAmount =
                    reloadStartFillAmount * (1f - reloadStateProgress);

                weaponRedFill.enabled = true;

                Color c = weaponRedFill.color;
                c.a = reloadCurrentAlpha;
                weaponRedFill.color = c;
            }

            wasReloading = true;
        }
        else
        {
            wasReloading = false;
            activeReloadStateName = null;
            reloadStartFillAmount = 1f;
            reloadCurrentAlpha = 1f;

            int mag = currentWeapon.currentMagazineAmmo;
            int carried = currentWeapon.currentCarriedAmmo;
            int maxMag = currentWeapon.CurrentGunData.magezineSize;

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
                    weaponRedFill.fillAmount = 1f - magRatio;
                    weaponRedFill.enabled = true;

                    Color c = weaponRedFill.color;
                    c.a = 1f;
                    weaponRedFill.color = c;
                }
            }
        }

        // 无论换弹状态是否已经结束，
        // 独立文字动画都会按照 Inspector 中的速度继续播放到结束。
        AdvanceReloadAmmoTextAnimation(Time.deltaTime);
    }

    private void AdvanceReloadAmmoTextAnimation(float deltaTime)
    {
        if (!isAmmoTextAnimationActive)
        {
            return;
        }

        float speed = Mathf.Max(0.05f, ammoReloadAnimationSpeed);

        ammoTextAnimationProgress +=
            deltaTime / ammoTextAnimationDuration * speed;

        ammoTextAnimationProgress =
            Mathf.Clamp01(ammoTextAnimationProgress);

        UpdateReloadAmmoTextAnimation(ammoTextAnimationProgress);

        if (ammoTextAnimationProgress >= 1f)
        {
            FinishReloadAmmoTextAnimation();
        }
    }

    private void FinishReloadAmmoTextAnimation()
    {
        isAmmoTextAnimationActive = false;
        ammoTextAnimationProgress = 1f;

        if (reloadOutgoingAmmoText != null)
        {
            reloadOutgoingAmmoText.text = string.Empty;
            reloadOutgoingAmmoText.gameObject.SetActive(false);
        }

        if (ammoText != null)
        {
            ammoText.text = BuildAmmoText(
                reloadNewMagazineAmmo,
                reloadNewCarriedAmmo
            );
        }
    }

    private void UpdateReloadAmmoTextAnimation(float progress)
    {
        EnsureReloadOutgoingAmmoText();

        if (ammoText == null || reloadOutgoingAmmoText == null)
        {
            return;
        }

        reloadOutgoingAmmoText.gameObject.SetActive(true);

        float t = Mathf.Clamp01(progress);
        float easedT = Mathf.SmoothStep(0f, 1f, t);
        float moveDistance = Mathf.Max(0f, ammoReloadMoveDistance);

        float oldAlpha = 1f - t;
        float newAlpha = t;

        /*
         * 旧值图层：
         * 弹匣数向下移动并淡出。
         * 备弹数向上移动并淡出。
         */
        ApplyAmmoTextAnimation(
            reloadOutgoingAmmoText,
            reloadOldMagazineAmmo,
            reloadOldCarriedAmmo,
            -moveDistance * easedT,
            moveDistance * easedT,
            oldAlpha,
            oldAlpha
        );

        /*
         * 新值图层：
         * 弹匣数从上方落入原位并淡入。
         * 备弹数从下方升入原位并淡入。
         */
        ApplyAmmoTextAnimation(
            ammoText,
            reloadNewMagazineAmmo,
            reloadNewCarriedAmmo,
            moveDistance * (1f - easedT),
            -moveDistance * (1f - easedT),
            newAlpha,
            newAlpha
        );
    }

    private void ApplyAmmoTextAnimation(
        TMP_Text targetText,
        int magazineAmmo,
        int carriedAmmo,
        float magazineYOffset,
        float carriedYOffset,
        float magazineAlpha,
        float carriedAlpha
    )
    {
        if (targetText == null)
        {
            return;
        }

        targetText.text = BuildAmmoText(magazineAmmo, carriedAmmo);
        targetText.ForceMeshUpdate();

        TMP_TextInfo textInfo = targetText.textInfo;

        int magazineDigitCount = magazineAmmo.ToString().Length;
        int carriedDigitCount = carriedAmmo.ToString().Length;

        int visibleCharacterIndex = 0;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];

            if (!characterInfo.isVisible)
            {
                continue;
            }

            float yOffset = 0f;
            float alpha = 1f;

            if (visibleCharacterIndex < magazineDigitCount)
            {
                yOffset = magazineYOffset;
                alpha = magazineAlpha;
            }
            else if (visibleCharacterIndex <
                     magazineDigitCount + 1)
            {
                yOffset = 0f;
                alpha = 1f;
            }
            else if (visibleCharacterIndex <
                     magazineDigitCount + 1 + carriedDigitCount)
            {
                yOffset = carriedYOffset;
                alpha = carriedAlpha;
            }

            visibleCharacterIndex++;

            int materialIndex = characterInfo.materialReferenceIndex;
            int vertexIndex = characterInfo.vertexIndex;

            Vector3[] vertices =
                textInfo.meshInfo[materialIndex].vertices;

            Color32[] colors =
                textInfo.meshInfo[materialIndex].colors32;

            if (vertexIndex < 0 ||
                vertexIndex + 3 >= vertices.Length)
            {
                continue;
            }

            for (int vertexOffset = 0; vertexOffset < 4; vertexOffset++)
            {
                int currentVertex = vertexIndex + vertexOffset;

                Vector3 vertex = vertices[currentVertex];
                vertex.y += yOffset;
                vertices[currentVertex] = vertex;

                Color32 baseColor = characterInfo.color;

                float baseAlpha = baseColor.a / 255f;

                byte finalAlpha = (byte)Mathf.Clamp(
                    Mathf.RoundToInt(
                        baseAlpha * alpha * 255f
                    ),
                    0,
                    255
                );

                colors[currentVertex] = new Color32(
                    baseColor.r,
                    baseColor.g,
                    baseColor.b,
                    finalAlpha
                );
            }
        }

        targetText.UpdateVertexData(
            TMP_VertexDataUpdateFlags.Vertices |
            TMP_VertexDataUpdateFlags.Colors32
        );
    }

    private void UpdateWeaponIcon()
    {
        if (weaponIcon == null || currentWeapon == null) return;
        if (weaponSprites == null || weaponSprites.Length == 0) return;

        int index = (int)currentWeapon.CurrentGunData.gunType;

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