using UnityEngine;

public class CameraRecoil : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private Transform mainCamera;

    [SerializeField]
    private Transform weaponCamera;

    [Header("Recoil Defaults")]
    [SerializeField, Min(0f)]
    [Tooltip("武器未单独配置时，每发向上抬升的角度")]
    private float kickPitch = 0.5f;

    [SerializeField, Min(0f)]
    [Tooltip("武器未单独配置时，每发水平随机偏移的最大角度")]
    private float kickYaw = 0.12f;

    [SerializeField, Min(0f)]
    [Tooltip("武器未单独配置时，慢速单点的间隔阈值")]
    private float slowShotInterval = 0.25f;

    [Header("Visual Roll")]
    [SerializeField, Min(0f)]
    [Tooltip("每发镜头左右侧倾角度")]
    private float kickAngle = 2f;

    [SerializeField, Min(0f)]
    [Tooltip("镜头侧倾的绝对上限")]
    private float maxOffset = 5f;

    [SerializeField, Min(0.01f)]
    [Tooltip("侧倾恢复时间")]
    private float returnTime = 0.1f;

    private float currentOffset;
    private float offsetVelocity;
    private float lastShotTime = -Mathf.Infinity;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (mainCamera == null)
        {
            mainCamera = transform.Find("MainCamera");
        }

        if (weaponCamera == null)
        {
            weaponCamera = transform.Find("WeaponCamera");
        }
    }

    public void PlayRecoil(
        float weaponKickPitch,
        float weaponKickYaw,
        float weaponSlowShotInterval)
    {
        // 武器没有单独配置时（旧场景数据为0），使用这里的默认值
        if (weaponKickPitch <= 0f)
        {
            weaponKickPitch = kickPitch;
        }

        if (weaponKickYaw <= 0f)
        {
            weaponKickYaw = kickYaw;
        }

        if (weaponSlowShotInterval <= 0f)
        {
            weaponSlowShotInterval = slowShotInterval;
        }

        if (playerController != null)
        {
            float timeSinceLastShot = Time.time - lastShotTime;
            lastShotTime = Time.time;

            // 水平左右各一半范围随机
            float yawAmount = Random.Range(
                -weaponKickYaw,
                weaponKickYaw
            );

            if (timeSinceLastShot >= weaponSlowShotInterval)
            {
                // 慢速单点：临时后坐力，会自动恢复
                playerController.AddTemporaryRecoil(
                    weaponKickPitch,
                    yawAmount
                );
            }
            else
            {
                // 快速单点或全自动：永久后坐力，保持现有累积效果
                playerController.AddRecoil(
                    weaponKickPitch,
                    yawAmount
                );
            }
        }

        // 镜头左右侧倾的视觉晃动
        float direction = Random.value < 0.5f ? -1f : 1f;

        currentOffset += direction * kickAngle;
        currentOffset = Mathf.Clamp(
            currentOffset,
            -maxOffset,
            maxOffset
        );
    }

    private void LateUpdate()
    {
        // 侧倾平滑恢复原位
        currentOffset = Mathf.SmoothDamp(
            currentOffset,
            0f,
            ref offsetVelocity,
            returnTime
        );

        if (mainCamera == null || weaponCamera == null)
        {
            return;
        }

        // 在相机最终俯仰角的基础上叠加左右侧倾
        Quaternion recoilRotation =
            Quaternion.Euler(0f, 0f, currentOffset);

        Quaternion baseRotation = mainCamera.localRotation;

        mainCamera.localRotation =
            baseRotation * recoilRotation;

        // 两个相机保持同步，避免武器视角和主视角错位
        weaponCamera.rotation = mainCamera.rotation;
    }
}
