using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    #region 组件
    [Tooltip("玩家刚体")]
    private CharacterController characterController;
    [Tooltip("玩家相机")]
    private Camera cam;
    [Tooltip("武器相机")]
    private Camera weaponCam;
    [Tooltip("武器挂载点")]
    private Transform weaponHolder;
    [Tooltip("音源")]
    private AudioSource audioSource;
    #endregion

    #region 角色属性
    [Tooltip("行走速度")]
    public float walkSpeed = 5.0f;
    [Tooltip("重力")]
    public float gravity = -9.81f;
    private float verticalVelocity = 0f;
    [Tooltip("行走音效")]
    public AudioClip walkSound;
    #endregion

    #region 相机参数
    [Tooltip("鼠标灵敏度")]
    public float mouseSensitivity = 2.0f;
    [Tooltip("上下垂直方向的最大旋转角度")]
    public float maxLookAngle = 80.0f;
    [Tooltip("记录当前相机的垂直旋转角度")]
    private float rotationX = 0.0f;

    [Header("慢速单点后坐力恢复")]
    [SerializeField, Min(0.01f)]
    [Tooltip("慢速单点后坐力恢复的平滑时间")]
    private float temporaryRecoilReturnTime = 0.2f;

    private float temporaryRecoilPitch;
    private float temporaryRecoilYaw;
    private float temporaryRecoilPitchVelocity;
    private float temporaryRecoilYawVelocity;
    #endregion

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        weaponCam = transform.Find("WeaponCamera")?.GetComponent<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = walkSound;
    }

    void Update()
    {
        PlayerCameraController();
        PlayerMoveController();
    }

    // 方法：玩家移动
    private void PlayerMoveController()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool isMoving = (h != 0 || v != 0);
        Vector3 moveDirection = (transform.right * h + transform.forward * v).normalized;
        // 处理重力
        if (characterController.isGrounded)
        {
            verticalVelocity = -2f; // 保持贴地
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        // 组合水平和垂直移动
        Vector3 move = moveDirection * walkSpeed + Vector3.up * verticalVelocity;
        characterController.Move(move * Time.deltaTime);
        // 行走音效
        if (isMoving && characterController.isGrounded)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }

    // 方法：玩家相机视角
    private void PlayerCameraController()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 鼠标控制基础视角
        transform.Rotate(Vector3.up * mouseX);
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -maxLookAngle, maxLookAngle);

        // 临时后坐力平滑恢复到 0
        temporaryRecoilPitch = Mathf.SmoothDamp(
            temporaryRecoilPitch,
            0f,
            ref temporaryRecoilPitchVelocity,
            temporaryRecoilReturnTime
        );

        temporaryRecoilYaw = Mathf.SmoothDamp(
            temporaryRecoilYaw,
            0f,
            ref temporaryRecoilYawVelocity,
            temporaryRecoilReturnTime
        );

        // 最终相机角度 = 永久后坐力后的基础角度 + 临时后坐力
        float finalPitch = Mathf.Clamp(
            rotationX - temporaryRecoilPitch,
            -maxLookAngle,
            maxLookAngle
        );

        cam.transform.localRotation = Quaternion.Euler(
            finalPitch,
            temporaryRecoilYaw,
            0f
        );

        // 武器相机完全同步主相机
        weaponCam.transform.position = cam.transform.position;
        weaponCam.transform.rotation = cam.transform.rotation;
    }

    // 方法：添加后坐力（由 CameraRecoil 调用）
    public void AddRecoil(float pitchAmount, float yawAmount)
    {
        // 向上抬：rotationX 变小，视角永久改变
        rotationX -= pitchAmount;
        rotationX = Mathf.Clamp(rotationX, -maxLookAngle, maxLookAngle);

        // 水平方向：直接转动 Player，视角永久改变
        transform.Rotate(Vector3.up * yawAmount);
    }

    // 方法：添加会恢复的临时后坐力（慢速单点）
    public void AddTemporaryRecoil(float pitchAmount, float yawAmount)
    {
        temporaryRecoilPitch += pitchAmount;
        temporaryRecoilYaw += yawAmount;
    }
}
