using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    #region 组件

    private CharacterController characterController;
    private Camera cam;
    private Camera weaponCam;
    private Transform weaponHolder;
    private AudioSource audioSource;

    #endregion

    #region 角色属性

    public float walkSpeed = 5.0f;
    public float gravity = -9.81f;

    private float verticalVelocity = 0f;

    public AudioClip walkSound;

    #endregion

    #region 脚步玩法噪声

    [Header("脚步玩法噪声")]
    [Tooltip("玩家身上的玩法噪声发射器，留空时自动从当前物体获取")]
    [SerializeField]
    private NoiseEmitter noiseEmitter;

    [Tooltip("每隔多长时间发出一次脚步玩法噪声")]
    [SerializeField, Min(0.05f)]
    private float footstepNoiseInterval = 0.5f;

    [Tooltip("脚步玩法噪声的听觉半径")]
    [SerializeField, Min(0f)]
    private float footstepNoiseRadius = 6f;

    [Tooltip("脚步玩法噪声的优先级")]
    [SerializeField, Min(0f)]
    private float footstepNoisePriority = 1f;

    [Tooltip("每次脚步增加的怒气值")]
    [SerializeField, Min(0f)]
    private float footstepAngerValue = 4f;

    private float nextFootstepNoiseTime;

    #endregion

    #region 相机参数

    public float mouseSensitivity = 2.0f;
    public float maxLookAngle = 80.0f;

    [Tooltip("是否锁定移动（剧情演出用）：锁定时移动输入清零，视角与射击不受影响")]
    public bool movementLocked;

    private float rotationX = 0.0f;

    [Header("后坐力恢复")]
    [SerializeField, Min(0.01f)]
    private float temporaryRecoilReturnTime = 0.2f;

    private float temporaryRecoilPitch;
    private float temporaryRecoilYaw;
    private float temporaryRecoilPitchVelocity;
    private float temporaryRecoilYawVelocity;
    private bool isFiring;

    #endregion

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        weaponCam = transform.Find("WeaponCamera")?.GetComponent<Camera>();

        Cursor.lockState = CursorLockMode.Locked;

        audioSource = GetComponent<AudioSource>();
        audioSource.clip = walkSound;

        if (noiseEmitter == null)
        {
            noiseEmitter = GetComponent<NoiseEmitter>();
        }

        IgnoreSelfCollisions();
    }

    // 玩家身上除 CharacterController 外的碰撞体（武器模型等，
    // 含未激活的子物体）全部与其忽略碰撞：朝下开枪时武器模型
    // 插进脚下地面，CharacterController 的自动出穿会把玩家推开。
    // 运行时拾取更换的武器由 WeaponController.OnEnable 兜底补挂。
    private void IgnoreSelfCollisions()
    {
        foreach (Collider collider in
            GetComponentsInChildren<Collider>(true))
        {
            if (collider != characterController)
            {
                Physics.IgnoreCollision(
                    characterController,
                    collider,
                    true
                );
            }
        }
    }

    void Update()
    {
        PlayerCameraController();
        PlayerMoveController();
    }

    // 进入锁定瞬间记录的位置，锁定期间每帧钉回：
    // 朝下开枪时武器模型会与地面/踏板重叠，CharacterController
    // 的自动出穿会在 Move 里把玩家推开，仅清输入挡不住这种位移
    private Vector3 lockedPosition;
    private bool positionFrozen;

    private void PlayerMoveController()
    {
        // 锁定移动：除输入清零外直接钉死位置，保证锁定期间
        // 玩家无法通过任何方式移动（出穿、外力一律无效）
        if (movementLocked)
        {
            if (!positionFrozen)
            {
                lockedPosition = transform.position;
                positionFrozen = true;
            }

            transform.position = lockedPosition;
            verticalVelocity = 0f;

            // 行走途中被锁定时可能还在播放脚步声，
            // 立即停止而不是等这一段音频自然播完
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            return;
        }

        positionFrozen = false;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        bool hasMoveInput = h != 0f || v != 0f;

        Vector3 moveDirection =
            (transform.right * h + transform.forward * v).normalized;

        if (characterController.isGrounded)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 move =
            moveDirection * walkSpeed + Vector3.up * verticalVelocity;

        Vector3 positionBeforeMove = transform.position;

        characterController.Move(move * Time.deltaTime);

        Vector3 horizontalMovement =
            transform.position - positionBeforeMove;
        horizontalMovement.y = 0f;

        bool isActuallyWalking =
            hasMoveInput &&
            characterController.isGrounded &&
            horizontalMovement.sqrMagnitude > 0.000001f;

        if (isActuallyWalking)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }

            UpdateFootstepNoise();
        }
        else
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            nextFootstepNoiseTime = Time.time;
        }
    }

    private void UpdateFootstepNoise()
    {
        if (Time.time < nextFootstepNoiseTime)
        {
            return;
        }

        nextFootstepNoiseTime = Time.time + footstepNoiseInterval;

        if (noiseEmitter == null)
        {
            return;
        }

        noiseEmitter.EmitFootstep(
            transform.position,
            footstepNoiseRadius,
            footstepNoisePriority,
            footstepAngerValue
        );
    }

    private void PlayerCameraController()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        rotationX -= mouseY;
        rotationX = Mathf.Clamp(
            rotationX,
            -maxLookAngle,
            maxLookAngle
        );

        if (!isFiring)
        {
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
        }

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

        weaponCam.transform.position = cam.transform.position;
        weaponCam.transform.rotation = cam.transform.rotation;
    }

    public void AddTemporaryRecoil(
        float pitchAmount,
        float yawAmount,
        float returnTime)
    {
        temporaryRecoilPitch += pitchAmount;
        temporaryRecoilYaw += yawAmount;
        temporaryRecoilReturnTime = returnTime;
    }

    public void SetFiring(bool firing)
    {
        isFiring = firing;
    }
}