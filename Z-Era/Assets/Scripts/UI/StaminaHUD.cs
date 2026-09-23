using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StaminaHUD : MonoBehaviour
{
    [Header("体力值 UI")]
    [Tooltip("体力值图标 Image 组件")]
    public Image staminaIcon;

    [Tooltip("显示当前体力值的 TextMeshPro 组件")]
    public TMP_Text staminaText;

    [Tooltip("显示旧体力值的 TextMeshPro 组件（用于动画）")]
    public TMP_Text staminaOutgoingText;

    [Tooltip("体力图标红色填充组件")]
    public Image staminaIconRedFill;

    [Header("体力值动画")]
    [Tooltip("体力值变化时的动画持续时间")]
    [Range(0.01f, 1f)]
    public float staminaAnimationDuration = 0.2f;

    [Tooltip("数字上下移动的距离")]
    public float staminaMoveDistance = 18f;

    [Tooltip("数字和红色填充的动画速度")]
    [Min(0.05f)]
    public float staminaAnimationSpeed = 1f;

    [Header("低体力闪烁")]
    [Tooltip("开始闪烁的体力值阈值")]
    [Min(0f)]
    public float lowStaminaThreshold = 30f;

    [Tooltip("红白闪烁频率，越大闪烁越快")]
    [Min(0f)]
    public float lowStaminaBlinkFrequency = 4f;

    private PlayerStamina playerStamina;
    private float currentStamina;
    private float targetStamina;

    private bool hasInitialStamina;
    private bool isLowStamina;

    private Color staminaNormalColor = Color.white;
    private Color iconNormalColor = Color.white;
    private float redFillNormalAlpha = 1f;

    private Vector2 staminaBasePosition;

    private bool isStaminaTextAnimationActive;
    private float staminaTextAnimationProgress;
    private float staminaTextAnimationDuration = 1f;

    private bool isStaminaFillAnimationActive;
    private float staminaFillAnimationProgress;
    private float staminaFillAnimationDuration = 1f;
    private float staminaFillTargetAmount;

    private void Awake()
    {
        if (staminaText != null)
        {
            staminaBasePosition =
                staminaText.rectTransform.anchoredPosition;

            staminaNormalColor = staminaText.color;
            staminaText.alpha = 1f;
        }

        if (staminaIcon != null)
        {
            iconNormalColor = staminaIcon.color;
        }

        if (staminaOutgoingText != null)
        {
            staminaOutgoingText.raycastTarget = false;
            staminaOutgoingText.alpha = 1f;
            staminaOutgoingText.gameObject.SetActive(false);
        }

        ConfigureStaminaIconRedFill();
    }

    private void Start()
    {
        playerStamina = FindObjectOfType<PlayerStamina>();

        if (playerStamina != null)
        {
            playerStamina.StaminaChanged += OnStaminaChanged;

            OnStaminaChanged(
                playerStamina.CurrentStamina,
                playerStamina.MaxStamina
            );
        }
        else
        {
            Debug.LogError("PlayerStamina component not found!");
        }
    }

    private void OnDestroy()
    {
        if (playerStamina != null)
        {
            playerStamina.StaminaChanged -= OnStaminaChanged;
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        UpdateStaminaAnimation(deltaTime);
        UpdateStaminaFillAnimation(deltaTime);

        UpdateLowStaminaState();
        UpdateLowStaminaBlink();
    }

    private void OnStaminaChanged(
        float currentStamina,
        float maxStamina
    )
    {
        float previousCurrentStamina = this.currentStamina;
        bool hadInitialStamina = hasInitialStamina;

        int oldStaminaValue = hadInitialStamina
            ? Mathf.CeilToInt(targetStamina)
            : Mathf.CeilToInt(currentStamina);

        int newStaminaValue =
            Mathf.CeilToInt(currentStamina);

        bool isStaminaDecrease =
            hadInitialStamina &&
            currentStamina < previousCurrentStamina;

        this.currentStamina = currentStamina;
        targetStamina = currentStamina;
        hasInitialStamina = true;

        float newFillAmount =
            CalculateRedFillAmount(
                currentStamina,
                maxStamina
            );

        // 先更新低体力状态，以便后续判断
        UpdateLowStaminaState();

        // 第一次收到体力值时直接设置，不播放动画。
        if (!hadInitialStamina)
        {
            SetStaminaTextDirectly(newStaminaValue);
            SetStaminaFillImmediately(newFillAmount);
            UpdateLowStaminaBlink();
            return;
        }

        // 低体力闪烁期间，跳过数字动画，避免旧文本闪烁
        bool shouldSkipAnimation = isLowStamina &&
                                   currentStamina < lowStaminaThreshold;

        if (isStaminaDecrease || oldStaminaValue != newStaminaValue)
        {
            if (shouldSkipAnimation)
            {
                SetStaminaTextDirectly(newStaminaValue);
            }
            else
            {
                BeginStaminaTextAnimation(
                    oldStaminaValue,
                    newStaminaValue,
                    staminaAnimationDuration
                );
            }
        }
        else
        {
            SetStaminaTextDirectly(newStaminaValue);
        }

        BeginStaminaFillAnimation(
            newFillAmount,
            staminaAnimationDuration
        );

        UpdateLowStaminaBlink();
    }

    private void SetStaminaTextDirectly(int staminaValue)
    {
        CancelStaminaTextAnimation();
        SetStaminaTextValue(staminaText, staminaValue);
        ApplyCurrentStaminaTextColor();
    }

    private void BeginStaminaTextAnimation(
        int oldStaminaValue,
        int newStaminaValue,
        float animationDuration
    )
    {
        CancelStaminaTextAnimation();

        isStaminaTextAnimationActive = true;
        staminaTextAnimationProgress = 0f;
        staminaTextAnimationDuration =
            Mathf.Max(0.01f, animationDuration);

        if (staminaOutgoingText != null)
        {
            staminaOutgoingText.gameObject.SetActive(true);
            SetStaminaTextValue(
                staminaOutgoingText,
                oldStaminaValue
            );

            staminaOutgoingText.color = staminaNormalColor;
            staminaOutgoingText.alpha = 1f;
            staminaOutgoingText.rectTransform.anchoredPosition =
                staminaBasePosition;
        }

        if (staminaText != null)
        {
            SetStaminaTextValue(
                staminaText,
                newStaminaValue
            );

            ApplyCurrentStaminaTextColor();

            staminaText.alpha = 0f;
            staminaText.rectTransform.anchoredPosition =
                staminaBasePosition
                + Vector2.down * GetMoveDistance();
        }

        ApplyStaminaTextAnimation(0f);
    }

    private void UpdateStaminaAnimation(float deltaTime)
    {
        if (!isStaminaTextAnimationActive)
        {
            return;
        }

        float speed =
            Mathf.Max(0.05f, staminaAnimationSpeed);

        staminaTextAnimationProgress +=
            deltaTime
            / staminaTextAnimationDuration
            * speed;

        staminaTextAnimationProgress =
            Mathf.Clamp01(staminaTextAnimationProgress);

        ApplyStaminaTextAnimation(
            staminaTextAnimationProgress
        );

        if (staminaTextAnimationProgress >= 1f)
        {
            FinishStaminaTextAnimation();
        }
    }

    private void ApplyStaminaTextAnimation(float progress)
    {
        float t = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(progress)
        );

        float distance = GetMoveDistance();

        if (staminaOutgoingText != null)
        {
            staminaOutgoingText.rectTransform.anchoredPosition =
                staminaBasePosition
                + Vector2.up * distance * t;

            staminaOutgoingText.alpha = 1f - t;
        }

        if (staminaText != null)
        {
            staminaText.rectTransform.anchoredPosition =
                staminaBasePosition
                + Vector2.down * distance * (1f - t);

            staminaText.alpha = t;
        }
    }

    private void FinishStaminaTextAnimation()
    {
        isStaminaTextAnimationActive = false;
        staminaTextAnimationProgress = 1f;

        if (staminaOutgoingText != null)
        {
            staminaOutgoingText.alpha = 0f;
            staminaOutgoingText.gameObject.SetActive(false);
        }

        if (staminaText != null)
        {
            staminaText.rectTransform.anchoredPosition =
                staminaBasePosition;

            staminaText.alpha = 1f;
        }

        ApplyCurrentStaminaTextColor();
    }

    private void CancelStaminaTextAnimation()
    {
        isStaminaTextAnimationActive = false;
        staminaTextAnimationProgress = 0f;

        if (staminaOutgoingText != null)
        {
            staminaOutgoingText.alpha = 0f;
            staminaOutgoingText.gameObject.SetActive(false);
        }

        if (staminaText != null)
        {
            staminaText.rectTransform.anchoredPosition =
                staminaBasePosition;

            staminaText.alpha = 1f;
        }
    }

    private float CalculateRedFillAmount(
        float stamina,
        float maxStamina
    )
    {
        if (maxStamina <= 0f)
        {
            return stamina <= 0f ? 1f : 0f;
        }

        float staminaRatio =
            Mathf.Clamp01(stamina / maxStamina);

        // 红色区域表示已经消耗的体力值。
        return 1f - staminaRatio;
    }

    private void ConfigureStaminaIconRedFill()
    {
        if (staminaIconRedFill == null)
        {
            return;
        }

        staminaIconRedFill.type = Image.Type.Filled;
        staminaIconRedFill.fillMethod =
            Image.FillMethod.Vertical;
        staminaIconRedFill.fillOrigin =
            (int)Image.OriginVertical.Top;

        if (staminaIcon != null &&
            staminaIcon.sprite != null)
        {
            staminaIconRedFill.sprite =
                staminaIcon.sprite;
        }

        redFillNormalAlpha =
            staminaIconRedFill.color.a;

        staminaIconRedFill.enabled = true;
    }

    private void SetStaminaFillImmediately(float fillAmount)
    {
        isStaminaFillAnimationActive = false;
        staminaFillAnimationProgress = 1f;
        staminaFillTargetAmount = fillAmount;

        if (staminaIconRedFill != null)
        {
            staminaIconRedFill.fillAmount = fillAmount;
        }
    }

    private void BeginStaminaFillAnimation(
        float targetAmount,
        float animationDuration
    )
    {
        if (staminaIconRedFill == null)
        {
            return;
        }

        float startAmount =
            staminaIconRedFill.fillAmount;

        if (Mathf.Approximately(
            startAmount,
            targetAmount
        ))
        {
            SetStaminaFillImmediately(targetAmount);
            return;
        }

        isStaminaFillAnimationActive = true;
        staminaFillAnimationProgress = 0f;
        staminaFillAnimationDuration =
            Mathf.Max(0.01f, animationDuration);
        staminaFillTargetAmount = targetAmount;

        ApplyStaminaFillAnimation(0f);
    }

    private void UpdateStaminaFillAnimation(float deltaTime)
    {
        if (!isStaminaFillAnimationActive)
        {
            return;
        }

        float speed =
            Mathf.Max(0.05f, staminaAnimationSpeed);

        staminaFillAnimationProgress +=
            deltaTime
            / staminaFillAnimationDuration
            * speed;

        staminaFillAnimationProgress =
            Mathf.Clamp01(staminaFillAnimationProgress);

        ApplyStaminaFillAnimation(
            staminaFillAnimationProgress
        );

        if (staminaFillAnimationProgress >= 1f)
        {
            FinishStaminaFillAnimation();
        }
    }

    private void ApplyStaminaFillAnimation(float progress)
    {
        if (staminaIconRedFill == null)
        {
            return;
        }

        float t = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(progress)
        );

        staminaIconRedFill.fillAmount =
            Mathf.Lerp(
                staminaIconRedFill.fillAmount,
                staminaFillTargetAmount,
                t
            );
    }

    private void FinishStaminaFillAnimation()
    {
        isStaminaFillAnimationActive = false;
        staminaFillAnimationProgress = 1f;

        if (staminaIconRedFill != null)
        {
            staminaIconRedFill.fillAmount =
                staminaFillTargetAmount;
        }
    }

    private void UpdateLowStaminaState()
    {
        bool lowStamina =
            hasInitialStamina &&
            currentStamina < lowStaminaThreshold;

        if (lowStamina == isLowStamina)
        {
            return;
        }

        isLowStamina = lowStamina;

        if (!isLowStamina)
        {
            RestoreNormalColors();
        }
    }

    private void UpdateLowStaminaBlink()
    {
        if (!isLowStamina)
        {
            if (staminaIcon != null)
            {
                staminaIcon.color = iconNormalColor;
            }

            if (staminaText != null &&
                !isStaminaTextAnimationActive)
            {
                staminaText.color = staminaNormalColor;
            }

            return;
        }

        Color blinkColor =
            GetLowStaminaBlinkColor();

        if (staminaIcon != null)
        {
            staminaIcon.color = blinkColor;
        }

        if (staminaText != null)
        {
            staminaText.color = blinkColor;
        }

        // StaminaIconRedFill 只显示体力值填充，不参与闪烁。
    }

    private float GetLowStaminaBlinkAmount()
    {
        return Mathf.PingPong(
            Time.time *
            Mathf.Max(0f, lowStaminaBlinkFrequency),
            1f
        );
    }

    private Color GetLowStaminaBlinkColor()
    {
        return Color.Lerp(
            Color.white,
            Color.red,
            GetLowStaminaBlinkAmount()
        );
    }

    private void ApplyCurrentStaminaTextColor()
    {
        if (staminaText == null)
        {
            return;
        }

        if (isLowStamina)
        {
            staminaText.color =
                GetLowStaminaBlinkColor();
            return;
        }

        staminaText.color =
            staminaNormalColor;
    }

    private void RestoreNormalColors()
    {
        if (staminaIcon != null)
        {
            staminaIcon.color =
                iconNormalColor;
        }

        if (staminaText != null &&
            !isStaminaTextAnimationActive)
        {
            staminaText.color =
                staminaNormalColor;
        }

        ApplyRedFillAlpha(
            redFillNormalAlpha
        );
    }

    private void ApplyRedFillAlpha(float alpha)
    {
        if (staminaIconRedFill == null)
        {
            return;
        }

        Color color =
            staminaIconRedFill.color;

        color.a = alpha;

        staminaIconRedFill.color = color;
    }

    private float GetMoveDistance()
    {
        return Mathf.Max(0f, staminaMoveDistance);
    }

    private void SetStaminaTextValue(
        TMP_Text targetText,
        int value
    )
    {
        if (targetText != null)
        {
            targetText.text = value.ToString();
        }
    }

    private void OnValidate()
    {
        lowStaminaThreshold =
            Mathf.Max(0f, lowStaminaThreshold);

        lowStaminaBlinkFrequency =
            Mathf.Max(0f, lowStaminaBlinkFrequency);

        ConfigureStaminaIconRedFill();
    }

    public void SetColor(Color color)
    {
        staminaNormalColor = color;
        iconNormalColor = color;

        if (staminaIcon != null &&
            !isLowStamina)
        {
            staminaIcon.color = color;
        }

        if (staminaText != null &&
            !isLowStamina &&
            !isStaminaTextAnimationActive)
        {
            staminaText.color = color;
        }

        if (staminaOutgoingText != null &&
            !isStaminaTextAnimationActive)
        {
            staminaOutgoingText.color = color;
        }
    }
}