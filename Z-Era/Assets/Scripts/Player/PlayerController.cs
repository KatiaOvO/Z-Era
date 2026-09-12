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
    #endregion

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        weaponCam = transform.Find("WeaponCamera")?.GetComponent<Camera>();
        weaponHolder = transform.Find("WeaponHolder");
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
        // 水平旋转Player
        transform.Rotate(Vector3.up * mouseX);
        // 垂直旋转Camera
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -maxLookAngle, maxLookAngle);
        cam.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        // 武器相机完全同步主相机
        weaponCam.transform.position = cam.transform.position;
        weaponCam.transform.rotation = cam.transform.rotation;
    }
}
