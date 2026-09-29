using UnityEngine;

// 踏板触发器转发组件：由 PrologueController 在运行时自动挂载到
// SteppingStool 及其所有触发器碰撞体所在物体上，玩家进出触发器时
// 把事件转发给 PrologueController，这样 PrologueController 本体
// 可以挂在场景任意空物体上管理线性流程。
public class PrologueStoolRelay : MonoBehaviour
{
    [HideInInspector]
    public PrologueController controller;

    private void OnTriggerEnter(Collider other)
    {
        if (controller != null)
        {
            controller.OnStoolTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller != null)
        {
            controller.OnStoolTriggerExit(other);
        }
    }
}
