using System.Collections.Generic;
using Migration.UI;
using UnityEngine;

/// <summary>
/// 训练场退出确认：挂在 QuitRoot 上（其下为挂了触发器的 3D
/// TextMeshPro 文字，如 YesText）。子弹命中文字后启用 ConfirmQuit
/// Canvas，并把画布下挂载了 UI Dissolve Image 的背景遮罩
/// ConfirmTextMaskBgImage 溶解消失。
/// 子弹命中当帧即被对象池回收，物理触发器事件不一定触发，因此这里
/// 订阅 BulletHandle 的 BulletLaunched/BulletSettled 静态事件，用
/// "发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短射线，
/// 找出子弹实际命中的碰撞体。
/// 只订阅事件、只开关画布与调用公开的溶解接口，不修改任何根代码；
/// 场景卸载时反订阅事件，不影响其他场景。
/// </summary>
public class TrainingGroundQuitConfirm : MonoBehaviour
{
    [Header("确认界面")]

    [Tooltip("射击确认文字后启用的 ConfirmQuit Canvas")]
    [SerializeField]
    private GameObject confirmCanvas;

    [Tooltip("ConfirmQuit Canvas 下的背景遮罩 Image（挂载 UI Dissolve " +
        "Image）：画布启用后播放溶解消失；留空则自动在画布子物体中查找")]
    [SerializeField]
    private UIDissolveImage confirmMaskBg;

    private readonly Dictionary<BulletHandle, Vector3> launchPositions =
        new Dictionary<BulletHandle, Vector3>();

    // 射击确认文字前的鼠标状态，关闭确认界面后恢复
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool hasSavedCursorState;

    // 确认界面打开期间被禁用的玩家控制组件，关闭后按原样恢复
    // （与 DialogueRunner.LockGameplay 相同的控制锁存模式：
    // 视角旋转与开火的输入都在这些组件内部直接读取，
    // 只显示鼠标挡不住它们，必须停用组件）
    private readonly List<Behaviour> disabledControls =
        new List<Behaviour>();

    private PlayerController playerController;

    // 落点反查射线的参数：子弹停在命中点表面，稍微沿反方向退一点
    // 再沿飞行方向打短射线即可命中原表面
    private const float SettleRayBackOffset = 0.05f;
    private const float SettleRayDistance = 0.15f;

    // 落点反查时排除的图层名，与 BulletHandle 默认排除的图层保持一致，
    // 避免反查射线命中子弹本身被忽略的玩家/武器碰撞体；
    // 图层号在 OnEnable 中查询（Unity API 不允许在字段初始化器中调用）
    private static readonly string[] SettleRayIgnoredLayerNames =
    {
        "Bullet",
        "Player",
        "Weapon",
        "Ignore Raycast"
    };

    private int settleRayIgnoreMask;

    // 溶解完毕（完全消失）后遮罩不应再挡住后面的 UI 按钮，
    // 每帧按溶解状态同步其 Raycast Target：
    // 溶解中/完全显示时勾选，溶解完毕后取消勾选
    private void Update()
    {
        if (confirmMaskBg == null || confirmMaskBg.graphic == null)
        {
            return;
        }

        bool shouldBlockRaycast = !confirmMaskBg.isHideComplete;

        if (confirmMaskBg.graphic.raycastTarget != shouldBlockRaycast)
        {
            confirmMaskBg.graphic.raycastTarget = shouldBlockRaycast;
        }
    }

