using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Migration.UI
{
    [DisallowMultipleComponent]
    public sealed class UIFillOnHover : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Header("Target")]
        [SerializeField] private Image m_TargetImage;
        [SerializeField] private bool m_ConfigureImageOnAwake = true;

        [Header("Fill")]
        [SerializeField, Range(0f, 1f)] private float m_StartAmount = 0f;
        [SerializeField, Range(0f, 1f)] private float m_EndAmount = 1f;
        [SerializeField, Min(0f)] private float m_FillDuration = 0.18f;
        [SerializeField, Min(0f)] private float m_EmptyDuration = 0.18f;
        [SerializeField] private bool m_FillOnHover = true;
        [SerializeField] private bool m_FillWhilePressed = true;
        [SerializeField] private bool m_FillOnSelect = false;
        [SerializeField] private bool m_UseUnscaledTime = true;

        private bool m_IsHovered;
        private bool m_IsPressed;
        private bool m_IsSelected;
        private bool m_Initialized;
        private float m_CurrentAmount;
        private float m_TargetAmount;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            RefreshTarget(false);
        }

        private void Update()
        {
            if (!m_Initialized || m_TargetImage == null)
                return;

            if (Mathf.Approximately(m_CurrentAmount, m_TargetAmount))
                return;

            float deltaTime = m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float duration = m_TargetAmount > m_CurrentAmount ? m_FillDuration : m_EmptyDuration;

            if (duration <= 0f)
            {
                m_CurrentAmount = m_TargetAmount;
            }
            else
            {
                m_CurrentAmount = Mathf.MoveTowards(
                    m_CurrentAmount,
                    m_TargetAmount,
                    deltaTime / duration);
            }

            m_TargetImage.fillAmount = m_CurrentAmount;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_IsHovered = true;
            RefreshTarget();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_IsHovered = false;
            RefreshTarget();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_IsPressed = true;
            RefreshTarget();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            m_IsPressed = false;
            RefreshTarget();
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_IsSelected = true;
            RefreshTarget();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            m_IsSelected = false;
            RefreshTarget();
        }

        public void SetFillImmediately(float amount)
        {
            if (!Initialize())
                return;

            m_CurrentAmount = Mathf.Clamp01(amount);
            m_TargetAmount = m_CurrentAmount;
            m_TargetImage.fillAmount = m_CurrentAmount;
        }

        public void ResetToStart()
        {
            SetFillImmediately(m_StartAmount);
        }

        private bool Initialize()
        {
            if (m_TargetImage == null)
                m_TargetImage = FindFillImage();

            if (m_TargetImage == null)
                return false;

            if (m_ConfigureImageOnAwake)
            {
                m_TargetImage.type = Image.Type.Filled;
                m_TargetImage.fillMethod = Image.FillMethod.Horizontal;
                m_TargetImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                m_TargetImage.fillClockwise = true;
            }

            if (!m_Initialized)
            {
                m_CurrentAmount = Mathf.Clamp01(m_TargetImage.fillAmount);
                m_TargetAmount = m_CurrentAmount;
                m_Initialized = true;
            }

            return true;
        }

        private Image FindFillImage()
        {
            Transform fill = transform.Find("Fill");
            if (fill != null)
            {
                Image image = fill.GetComponent<Image>();
                if (image != null)
                    return image;
            }

            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject != gameObject)
                    return images[i];
            }

            return GetComponent<Image>();
        }

        private void RefreshTarget(bool animated = true)
        {
            if (!Initialize())
                return;

            bool shouldFill =
                (m_FillOnHover && m_IsHovered) ||
                (m_FillWhilePressed && m_IsPressed) ||
                (m_FillOnSelect && m_IsSelected);

            m_TargetAmount = shouldFill ? m_EndAmount : m_StartAmount;

            if (!animated)
            {
                m_CurrentAmount = m_TargetAmount;
                m_TargetImage.fillAmount = m_CurrentAmount;
            }
        }

        private void OnValidate()
        {
            m_StartAmount = Mathf.Clamp01(m_StartAmount);
            m_EndAmount = Mathf.Clamp01(m_EndAmount);
            m_FillDuration = Mathf.Max(0f, m_FillDuration);
            m_EmptyDuration = Mathf.Max(0f, m_EmptyDuration);
        }
    }
}
