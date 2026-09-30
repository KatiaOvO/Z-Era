using UnityEngine;
using UnityEngine.UI;

// 主菜单 Logo 流动驱动：把检查器上的流动参数同步到流动 shader
// 材质，并用 unscaledTime 持续喂给 _FlowTime——保证
// Time.timeScale = 0（如暂停、加载冻结）时黑白流动依然持续。
// logo 原图为白色：shader 会在 sprite 不透明区域内做黑白流动，
// 透明区域保持镂空。
[RequireComponent(typeof(Image))]
public class MainMenuLogoFlow : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("挂流动材质的 Image，留空则用自身 Image")]
    [SerializeField]
    private Image targetImage;

    [Header("流动参数（每帧同步到材质，即改即见）")]

    [Tooltip("流速：波纹漂移快慢，0 为静止，负值反向流动")]
    [SerializeField]
    private float flowSpeed = 0.25f;

    [Tooltip("斑块大小：噪声密度，越大斑块越细碎，越小斑块越舒展")]
    [SerializeField]
    private float flowScale = 6f;

    [Tooltip("黑白比例：0 画面偏黑，0.5 黑白均衡，1 画面偏白")]
    [Range(0f, 1f)]
    [SerializeField]
    private float blackWhiteBalance = 0.5f;

    [Tooltip("明暗对比：大于 1 黑白分明，接近 1 灰蒙柔和")]
    [Range(0.5f, 4f)]
    [SerializeField]
    private float flowContrast = 2f;

    [Tooltip("效果强度：0 显示原图，1 完全黑白流动")]
    [Range(0f, 1f)]
    [SerializeField]
    private float intensity = 1f;

    [Tooltip("暗端颜色（纯黑太刺眼时可调深灰）")]
    [SerializeField]
    private Color colorDark = Color.black;

    [Tooltip("亮端颜色")]
    [SerializeField]
    private Color colorBright = Color.white;

    // 实例化出的材质副本，避免每帧 SetFloat 改到共享材质
    private Material flowMaterial;

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetImage == null || targetImage.material == null)
        {
            Debug.LogError(
                "MainMenuLogoFlow：未找到 Image 或其材质，流动无法驱动。",
                this
            );

            return;
        }

        // 实例化材质副本挂回 Image，只影响本 Image
        flowMaterial = new Material(targetImage.material);
        targetImage.material = flowMaterial;
    }

    private void Update()
    {
        if (flowMaterial == null)
        {
            return;
        }

        // 参数每帧全量同步：Play 模式下在检查器拖动即时生效
        flowMaterial.SetFloat("_FlowTime", Time.unscaledTime);
        flowMaterial.SetFloat("_FlowSpeed", flowSpeed);
        flowMaterial.SetFloat("_FlowScale", flowScale);
        flowMaterial.SetFloat("_FlowBalance", blackWhiteBalance);
        flowMaterial.SetFloat("_FlowContrast", flowContrast);
        flowMaterial.SetFloat("_Intensity", intensity);
        flowMaterial.SetColor("_ColorDark", colorDark);
        flowMaterial.SetColor("_ColorBright", colorBright);
    }

    private void OnDestroy()
    {
        if (flowMaterial != null)
        {
            Destroy(flowMaterial);
        }
    }
}
