using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthHUD : MonoBehaviour
{
    [Header("生命值 UI")]
    [Tooltip("生命值图标 Image 组件")]
    public Image healthIcon;

    [Tooltip("显示当前血量的 TextMeshPro 组件")]
    public TMP_Text healthText;

    [Tooltip("显示旧血量的 TextMeshPro 组件（用于动画）")]
    public TMP_Text healthOutgoingText;

    [Tooltip("生命图标红色填充组件")]
    public Image healthIconRedFill;

    [Header("生命值动画")]
    [Tooltip("血量变化时的动画持续时间")]
    [Range(0.01f, 1f)]
    public float healthAnimationDuration = 0.2f;

    [Tooltip("数字上下移动的距离")]
    public float healthMoveDistance = 18f;

    [Tooltip("数字和红色填充的动画速度")]
    [Min(0.05f)]
    public float healthAnimationSpeed = 1f;

    [Header("低血量闪烁")]
    [Tooltip("开始闪烁的绝对生命值")]
    [Min(0f)]
    public float lowHealthThreshold = 30f;

    [Tooltip("红白闪烁频率，越大闪烁越快")]
    [Min(0f)]
    public float lowHealthBlinkFrequency = 4f;

    private static readonly Color DamageNumberColor =
        new Color32(0xFF, 0x33, 0x33, 0xFF);

    private PlayerHealth playerHealth;
    private float currentHealth;
    private float targetHealth;

    private bool hasInitialHealth;
    private bool isLowHealth;

    private Color healthNormalColor = Color.white;
    private Color iconNormalColor = Color.white;
    private float redFillNormalAlpha = 1f;

    private Vector2 healthBasePosition;

    private bool isHealthTextAnimationActive;
    private float healthTextAnimationProgress;
    private float healthTextAnimationDuration = 1f;
    private bool damageNumberAnimation;

    private bool isHealthFillAnimationActive;
    private float healthFillAnimationProgress;
    private float healthFillAnimationDuration = 1f;
    private float healthFillTargetAmount;

    private void Awake()
    {
        if (healthText != null)
        {
            healthBasePosition =
                healthText.rectTransform.anchoredPosition;

            healthNormalColor = healthText.color;
            healthText.alpha = 1f;
        }

        if (healthIcon != null)
        {
            iconNormalColor = healthIcon.color;
        }

        if (healthOutgoingText != null)
        {
            healthOutgoingText.raycastTarget = false;
            healthOutgoingText.alpha = 1f;
            healthOutgoingText.gameObject.SetActive(false);
        }

        ConfigureHealthIconRedFill();
    }

    private void Start()
    {
        playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.HealthChanged += OnHealthChanged;

            OnHealthChanged(
                playerHealth.CurrentHealth,
                playerHealth.MaxHealth
            );
        }
        else
        {
            Debug.LogError("PlayerHealth component not found!");
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= OnHealthChanged;
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        UpdateHealthAnimation(deltaTime);
        UpdateHealthFillAnimation(deltaTime);

        UpdateLowHealthState();
        UpdateLowHealthBlink();
    }

    private void OnHealthChanged(
        float currentHealth,
        float maxHealth
    )
    {
        float previousCurrentHealth = this.currentHealth;
        bool hadInitialHealth = hasInitialHealth;

        int oldHealthValue = hadInitialHealth
            ? Mathf.CeilToInt(targetHealth)
            : Mathf.CeilToInt(currentHealth);

        int newHealthValue =
            Mathf.CeilToInt(currentHealth);

        bool isDamage =
            hadInitialHealth &&
            currentHealth < previousCurrentHealth;

        this.currentHealth = currentHealth;
        targetHealth = currentHealth;
        hasInitialHealth = true;

        float newFillAmount =
            CalculateRedFillAmount(
                currentHealth,
                maxHealth
            );

        // 第一次收到生命值时直接设置，不播放动画。
        if (!hadInitialHealth)
        {
            SetHealthTextDirectly(newHealthValue);
            SetHealthFillImmediately(newFillAmount);
            UpdateLowHealthState();
            UpdateLowHealthBlink();
            return;
        }

        // 受伤时始终播放一次数字动画。
        // 即使整数没有变化，旧数字也会变红并切换。
        if (isDamage)
        {
            BeginHealthTextAnimation(
                oldHealthValue,
                newHealthValue,
                healthAnimationDuration,
                true
            );
        }
        else if (oldHealthValue != newHealthValue)
        {
            BeginHealthTextAnimation(
                oldHealthValue,
                newHealthValue,
                healthAnimationDuration,
                false
            );
        }
        else
        {
            SetHealthTextDirectly(newHealthValue);
        }

        BeginHealthFillAnimation(
            newFillAmount,
            healthAnimationDuration
        );

        UpdateLowHealthState();
        UpdateLowHealthBlink();
    }

    private void SetHealthTextDirectly(int healthValue)
    {
        CancelHealthTextAnimation();
        SetHealthTextValue(healthText, healthValue);
        ApplyCurrentHealthTextColor();
    }

    private void BeginHealthTextAnimation(
        int oldHealthValue,
        int newHealthValue,
        float animationDuration,
        bool isDamageAnimation
    )
    {
        CancelHealthTextAnimation();

        damageNumberAnimation = isDamageAnimation;
        isHealthTextAnimationActive = true;
        healthTextAnimationProgress = 0f;
        healthTextAnimationDuration =
            Mathf.Max(0.01f, animationDuration);

        if (healthOutgoingText != null)
        {
            healthOutgoingText.gameObject.SetActive(true);
            SetHealthTextValue(
                healthOutgoingText,
                oldHealthValue
            );

            healthOutgoingText.color = isDamageAnimation
                ? DamageNumberColor
                : healthNormalColor;

            healthOutgoingText.alpha = 1f;
            healthOutgoingText.rectTransform.anchoredPosition =
                healthBasePosition;
        }

        if (healthText != null)
        {
            SetHealthTextValue(
                healthText,
                newHealthValue
            );

            // 没有旧数字组件时，新数字本身先显示为红色。
            if (healthOutgoingText == null &&
                isDamageAnimation)
            {
                healthText.color = DamageNumberColor;
            }
            else
            {
                ApplyCurrentHealthTextColor();
            }

            healthText.alpha = 0f;
            healthText.rectTransform.anchoredPosition =
                healthBasePosition
                + Vector2.down * GetMoveDistance();
        }

        ApplyHealthTextAnimation(0f);
    }

    private void UpdateHealthAnimation(float deltaTime)
    {
        if (!isHealthTextAnimationActive)
        {
            return;
        }

        float speed =
            Mathf.Max(0.05f, healthAnimationSpeed);

        healthTextAnimationProgress +=
            deltaTime
            / healthTextAnimationDuration
            * speed;

        healthTextAnimationProgress =
            Mathf.Clamp01(healthTextAnimationProgress);

        ApplyHealthTextAnimation(
            healthTextAnimationProgress
        );

        if (healthTextAnimationProgress >= 1f)
        {
            FinishHealthTextAnimation();
        }
    }

    private void ApplyHealthTextAnimation(float progress)
    {
        float t = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(progress)
        );

        float distance = GetMoveDistance();

        if (healthOutgoingText != null)
        {
            healthOutgoingText.rectTransform.anchoredPosition =
                healthBasePosition
                + Vector2.up * distance * t;

            healthOutgoingText.alpha = 1f - t;
        }

        if (healthText != null)
        {
            healthText.rectTransform.anchoredPosition =
                healthBasePosition
                + Vector2.down * distance * (1f - t);

            healthText.alpha = t;
        }
    }

    private void FinishHealthTextAnimation()
    {
        isHealthTextAnimationActive = false;
        damageNumberAnimation = false;
        healthTextAnimationProgress = 1f;

        if (healthOutgoingText != null)
        {
            healthOutgoingText.alpha = 0f;
            healthOutgoingText.gameObject.SetActive(false);
        }

        if (healthText != null)
        {
            healthText.rectTransform.anchoredPosition =
                healthBasePosition;

            healthText.alpha = 1f;
        }

        ApplyCurrentHealthTextColor();
    }

    private void CancelHealthTextAnimation()
    {
        isHealthTextAnimationActive = false;
        damageNumberAnimation = false;
        healthTextAnimationProgress = 0f;

        if (healthOutgoingText != null)
        {
            healthOutgoingText.alpha = 0f;
            healthOutgoingText.gameObject.SetActive(false);
        }

        if (healthText != null)
        {
            healthText.rectTransform.anchoredPosition =
                healthBasePosition;

            healthText.alpha = 1f;
        }
    }

    private float CalculateRedFillAmount(
        float health,
        float maxHealth
    )
    {
        if (maxHealth <= 0f)
        {
            return health <= 0f ? 1f : 0f;
        }

        float healthRatio =
            Mathf.Clamp01(health / maxHealth);

        // 红色区域表示已经损失的生命值。
        return 1f - healthRatio;
    }

    private void ConfigureHealthIconRedFill()
    {
        if (healthIconRedFill == null)
        {
            return;
        }

        healthIconRedFill.type = Image.Type.Filled;
        healthIconRedFill.fillMethod =
            Image.FillMethod.Vertical;
        healthIconRedFill.fillOrigin =
            (int)Image.OriginVertical.Top;

        if (healthIcon != null &&
            healthIcon.sprite != null)
        {
            healthIconRedFill.sprite =
                healthIcon.sprite;
        }

        redFillNormalAlpha =
            healthIconRedFill.color.a;

        healthIconRedFill.enabled = true;
    }

    private void SetHealthFillImmediately(float fillAmount)
    {
        isHealthFillAnimationActive = false;
        healthFillAnimationProgress = 1f;
        healthFillTargetAmount = fillAmount;

        if (healthIconRedFill != null)
        {
            healthIconRedFill.fillAmount = fillAmount;
        }
    }

    private void BeginHealthFillAnimation(
        float targetAmount,
        float animationDuration
    )
    {
        if (healthIconRedFill == null)
        {
            return;
        }

        float startAmount =
            healthIconRedFill.fillAmount;

        if (Mathf.Approximately(
            startAmount,
            targetAmount
        ))
        {
            SetHealthFillImmediately(targetAmount);
            return;
        }

        isHealthFillAnimationActive = true;
        healthFillAnimationProgress = 0f;
        healthFillAnimationDuration =
            Mathf.Max(0.01f, animationDuration);
        healthFillTargetAmount = targetAmount;

        ApplyHealthFillAnimation(0f);
    }

    private void UpdateHealthFillAnimation(float deltaTime)
    {
        if (!isHealthFillAnimationActive)
        {
            return;
        }

        float speed =
            Mathf.Max(0.05f, healthAnimationSpeed);

        healthFillAnimationProgress +=
            deltaTime
            / healthFillAnimationDuration
            * speed;

        healthFillAnimationProgress =
            Mathf.Clamp01(healthFillAnimationProgress);

        ApplyHealthFillAnimation(
            healthFillAnimationProgress
        );

        if (healthFillAnimationProgress >= 1f)
        {
            FinishHealthFillAnimation();
        }
    }

    private void ApplyHealthFillAnimation(float progress)
    {
        if (healthIconRedFill == null)
        {
            return;
        }

        float startAmount =
            isLowHealth && isHealthFillAnimationActive
                ? healthIconRedFill.fillAmount
                : healthIconRedFill.fillAmount;

        // 重新开始动画时保留当前显示值，避免填充跳变。
        if (healthFillAnimationProgress <= 0f)
        {
            startAmount =
                healthIconRedFill.fillAmount;
        }

        float t = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(progress)
        );

        healthIconRedFill.fillAmount =
            Mathf.Lerp(
                startAmount,
                healthFillTargetAmount,
                t
            );
    }

    private void FinishHealthFillAnimation()
    {
        isHealthFillAnimationActive = false;
        healthFillAnimationProgress = 1f;

        if (healthIconRedFill != null)
        {
            healthIconRedFill.fillAmount =
                healthFillTargetAmount;
        }
    }

    private void UpdateLowHealthState()
    {
        bool lowHealth =
            hasInitialHealth &&
            currentHealth < lowHealthThreshold;

        if (lowHealth == isLowHealth)
        {
            return;
        }

        isLowHealth = lowHealth;

        if (!isLowHealth)
        {
            RestoreNormalColors();
        }
    }

    private void UpdateLowHealthBlink()
    {
        if (!isLowHealth)
        {
            if (healthIcon != null)
            {
                healthIcon.color = iconNormalColor;
            }

            if (healthText != null &&
                !isHealthTextAnimationActive)
            {
                healthText.color = healthNormalColor;
            }

            return;
        }

        Color blinkColor =
            GetLowHealthBlinkColor();

        if (healthIcon != null)
        {
            healthIcon.color = blinkColor;
        }

        if (healthText != null)
        {
            healthText.color = blinkColor;
        }

        // HealthIconRedFill 只显示生命值填充，不参与闪烁。
    }

    private float GetLowHealthBlinkAmount()
    {
        return Mathf.PingPong(
            Time.time *
            Mathf.Max(0f, lowHealthBlinkFrequency),
            1f
        );
    }

    private Color GetLowHealthBlinkColor()
    {
        return Color.Lerp(
            Color.white,
            Color.red,
            GetLowHealthBlinkAmount()
        );
    }

    private void ApplyCurrentHealthTextColor()
    {
        if (healthText == null)
        {
            return;
        }

        if (isLowHealth)
        {
            healthText.color =
                GetLowHealthBlinkColor();
            return;
        }

        if (isHealthTextAnimationActive &&
            damageNumberAnimation &&
            healthOutgoingText == null)
        {
            healthText.color =
                DamageNumberColor;
            return;
        }

        healthText.color =
            healthNormalColor;
    }

    private void RestoreNormalColors()
    {
        if (healthIcon != null)
        {
            healthIcon.color =
                iconNormalColor;
        }

        if (healthText != null &&
            !isHealthTextAnimationActive)
        {
            healthText.color =
                healthNormalColor;
        }

        ApplyRedFillAlpha(
            redFillNormalAlpha
        );
    }

    private void ApplyRedFillAlpha(float alpha)
    {
        if (healthIconRedFill == null)
        {
            return;
        }

        Color color =
            healthIconRedFill.color;

        color.a = alpha;

        healthIconRedFill.color = color;
    }

    private float GetMoveDistance()
    {
        return Mathf.Max(0f, healthMoveDistance);
    }

    private void SetHealthTextValue(
        TMP_Text targetText,
        int value
    )
    {
        if (targetText != null)
        {
            targetText.text = value.ToString();
        }
    }

    private void ApplyHealthAppearance()
    {
        if (healthIcon != null)
        {
            healthIcon.color =
                iconNormalColor;
        }

        if (healthText != null &&
            !isHealthTextAnimationActive)
        {
            healthText.color =
                healthNormalColor;
        }

        if (healthOutgoingText != null &&
            !isHealthTextAnimationActive)
        {
            healthOutgoingText.color =
                healthNormalColor;
        }

        UpdateLowHealthBlink();
    }

    private void OnValidate()
    {
        lowHealthThreshold =
            Mathf.Max(0f, lowHealthThreshold);

        lowHealthBlinkFrequency =
            Mathf.Max(0f, lowHealthBlinkFrequency);

        ConfigureHealthIconRedFill();

        if (Application.isPlaying)
        {
            ApplyHealthAppearance();
        }
    }

    public void SetColor(Color color)
    {
        healthNormalColor = color;
        iconNormalColor = color;

        if (healthIcon != null &&
            !isLowHealth)
        {
            healthIcon.color = color;
        }

        if (healthText != null &&
            !isLowHealth &&
            !isHealthTextAnimationActive)
        {
            healthText.color = color;
        }

        if (healthOutgoingText != null &&
            !isHealthTextAnimationActive)
        {
            healthOutgoingText.color = color;
        }
    }
}