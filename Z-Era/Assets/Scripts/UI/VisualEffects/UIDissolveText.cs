using TMPro;
using UnityEngine;

namespace Migration.UI
{
    // TMP 文本不走 UGUI 的 BaseMeshEffect 网格管线，
    // 因此溶解通过修改 TMP 网格的顶点色透明度实现：
    // 按文字矩形内的归一化坐标采样程序化噪声，与 Location 比较得出每个顶点的透明度。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class UIDissolveText : MonoBehaviour, IUIDissolveEffect
    {
        [Tooltip("噪声频率，数值越大文字溶解的碎块越细")]
        [Min(1f)]
        [SerializeField]
        private float noiseScale = 8f;

        [Tooltip("溶解动画时长（秒），Show 时 Location 从 1 动画到 0")]
        [Min(0f)]
        [SerializeField]
        private float duration = 1f;

        [Tooltip("开启后使用未缩放时间，timeScale 为 0 时动画仍会播放")]
        [SerializeField]
        private bool useUnscaledTime = true;

        // 噪声与 Location 的过渡带宽度，越小溶解边缘越锐利。
        private const float DissolveSoftness = 0.12f;

        private TMP_Text text;
        private bool isAnimating;
        private float location = 1f;
        private float targetLocation;

        public void SetLocation(float value)
        {
            location = Mathf.Clamp01(value);
            isAnimating = false;
            ApplyDissolve();
        }

        public void Show()
        {
            targetLocation = 0f;

            if (duration <= 0f)
            {
                location = 0f;
                isAnimating = false;
                ApplyDissolve();
                return;
            }

            isAnimating = true;
        }

        private void Update()
        {
            if (!isAnimating)
            {
                return;
            }

            float deltaTime =
                useUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

            location = Mathf.MoveTowards(
                location,
                targetLocation,
                deltaTime / Mathf.Max(0.001f, duration)
            );

            if (Mathf.Approximately(location, targetLocation))
            {
                isAnimating = false;
            }

            ApplyDissolve();
        }

        private void OnEnable()
        {
            text = GetComponent<TMP_Text>();

            if (text == null)
            {
                return;
            }

            text.ForceMeshUpdate();

            // 打开背包时自驱动播放溶解，不依赖 InventoryInput 的驱动链路；
            // InventoryInput 随后的 SetLocation/Show 只是重启同一动画，互不冲突。
            SetLocation(1f);
            Show();
        }

        private void ApplyDissolve()
        {
            if (text == null ||
                text.textInfo == null)
            {
                return;
            }

            Rect rect = text.rectTransform.rect;

            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            TMP_TextInfo textInfo = text.textInfo;

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[i];

                if (meshInfo.mesh == null ||
                    meshInfo.colors32 == null ||
                    meshInfo.vertices == null)
                {
                    continue;
                }

                Color32[] colors = meshInfo.colors32;
                Vector3[] vertices = meshInfo.vertices;

                for (int v = 0; v < colors.Length; v++)
                {
                    Vector2 uv = new Vector2(
                        vertices[v].x / rect.width + 0.5f,
                        vertices[v].y / rect.height + 0.5f
                    );

                    float noise = SampleNoise(uv * noiseScale);

                    // 把噪声值域抬升到 [DissolveSoftness, 1]，保证
                    // Location=0 时所有顶点完全不透明、Location=1 时完全消失。
                    noise =
                        noise * (1f - DissolveSoftness) + DissolveSoftness;

                    float alpha = Mathf.Clamp01(
                        (noise - location) / DissolveSoftness
                    );

                    // 直接按噪声写绝对透明度，避免在复用数组上逐帧累乘导致文字消失。
                    colors[v] = new Color32(
                        colors[v].r,
                        colors[v].g,
                        colors[v].b,
                        (byte)(255f * alpha)
                    );
                }

                meshInfo.mesh.colors32 = colors;
            }

            // CanvasRenderer 在 SetMesh 时拷贝顶点数据，直接改 Mesh 不生效，
            // 必须把修改后的网格重新提交给渲染器才会显示。
            // meshInfo[0] 是主字体网格；回退字体/多图集页的字符
            // 位于 meshInfo[1..] 的子网格中，由 TMP SubMesh 子对象渲染。
            if (textInfo.meshInfo[0].mesh != null)
            {
                text.canvasRenderer.SetMesh(
                    textInfo.meshInfo[0].mesh
                );
            }

            if (textInfo.meshInfo.Length > 1)
            {
                TMP_SubMeshUI[] subMeshUIs =
                    text.GetComponentsInChildren<
                        TMP_SubMeshUI
                    >(false);

                foreach (
                    TMP_SubMeshUI subMeshUI in subMeshUIs
                )
                {
                    if (subMeshUI == null ||
                        subMeshUI.canvasRenderer == null)
                    {
                        continue;
                    }

                    for (
                        int i = 1;
                        i < textInfo.meshInfo.Length;
                        i++
                    )
                    {
                        if (textInfo.meshInfo[i].mesh != null &&
                            textInfo.meshInfo[i].material ==
                                subMeshUI.sharedMaterial)
                        {
                            subMeshUI.canvasRenderer.SetMesh(
                                textInfo.meshInfo[i].mesh
                            );
                        }
                    }
                }
            }
        }

        // 二维值噪声：晶格随机值 + 平滑双线性插值，输出范围约 (0, 1)。
        private float SampleNoise(Vector2 p)
        {
            Vector2 lattice = new Vector2(
                Mathf.Floor(p.x),
                Mathf.Floor(p.y)
            );

            Vector2 fraction = p - lattice;

            float smoothX =
                fraction.x * fraction.x * (3f - 2f * fraction.x);

            float smoothY =
                fraction.y * fraction.y * (3f - 2f * fraction.y);

            float n00 = Hash(lattice);
            float n10 = Hash(lattice + Vector2.right);
            float n01 = Hash(lattice + Vector2.up);
            float n11 = Hash(lattice + Vector2.one);

            return Mathf.Lerp(
                Mathf.Lerp(n00, n10, smoothX),
                Mathf.Lerp(n01, n11, smoothX),
                smoothY
            );
        }

        private static float Hash(Vector2 v)
        {
            float value =
                Mathf.Sin(v.x * 127.1f + v.y * 311.7f) * 43758.5453f;

            // 取小数部分得到 (0,1) 的伪随机值，作为噪声晶格亮度。
            return value - Mathf.Floor(value);
        }
    }
}
