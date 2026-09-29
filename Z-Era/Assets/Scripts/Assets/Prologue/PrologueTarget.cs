using System;
using UnityEngine;

// 训练场靶子的命中计数器：挂在靶子上（或其父级），
// 由 BulletHandle 在子弹命中时调用 RegisterHit()：
// - 普通命中累计到需求次数后通过事件通知 PrologueController
//   驱动靶子倒下（tk02 击倒靶子）；
// - 命中头部碰撞体（可选拖拽）时触发独立事件，
//   供 PrologueController 统计爆头进度（tk03）。
public class PrologueTarget : MonoBehaviour
{
    [Tooltip("倒下需要的子弹命中次数（tk02 击倒靶子用）")]
    public int requiredHits = 5;

    [Tooltip("头部碰撞体：命中它时触发 OnHeadHit 事件（tk03 爆头统计用），留空则不检测")]
    public Collider headCollider;

    // 命中次数达到需求时触发（只触发一次）。
    public event Action OnRequiredHitsReached;

    // 命中头部碰撞体时触发（每次命中触发一次）。
    public event Action OnHeadHit;

    // 是否统计命中：靶子立起后由 PrologueController 开启，
    // 避免靶子倒地期间被乱枪提前计入次数。
    public bool countingEnabled;

    private int hitCount;
    private bool reached;

    // 由 BulletHandle 在子弹命中时调用，传入被命中的碰撞体。
    public void RegisterHit(Collider hitCollider)
    {
        // 爆头检测：与头部碰撞体完全一致才算爆头
        if (countingEnabled &&
            headCollider != null &&
            hitCollider == headCollider)
        {
            OnHeadHit?.Invoke();
        }

        if (!countingEnabled || reached)
        {
            return;
        }

        hitCount++;

        if (hitCount >= requiredHits)
        {
            reached = true;
            OnRequiredHitsReached?.Invoke();
        }
    }

    // 重置计数（线性关卡重开时可用）。
    public void ResetCounter()
    {
        hitCount = 0;
        reached = false;
    }
}
