using System.Collections.Generic;
using Migration.UI;
using UnityEngine;

/// <summary>
/// 训练场帮助界面触发器：挂在 HelpRoot 上（其下为挂了触发器的
/// 3D TextMeshPro 文字 HelpText）。子弹命中后启用 HelpInfo Canvas，
/// 释放鼠标并停用玩家控制组件（与退出确认界面相同的控制锁存模式），
/// 关闭画布时由 Help 画布上的关闭按钮调用 RestoreCursor 恢复。
/// 子弹命中当帧即被对象池回收，物理触发器事件不一定触发，因此这里
/// 订阅 BulletHandle 的 BulletLaunched/BulletSettled 静态事件，用
/// "发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短射线，
/// 找出子弹实际命中的碰撞体。
/// 只订阅事件、只开关画布与玩家控制组件，不修改任何根代码；
/// 场景卸载时反订阅事件，不影响其他场景。
/// </summary>
public class TrainingGroundHelpTrigger : MonoBehaviour
{
    [Header("引用")]

    [Tooltip("射击 HelpText 后启用的 HelpInfo Canvas")]
    [SerializeField]
    private GameObject helpCanvas;

    [Tooltip("HelpInfo Canvas 下的背景遮罩 Image（挂载 UI Dissolve " +
        "Image）：画布启用后播放溶解消失；留空则自动在画布子物体中查找")]
    [SerializeField]
    private UIDissolveImage helpMaskBg;

    private readonly Dictionary<BulletHandle, Vector3> launchPositions =
        new Dictionary<BulletHandle, Vector3>();

    // 射击确认文字前的鼠标状态，关闭帮助界面后恢复
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool hasSavedCursorState;

    // 帮助界面打开期间被禁用的玩家控制组件，关闭后按原样恢复
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

    // 背景遮罩的 Raycast Target 同步：溶解中/完全显示时勾选
    // （遮罩照常挡住射线），溶解完毕（完全消失）后取消勾选，
    // 不再挡住帮助界面内容的点击；再次打开时 SetVisible(true, true)
    // 瞬时显示，勾选会自动恢复
    private void Update()
    {
        if (helpMaskBg == null || helpMaskBg.graphic == null)
        {
            return;
        }

        bool shouldBlockRaycast = !helpMaskBg.isHideComplete;

        if (helpMaskBg.graphic.raycastTarget != shouldBlockRaycast)
        {
            helpMaskBg.graphic.raycastTarget = shouldBlockRaycast;
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

        // 管理器被禁用时恢复控制，避免玩家卡在失控状态
        RestoreCursor();
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

        TryOpenHelpAtHit(
            bullet.transform.position,
            startPosition);
    }

    // 用"发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短
    // 射线，找出子弹实际命中的碰撞体；命中 QuitRoot 的直接子物体
    // （HelpText）时打开帮助界面
    private void TryOpenHelpAtHit(
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
            OpenHelp();
        }
    }

    // 从命中的碰撞体向上找到 HelpRoot 的直接子物体
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

    private void OpenHelp()
    {
        if (helpCanvas == null)
        {
            Debug.LogError(
                "HelpInfo Canvas 未赋值，无法打开帮助界面。",
                this
            );
            return;
        }

        // 重复命中时界面已打开，不重复处理
        if (helpCanvas.activeSelf)
        {
            return;
        }

        helpCanvas.SetActive(true);

        // 背景遮罩溶解：先瞬时置为完全显示，再播放溶解消失——
        // 上次的溶解动画结束时遮罩处于完全消失状态，直接 Hide
        // 不会有任何动画，重复打开也能看到完整溶解
        if (helpMaskBg == null)
        {
            helpMaskBg =
                helpCanvas.GetComponentInChildren<UIDissolveImage>(
                    true
                );
        }

        if (helpMaskBg != null)
        {
            helpMaskBg.SetVisible(true, true);
            helpMaskBg.Hide();
        }

        // 释放鼠标（保存进入训练场时被锁定的状态，便于之后恢复），
        // 否则无法点击帮助界面上的按钮
        if (!hasSavedCursorState)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            hasSavedCursorState = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 停用玩家控制组件：帮助界面打开期间不能旋转视角、
        // 不能开火、不能移动，只能操作 UI
        LockPlayerControl();
    }

    // 恢复射击确认前的鼠标锁定状态与玩家控制。由 Help 画布上的
    // 关闭按钮（TrainingGroundHelpCloseButton）在关闭时调用
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
    // 控制锁存模式一致），界面关闭时按原样恢复
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
