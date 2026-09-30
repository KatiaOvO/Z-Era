using System;
using UnityEngine;

// 训练场靶子的命中计数器：挂在靶子上（或其父级），
// 由 BulletHandle 在子弹命中时调用 RegisterHit()：
// - 普通命中累计到需求次数后通过事件通知 PrologueController
//   驱动靶子倒下（tk02 击倒靶子）；
// - 命中头部碰撞体（可选拖拽）时触发独立事件，
//   供 PrologueController 统计爆头进度（tk03）。
// 同时实现 IDamageable：匕首攻击（tk08）经 WeaponController 的
// 通用伤害链路命中靶子，一刀即倒；与子弹计数完全隔离。
public class PrologueTarget : MonoBehaviour,
    IDamageable
{
    [Tooltip("倒下需要的子弹命中次数（tk02 击倒靶子用）")]
    public int requiredHits = 5;

    [Tooltip("头部碰撞体：命中它时触发 OnHeadHit 事件（tk03 爆头统计、tk06 爆头击倒用），留空则不检测")]
    public Collider headCollider;

    // 命中次数达到需求时触发（只触发一次）。
    public event Action OnRequiredHitsReached;

    // 命中头部碰撞体时触发（每次命中触发一次）。
    public event Action OnHeadHit;

    // 命中任意碰撞体时触发（每次命中触发一次，参数为子弹的
    // 长按轮次编号），供 tk07 统计单轮长按内的命中数。
    public event Action<int> OnBurstHit;

    // 是否统计命中：靶子立起后由 PrologueController 开启，
    // 避免靶子倒地期间被乱枪提前计入次数。
    public bool countingEnabled;

    [Tooltip("是否统计匕首命中（tk08 用）：由 PrologueController 在任务阶段开启，开启后匕首一刀即倒；子弹命中走 countingEnabled，与此互不影响")]
    public bool knifeCountingEnabled;

    private int hitCount;
    private bool reached;

    // 该靶已被匕首击倒，防止重复触发。
    private bool knifeDown;

    // 匕首命中（一刀即倒）时触发，供 PrologueController 统计进度。
    public event Action OnKnifeHit;

    // 由 BulletHandle 在子弹命中时调用，传入被命中的碰撞体
    // 与子弹所属的长按轮次编号。
    public void RegisterHit(Collider hitCollider, int burstId = 0)
    {
        // 任意命中检测：计数开启时每次命中都通知（先于 reached
        // 门控，命中数达到 requiredHits 后仍继续触发），
        // tk07 按长按轮次编号归属命中
        if (countingEnabled)
        {
            OnBurstHit?.Invoke(burstId);
        }

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

    // IDamageable：仅响应匕首攻击（子弹命中已由 RegisterHit
    // 统计，这里再计数会双计）。一刀即倒，倒过即不再触发。
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!knifeCountingEnabled ||
            knifeDown ||
            damageInfo.source != DamageSource.KnifeAttack)
        {
            return;
        }

        knifeDown = true;
        knifeCountingEnabled = false;

        OnKnifeHit?.Invoke();
    }
}
