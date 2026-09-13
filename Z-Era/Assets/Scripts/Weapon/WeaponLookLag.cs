using UnityEngine;

[DefaultExecutionOrder(100)]
public class WeaponLookLag : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform weaponCamera;

    [Header("Lag Settings")]
    [SerializeField, Min(0.01f)]
    [Tooltip("武器追上相机旋转的速度，越大越快")]
    private float followSpeed = 15f;

    [SerializeField, Min(0f)]
    [Tooltip("最大允许落后的角度")]
    private float maxLagAngle = 3f;

    private Quaternion initialLocalRotation;
    private Quaternion currentWorldRotation;

    private void Awake()
    {
        if (weaponCamera == null)
        {
            weaponCamera = transform.parent;
        }

        initialLocalRotation = transform.localRotation;
        currentWorldRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (weaponCamera == null)
        {
            return;
        }

        // 武器最终应该达到的相机旋转
        Quaternion targetWorldRotation =
            weaponCamera.rotation * initialLocalRotation;

        // 使用脚本缓存的旋转计算滞后，而不是读取已经跟随父物体的 Transform
        float currentLag = Quaternion.Angle(
            currentWorldRotation,
            targetWorldRotation
        );

        if (currentLag > maxLagAngle)
        {
            // 限制最大滞后角度
            currentWorldRotation = Quaternion.RotateTowards(
                targetWorldRotation,
                currentWorldRotation,
                maxLagAngle
            );
        }
        else
        {
            // 平滑追上相机
            float interpolation =
                1f - Mathf.Exp(-followSpeed * Time.deltaTime);

            currentWorldRotation = Quaternion.Slerp(
                currentWorldRotation,
                targetWorldRotation,
                interpolation
            );
        }

        transform.rotation = currentWorldRotation;
    }
}