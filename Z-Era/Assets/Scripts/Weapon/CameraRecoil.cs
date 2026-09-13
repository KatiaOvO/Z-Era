using UnityEngine;

public class CameraRecoil : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField]
    private Transform mainCamera;

    [SerializeField]
    private Transform weaponCamera;

    [Header("Recoil Settings")]
    [SerializeField, Min(0f)]
    [Tooltip("每次射击镜头立即增加的横向偏移量")]
    private float kickAngle = 2f;

    [SerializeField, Min(0f)]
    [Tooltip("镜头横向偏移的绝对上限")]
    private float maxOffset = 5f;

    [SerializeField, Min(0.01f)]
    [Tooltip("平滑恢复时间")]
    private float returnTime = 0.1f;

    private float currentOffset;
    private float offsetVelocity;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = transform.Find("MainCamera");
        }

        if (weaponCamera == null)
        {
            weaponCamera = transform.Find("WeaponCamera");
        }
    }

    public void PlayRecoil()
    {
        // 50% 向左，50% 向右
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
        // 平滑恢复原位
        currentOffset = Mathf.SmoothDamp(
            currentOffset,
            0f,
            ref offsetVelocity,
            returnTime
        );

        // 这里绕 Z 轴旋转，表现为镜头左右侧倾
        Quaternion recoilRotation =
            Quaternion.Euler(0f, 0f, currentOffset);

        // PlayerController 每帧会先设置基础俯仰角
        // 再在基础旋转上叠加后坐力偏移
        Quaternion baseRotation = mainCamera.localRotation;

        mainCamera.localRotation =
            baseRotation * recoilRotation;

        // 两个相机保持同步，避免武器视角和主视角错位
        weaponCamera.rotation = mainCamera.rotation;
    }
}