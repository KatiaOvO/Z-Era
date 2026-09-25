using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Migration.UI
{
    // 供 InventoryInput 等系统统一驱动的溶解效果接口：
    // Graphic 用 BaseMeshEffect 实现，TMP 文本用顶点色实现。
    public interface IUIDissolveEffect
    {
        void SetLocation(float location);

        void Show();
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class UIDissolveImage : BaseMeshEffect, IUIDissolveEffect
    {
        public enum ColorMode
        {
            None = 0,
            Set = 1,
            Add = 2,
            Sub = 3
        }

        public enum StartState
        {
            Visible = 0,
            Hidden = 1
        }

        private const string ShaderName = "Migration/UI Dissolve Effect";

        [Header("Dissolve")]
        [Tooltip("溶解进度。0 表示完全显示，1 表示完全消失。Show() 会动画到 0，Hide() 会动画到 1。")]
        [SerializeField, Range(0f, 1f)] private float m_Location = 0f;
        [Tooltip("溶解边缘彩色效果的宽度或覆盖范围。数值越大，边缘颜色的影响区域越宽，建议范围 0.03 到 0.2。")]
        [SerializeField, Range(0f, 1f)] private float m_Width = 0.2f;
        [Tooltip("溶解边缘的柔化程度。数值越小裁切越硬，数值越大过渡越柔和，建议范围 0.1 到 0.4，不建议设为 0。")]
        [SerializeField, Range(0f, 1f)] private float m_Softness = 0.25f;
        [Tooltip("溶解边缘使用的颜色。Color Mode 为 None 时基本不改变颜色，仅透明度会发生变化。")]
        [SerializeField, ColorUsage(false)] private Color m_Color = Color.white;
        [Tooltip("边缘颜色模式。None 为普通溶解；Set 用 Color 替换边缘颜色；Add 叠加颜色；Sub 减去颜色。")]
        [SerializeField] private ColorMode m_ColorMode = ColorMode.None;
        [Tooltip("溶解效果使用的材质。必须指向 Assets/Migration/Materials/UI Dissolve.mat，否则溶解数据不会交给溶解 Shader。")]
        [SerializeField] private Material m_EffectMaterial;

        [Header("Animation")]
        [Tooltip("Show、Hide 和 Toggle 的动画时间，单位为秒。设为 0 时立即完成，不做渐变。")]
        [SerializeField, Min(0f)] private float m_Duration = 0.45f;
        [Tooltip("组件启用时使用的初始状态。Visible 对应 Location = 0，Hidden 对应 Location = 1。")]
        [SerializeField] private StartState m_StartState = StartState.Visible;
        [Tooltip("开启后，组件每次启用都会立即应用 Start State。做默认隐藏的弹窗时可以开启。")]
        [SerializeField] private bool m_PlayStartStateOnEnable = false;
        [Tooltip("开启后使用未缩放时间。即使 Time.timeScale 为 0，溶解动画仍会播放，适合暂停菜单和弹窗。")]
        [SerializeField] private bool m_UseUnscaledTime = true;

        [Header("Events")]
        [Tooltip("Location 动画到 0、背景完全显示后触发。可用于启用按钮、播放音效或启动其他动画。")]
        [SerializeField] private UnityEvent m_OnShown = new UnityEvent();
        [Tooltip("Location 动画到 1、背景完全消失后触发。可用于关闭父面板或禁用对象。")]
        [SerializeField] private UnityEvent m_OnHidden = new UnityEvent();

        private bool m_IsAnimating;
        private float m_TargetLocation;

        public new Graphic graphic { get { return base.graphic; } }

        public float location
        {
            get { return m_Location; }
            set
            {
                float next = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_Location, next))
                    return;

                m_Location = next;
                SetVerticesDirty();
            }
        }

        public float width
        {
            get { return m_Width; }
            set
            {
                m_Width = Mathf.Clamp01(value);
                SetVerticesDirty();
            }
        }

        public float softness
        {
            get { return m_Softness; }
            set
            {
                m_Softness = Mathf.Clamp01(value);
                SetVerticesDirty();
            }
        }

        public Color color
        {
            get { return m_Color; }
            set
            {
                m_Color = value;
                SetVerticesDirty();
            }
        }

        public ColorMode colorMode { get { return m_ColorMode; } }
        public bool isAnimating { get { return m_IsAnimating; } }
        public bool isVisible { get { return m_Location <= 0.001f; } }

        protected override void OnEnable()
        {
            ApplyMaterial();
            EnsureCanvasShaderChannels();
            base.OnEnable();

            if (m_PlayStartStateOnEnable)
                SetVisible(m_StartState == StartState.Visible, true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (graphic != null && graphic.material == m_EffectMaterial)
                graphic.material = null;
        }

        private void Update()
        {
            if (!m_IsAnimating)
                return;

            float deltaTime = m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (m_Duration <= 0f)
            {
                location = m_TargetLocation;
            }
            else
            {
                location = Mathf.MoveTowards(
                    m_Location,
                    m_TargetLocation,
                    deltaTime / m_Duration);
            }

            if (Mathf.Approximately(m_Location, m_TargetLocation))
                CompleteAnimation();
        }

        public void Show()
        {
            PlayTo(0f);
        }

        public void Hide()
        {
            PlayTo(1f);
        }

        public void Toggle()
        {
            if (m_Location > 0.5f)
                Show();
            else
                Hide();
        }

        public void SetVisible(bool visible)
        {
            if (visible)
                Show();
            else
                Hide();
        }

        public void SetVisible(bool visible, bool immediate)
        {
            PlayTo(visible ? 0f : 1f, immediate);
        }

        public void SetLocation(float value)
        {
            m_IsAnimating = false;
            location = value;
        }

        private void PlayTo(float target, bool immediate = false)
        {
            m_TargetLocation = Mathf.Clamp01(target);

            if (immediate || m_Duration <= 0f)
            {
                location = m_TargetLocation;
                CompleteAnimation();
                return;
            }

            m_IsAnimating = true;
        }

        private void CompleteAnimation()
        {
            bool wasAnimating = m_IsAnimating;
            m_IsAnimating = false;

            if (!wasAnimating)
                return;

            if (m_TargetLocation <= 0f)
                m_OnShown.Invoke();
            else
                m_OnHidden.Invoke();
        }

        private void ApplyMaterial()
        {
            if (graphic == null || m_EffectMaterial == null)
                return;

            graphic.material = m_EffectMaterial;
        }

        private void EnsureCanvasShaderChannels()
        {
            if (graphic == null || graphic.canvas == null)
                return;

            Canvas canvas = graphic.canvas;
            AdditionalCanvasShaderChannels required = AdditionalCanvasShaderChannels.TexCoord1;

            if ((canvas.additionalShaderChannels & required) != required)
                canvas.additionalShaderChannels |= required;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || graphic == null)
                return;

            EnsureCanvasShaderChannels();

            Rect rect = graphic.rectTransform.rect;
            UIVertex vertex = default(UIVertex);

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);

                float x = Mathf.Clamp01(vertex.position.x / rect.width + 0.5f);
                float y = Mathf.Clamp01(vertex.position.y / rect.height + 0.5f);

                vertex.uv1 = new Vector2(
                    PackToFloat(x, y, m_Location, m_Width),
                    PackToFloat(m_Color.r, m_Color.g, m_Color.b, m_Softness));

                vh.SetUIVertex(vertex, i);
            }
        }

        private void SetVerticesDirty()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        private static float PackToFloat(float x, float y, float z)
        {
            const int precision = (1 << 8) - 1;
            return (Mathf.FloorToInt(z * precision) << 16)
                + (Mathf.FloorToInt(y * precision) << 8)
                + Mathf.FloorToInt(x * precision);
        }

        private static float PackToFloat(float x, float y, float z, float w)
        {
            const int precision = (1 << 6) - 1;
            return (Mathf.FloorToInt(w * precision) << 18)
                + (Mathf.FloorToInt(z * precision) << 12)
                + (Mathf.FloorToInt(y * precision) << 6)
                + Mathf.FloorToInt(x * precision);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            m_Location = Mathf.Clamp01(m_Location);
            m_Width = Mathf.Clamp01(m_Width);
            m_Softness = Mathf.Clamp01(m_Softness);
            m_Duration = Mathf.Max(0f, m_Duration);

            if (m_OnShown == null)
                m_OnShown = new UnityEvent();

            if (m_OnHidden == null)
                m_OnHidden = new UnityEvent();

            SetVerticesDirty();
        }
    }
}
