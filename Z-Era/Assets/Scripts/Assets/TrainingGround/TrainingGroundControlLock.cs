using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 训练场玩家控制锁存工具：禁用玩家及其武器上的所有控制组件
/// （PlayerController、WeaponController、WeaponEffects、
/// PickupController、CameraRecoil——与 DialogueRunner 的控制锁存
/// 模式一致），需要恢复时按记录原样还原。
/// 静态工具类，不需要场景物体；视角旋转与开火的输入都在这些
/// 组件内部直接读取，只显示鼠标挡不住它们，必须停用组件。
/// </summary>
public static class TrainingGroundControlLock
{
    private static readonly List<Behaviour> disabledControls =
        new List<Behaviour>();

    private static PlayerController playerController;

    // 禁用玩家控制。重复调用安全：已禁用的组件不会重复记录
    public static void Lock()
    {
        // 清理已销毁的残留记录（例如锁定后直接切换了场景）
        disabledControls.RemoveAll(behaviour => behaviour == null);

        if (playerController == null)
        {
            // 静态类不继承 UnityEngine.Object，需要显式通过 Object 调用
            playerController = Object.FindObjectOfType<PlayerController>();
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

    // 恢复所有被 Lock 禁用的组件
    public static void Restore()
    {
        foreach (Behaviour behaviour in disabledControls)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledControls.Clear();
    }

    // 只记录并停用当前处于启用状态的组件，恢复时按记录还原
    private static void AddDisabled(Behaviour behaviour)
    {
        if (behaviour != null && behaviour.enabled)
        {
            behaviour.enabled = false;
            disabledControls.Add(behaviour);
        }
    }
}
