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
        [Tooltip("执行填充动画的 Image（Type 需为 Filled），留空则自动查找：优先名为 Fill 的直接子物体，其次子物体中的第一个 Image，最后退化为自身 Image")]
        [SerializeField] private Image m_TargetImage;
        [Tooltip("开启后 Awake 时自动把目标 Image 配置为 Filled 类型、水平填充、从左往右；已手动配置填充方式时关闭以免被覆盖")]
        [SerializeField] private bool m_ConfigureImageOnAwake = true;

        [Header("Fill")]
        [Tooltip("未交互（未悬停/未按下/未选中）时回落的填充量")]
        [SerializeField, Range(0f, 1f)] private float m_StartAmount = 0f;
        [Tooltip("交互（悬停/按下/选中任一条件满足）时填充到的目标量")]
        [SerializeField, Range(0f, 1f)] private float m_EndAmount = 1f;
        [Tooltip("填充动画时长（秒）：从当前量升到目标量的速度，0 表示瞬间填满")]
        [SerializeField, Min(0f)] private float m_FillDuration = 0.18f;
        [Tooltip("回落动画时长（秒）：从当前量降回起始量的速度，0 表示瞬间清空")]
        [SerializeField, Min(0f)] private float m_EmptyDuration = 0.18f;
        [Tooltip("鼠标悬停在按钮上时填充")]
        [SerializeField] private bool m_FillOnHover = true;
        [Tooltip("鼠标按下期间填充")]
        [SerializeField] private bool m_FillWhilePressed = true;
        [Tooltip("被键盘/手柄选中（EventSystem 的 Select）时填充，适合手柄导航场景")]
        [SerializeField] private bool m_FillOnSelect = false;
        [Tooltip("开启后动画使用 unscaledDeltaTime，Time.timeScale = 0（如暂停）时依然播放")]
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
