using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 射击镜头侧倾开关控件：挂在设置面板的 Toggle 勾选框上
/// （需有 Toggle 组件）。
/// 勾选 = 射击时镜头左右侧倾，取消勾选 = 关闭侧倾（后坐力抬枪
/// 保留），供晕 3D 的玩家使用。开关经 SettingsManager 实时应用
/// 并持久化，面板每次打开同步到当前保存值。
/// 点击勾选框时播放一次点击音效。
/// </summary>
[RequireComponent(typeof(Toggle))]
public class CameraRecoilToggle : MonoBehaviour
{
    [Header("点击音效")]

    [Tooltip("点击勾选框时播放的音效，留空则不播放")]
    [SerializeField]
    private AudioClip clickSound;

    [Tooltip("点击音效音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float clickSoundVolume = 1f;

    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
    }

    private void OnEnable()
    {
        // 用 SetIsOnWithoutNotify 避免同步初值时触发一次多余的保存
        toggle.SetIsOnWithoutNotify(
            SettingsManager.CameraRecoilEnabled);

        toggle.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable()
    {
        toggle.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(bool value)
    {
        PlayClickSound();

        SettingsManager.Instance.SetCameraRecoilEnabled(value);
    }

    // 与 SettingTabButton 相同：用独立的临时物体播放，
    // 音效独立存活到播完，不会被任何面板开关截断
    private void PlayClickSound()
    {
        if (clickSound == null)
        {
            return;
        }

        GameObject soundHost = new GameObject("ClickSound");
        AudioSource source = soundHost.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = clickSoundVolume;
        source.PlayOneShot(clickSound);
        Destroy(soundHost, clickSound.length);
    }
}