    private void OnEnable()
    {
        settleRayIgnoreMask = 0;
        foreach (string layerName in SettleRayIgnoredLayerNames)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer >= 0)
            {
                settleRayIgnoreMask |= 1 << layer;
            }
        }

        BulletHandle.BulletLaunched += OnBulletLaunched;
        BulletHandle.BulletSettled += OnBulletSettled;
    }

    private void OnDisable()
    {
        BulletHandle.BulletLaunched -= OnBulletLaunched;
        BulletHandle.BulletSettled -= OnBulletSettled;

        launchPositions.Clear();
    }

    private void OnBulletLaunched(BulletHandle bullet)
    {
        if (bullet == null)
        {
            return;
        }

        launchPositions[bullet] = bullet.transform.position;
    }

    private void OnBulletSettled(BulletHandle bullet)
    {
        if (bullet == null ||
            !launchPositions.TryGetValue(
                bullet,
                out Vector3 startPosition))
        {
            return;
        }

        launchPositions.Remove(bullet);

        // 超时回收的子弹没有命中任何表面，落点反查打不到东西时会
        // 自然忽略，这里无需额外区分
        TryConfirmAtHit(
            bullet.transform.position,
            startPosition);
    }

    // 用"发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短
    // 射线，找出子弹实际命中的碰撞体；命中 QuitRoot 的直接子物体
    // （YesText）时打开确认界面
    private void TryConfirmAtHit(
        Vector3 settlePosition,
        Vector3 launchPosition)
    {
        Vector3 direction = settlePosition - launchPosition;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        if (!Physics.Raycast(
            settlePosition - direction * SettleRayBackOffset,
            direction,
            out RaycastHit hit,
            SettleRayDistance,
            ~settleRayIgnoreMask,
            QueryTriggerInteraction.Collide))
        {
            return;
        }

        Transform display =
            GetDirectChildUnderRoot(hit.collider.transform);

        if (display != null)
        {
            ConfirmQuit();
        }
    }

    // 从命中的碰撞体向上找到 QuitRoot 的直接子物体
    // （即文字物体本体，命中可能发生在文字网格的子碰撞体上）
    private Transform GetDirectChildUnderRoot(Transform current)
    {
        while (current.parent != null &&
               current.parent != transform)
        {
            current = current.parent;
        }

        return current.parent == transform ? current : null;
    }

    private void ConfirmQuit()
    {
        if (confirmCanvas == null)
        {
            Debug.LogError(
                "ConfirmQuit Canvas 未赋值，无法打开确认界面。",
                this
            );
            return;
        }

        confirmCanvas.SetActive(true);

        // 释放鼠标（保存进入训练场时被锁定的状态，便于之后恢复），
        // 否则无法点击确认界面上的按钮
        if (!hasSavedCursorState)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            hasSavedCursorState = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 停用玩家控制组件：确认界面打开期间不能旋转视角、
        // 不能开火、不能移动，只能操作 UI
        LockPlayerControl();

        // 未拖拽赋值时在画布子物体中自动查找
        if (confirmMaskBg == null)
        {
            confirmMaskBg =
                confirmCanvas.GetComponentInChildren<UIDissolveImage>(
                    true
                );
        }

        if (confirmMaskBg == null)
        {
            Debug.LogWarning(
                "ConfirmQuit Canvas 下没有找到 UI Dissolve Image，" +
                "背景遮罩不会播放溶解消失。",
                this
            );
            return;
        }

        // 先瞬时置为完全显示，再播放溶解消失：
        // 上次的溶解动画结束时遮罩处于完全消失状态，
        // 直接 Hide 不会有任何动画，重复射击也能看到完整溶解
        confirmMaskBg.SetVisible(true, true);
        confirmMaskBg.Hide();
    }

    // 恢复射击确认前的鼠标锁定状态与玩家控制。可挂到确认界面的
    // 关闭按钮或 UI Dissolve Image 的 On Hidden 事件上，在界面
    // 关闭时调用
    public void RestoreCursor()
    {
        if (hasSavedCursorState)
        {
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
            hasSavedCursorState = false;
        }

        foreach (Behaviour behaviour in disabledControls)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledControls.Clear();
    }

    // 禁用玩家及其武器上的所有控制组件（与 DialogueRunner 的
    // 控制锁存模式一致），确认界面关闭时按原样恢复
    private void LockPlayerControl()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        if (playerController == null)
        {
            return;
        }

        AddDisabled(playerController);

        Transform controlRoot = playerController.transform;

        foreach (WeaponController controller in
            controlRoot.GetComponentsInChildren<WeaponController>(true))
        {
            AddDisabled(controller);
        }

        foreach (WeaponEffects effects in
            controlRoot.GetComponentsInChildren<WeaponEffects>(true))
        {
            AddDisabled(effects);
        }

        foreach (PickupController pickup in
            controlRoot.GetComponentsInChildren<PickupController>(true))
        {
            AddDisabled(pickup);
        }

        AddDisabled(controlRoot.GetComponent<CameraRecoil>());
    }

    // 只记录并停用当前处于启用状态的组件，恢复时按记录还原
    private void AddDisabled(Behaviour behaviour)
    {
        if (behaviour != null && behaviour.enabled)
        {
            behaviour.enabled = false;
            disabledControls.Add(behaviour);
        }
    }
}
