using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 全局设置单例：程序启动时自动创建（BeforeSceneLoad），跨场景存活，
/// 负责所有与场景无关的玩家设置：亮度（后处理曝光）与全局音量
/// （AudioListener.volume），后续灵敏度等可继续挂到这里。
/// 亮度实现：运行时创建一个 Global Volume 并在其 Profile 上写入
/// ColorAdjustments.postExposure（曝光偏移），滑条值以 0.5 为锚点
/// 分段映射曝光：0.5 对应 0 EV（画面与未调节时完全一致），
/// 0 对应 -3 EV，1 对应 +1 EV。Profile 只此一份，
/// 所有场景的相机共享同一个 Volume，因此一处修改全局生效。
/// 另外每个场景加载后会自动打开所有相机（含武器等 Overlay 相机）的
/// Post Processing，新场景无需手动勾选。Overlay 相机的曝光在 Base
/// 合成它之前不会重复应用，因此武器与场景亮度变化保持一致。
/// 亮度值通过 PlayerPrefs 持久化，下次启动自动恢复。
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // 滑条值（0~1）映射到的曝光范围（EV）：
    // 负向可以压得低（-3 约 1/8 亮度），正向控制得保守些，
    // +1（约 2 倍）已足够提亮，更高会过曝到看不清
    private const float MinExposure = -3f;
    private const float MaxExposure = 1.0f;

    private const float DefaultSliderValue = 0.5f;
    private const string BrightnessKey = "Settings.Brightness";

    private const string VolumeKey = "Settings.Volume";
    private const float DefaultVolumeValue = 0.5f;

    // 滑条最大档允许的软件增益倍数：AudioListener.volume 上限是 1
    // （原生响度），超过部分由 MasterVolumeFilter 输出滤波完成。
    // 只增强上半段：滑条 0.5 以内增益 1（音量手感与之前一致），
    // 向 1 线性升到 2 倍（原生响度 +6 dB）
    private const float MaxVolumeBoost = 2f;

    // 当前音量增益，MasterVolumeFilter 在音频线程直接读取：
    // 用静态属性避免在音频回调里访问 Unity API 或产生分配
    public static float VolumeGain { get; private set; } = 1f;

    // 灵敏度允许的数值范围与默认值：默认 2.0 即 PlayerController
    // 原 mouseSensitivity 字段的默认值，对应滑条进度 0.5；
    // 输入字段超出范围时自动钳到最值
    private const float MinSensitivity = 0.5f;
    private const float MaxSensitivity = 5f;
    private const float DefaultSensitivity = 2f;
    private const string SensitivityKey = "Settings.Sensitivity";

    // 当前全局灵敏度，PlayerController 每帧读取：
    // 用静态属性让任何场景的 Player 无需查找即可生效
    public static float MouseSensitivity { get; private set; } =
        DefaultSensitivity;

    // 射击镜头侧倾（左右晃动）开关：默认开启。只关闭 LateUpdate
    // 里叠加的镜头侧倾，后坐力抬枪（PlayRecoil）不受影响；
    // 关闭供晕 3D 的玩家使用
    private const string CameraRecoilKey = "Settings.CameraRecoil";
    private const int DefaultCameraRecoilEnabled = 1;

    public static bool CameraRecoilEnabled { get; private set; } = true;

    private ColorAdjustments colorAdjustments;

    /// <summary>程序启动时自动创建，无需在场景中手动摆放</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
        {
            new GameObject("SettingsManager").AddComponent<SettingsManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateBrightnessVolume();
        SetVolume(GetVolume());
        SetSensitivity(GetSensitivity());
        SetCameraRecoilEnabled(GetCameraRecoilEnabled());
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    /// <summary>读取持久化的亮度滑条值（0~1），首次运行为 0.5（不改变亮度）</summary>
    public float GetBrightness()
    {
        return PlayerPrefs.GetFloat(BrightnessKey, DefaultSliderValue);
    }

    /// <summary>应用亮度（0~1，实时生效），同时持久化</summary>
    public void SetBrightness(float sliderValue)
    {
        sliderValue = Mathf.Clamp01(sliderValue);
        PlayerPrefs.SetFloat(BrightnessKey, sliderValue);

        if (colorAdjustments != null)
        {
            // 以 0.5 为锚点的分段线性映射：0.5 严格对应 0 EV
            // （画面与未开启亮度调节时完全一致），0 → 最暗、
            // 1 → 最亮。若两端直接线性插值，负向范围更大时
            // 0.5 会落在 0 EV 以下，默认亮度就会偏暗
            float exposure;

            if (sliderValue <= DefaultSliderValue)
            {
                exposure = Mathf.Lerp(
                    MinExposure,
                    0f,
                    sliderValue / DefaultSliderValue
                );
            }
            else
            {
                exposure = Mathf.Lerp(
                    0f,
                    MaxExposure,
                    (sliderValue - DefaultSliderValue) /
                        (1f - DefaultSliderValue)
                );
            }

            colorAdjustments.postExposure.value = exposure;
        }
    }

    /// <summary>读取持久化的全局音量滑条值（0~1），首次运行为 0.5</summary>
    public float GetVolume()
    {
        return PlayerPrefs.GetFloat(VolumeKey, DefaultVolumeValue);
    }

    /// <summary>
    /// 应用全局音量（0~1，实时生效），同时持久化。
    /// 直接写 AudioListener.volume：它是监听器总音量，
    /// 场景中所有 AudioSource 都受其缩放，无需逐个修改。
    /// 滑条值经平方曲线映射到实际音量：人耳对响度的感知接近
    /// 对数，线性映射时中档与最大档听感差别小；平方曲线把
    /// 前半段压得更低，拉到最大时的提升就明显得多。
    /// 滑条超过 0.5 后由 MasterVolumeFilter 叠加软件增益
    /// （VolumeGain），最大档可突破原生响度到 +6 dB
    /// </summary>
    public void SetVolume(float sliderValue)
    {
        sliderValue = Mathf.Clamp01(sliderValue);
        PlayerPrefs.SetFloat(VolumeKey, sliderValue);
        AudioListener.volume = sliderValue * sliderValue;

        VolumeGain = sliderValue <= DefaultSliderValue
            ? 1f
            : Mathf.Lerp(
                1f,
                MaxVolumeBoost,
                (sliderValue - DefaultSliderValue) /
                    (1f - DefaultSliderValue)
            );
    }

    /// <summary>读取持久化的灵敏度数值，首次运行为 2（PlayerController 原默认值）</summary>
    public float GetSensitivity()
    {
        return PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
    }

    /// <summary>
    /// 应用全局灵敏度（实际数值，超出 0.5~5 的部分自动钳到最值），
    /// 同时持久化。PlayerController 每帧读取 MouseSensitivity 生效
    /// </summary>
    public void SetSensitivity(float sensitivity)
    {
        sensitivity = Mathf.Clamp(
            sensitivity,
            MinSensitivity,
            MaxSensitivity
        );

        PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        MouseSensitivity = sensitivity;
    }

    // 灵敏度数值与滑条进度（0~1）的相互映射：
    // 以 0.5 为锚点的分段线性——0.5 对应默认值 2，
    // 0 对应最小值 0.5，1 对应最大值 5，
    // 与亮度的映射思路一致（中点 = 原生手感）

    /// <summary>滑条进度（0~1）→ 灵敏度数值</summary>
    public static float SliderToSensitivity(float sliderValue)
    {
        sliderValue = Mathf.Clamp01(sliderValue);

        return sliderValue <= DefaultSliderValue
            ? Mathf.Lerp(
                MinSensitivity,
                DefaultSensitivity,
                sliderValue / DefaultSliderValue)
            : Mathf.Lerp(
                DefaultSensitivity,
                MaxSensitivity,
                (sliderValue - DefaultSliderValue) /
                    (1f - DefaultSliderValue));
    }

    /// <summary>灵敏度数值 → 滑条进度（0~1），数值先钳到允许范围</summary>
    public static float SensitivityToSlider(float sensitivity)
    {
        sensitivity = Mathf.Clamp(
            sensitivity,
            MinSensitivity,
            MaxSensitivity
        );

        return sensitivity <= DefaultSensitivity
            ? DefaultSliderValue *
                (sensitivity - MinSensitivity) /
                (DefaultSensitivity - MinSensitivity)
            : DefaultSliderValue +
                (1f - DefaultSliderValue) *
                (sensitivity - DefaultSensitivity) /
                (MaxSensitivity - DefaultSensitivity);
    }

    /// <summary>读取持久化的射击镜头侧倾开关，首次运行默认开启</summary>
    public bool GetCameraRecoilEnabled()
    {
        return PlayerPrefs.GetInt(
            CameraRecoilKey,
            DefaultCameraRecoilEnabled) == 1;
    }

    /// <summary>
    /// 应用射击镜头侧倾开关（实时生效），同时持久化。
    /// 只控制 LateUpdate 叠加的镜头左右侧倾，后坐力抬枪保留；
    /// CameraRecoil 读取静态属性 CameraRecoilEnabled 决定是否
    /// 产生侧倾——不用组件开关：锁定系统（对话/训练场等）会
    /// 禁用并按记录恢复组件的 enabled，组件级开关会被它们撤销，
    /// 标志位则与所有系统互不干扰
    /// </summary>
    public void SetCameraRecoilEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(
            CameraRecoilKey,
            enabled ? 1 : 0);

        CameraRecoilEnabled = enabled;
    }

    private void CreateBrightnessVolume()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        colorAdjustments = profile.Add<ColorAdjustments>();
        colorAdjustments.postExposure.overrideState = true;

        var volumeObject = new GameObject("GlobalBrightnessVolume");
        volumeObject.transform.SetParent(transform, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        // 直接共享同一个 Profile 实例，避免 Volume 再 Instantiate 一份副本
        volume.sharedProfile = profile;

        SetBrightness(GetBrightness());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnableCameraPostProcessing();
        EnsureVolumeGainFilter();
    }

    // Base 和 Overlay 相机都开启后处理：Base 的后处理在其合成 Overlay
    // 之前已经跑完，Overlay（如武器相机）再应用一次曝光不会叠加，
    // 只作用于自身内容，这样武器和场景的亮度变化才保持一致
    private void EnableCameraPostProcessing()
    {
        foreach (var camera in Camera.allCameras)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
        }
    }

    // 给场景中所有 AudioListener 补挂全局音量增益滤波器：
    // AudioListener.volume 上限是 1（原生响度），最大音量要超过
    // 原生响度只能在监听器的输出滤波层做软件增益（详见
    // MasterVolumeFilter）。场景切换后监听器是新的，需要重新补挂
    private void EnsureVolumeGainFilter()
    {
        foreach (AudioListener listener in FindObjectsOfType<AudioListener>())
        {
            if (listener.GetComponent<MasterVolumeFilter>() == null)
            {
                listener.gameObject.AddComponent<MasterVolumeFilter>();
            }
        }
    }
}
