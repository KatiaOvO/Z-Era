using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 训练场匕首靶：挂在 KnifeAttackTargets 空父物体上（每个直接子物体
/// 为一具靶子，需挂载 PrologueTarget 并勾选 Knife Counting Enabled）。
/// 匕首攻击命中一次，靶子局部旋转 X 由 0 倒下到 90，倒地保持
/// knifeStayDownDuration 秒后自动立起，可无限重复。
/// 注意 PrologueTarget 的匕首状态（knifeDown 等）是私有的、没有
/// 重置接口，因此每轮倒下后销毁旧计数器并按原配置重新挂载一个
/// 干净的（requiredHits、头部碰撞体等先捕获再还原）。
/// 只订阅事件、只开关场景内已有物体的组件，不修改任何根代码；
/// 其它场景没有本物体，不受影响。
/// </summary>
public class TrainingGroundKnifeTargets : MonoBehaviour
{
    [Header("表现")]

    [Tooltip("靶子倒地后到重新立起的等待时间（秒）")]
    [SerializeField, Min(0f)]
    private float knifeStayDownDuration = 2f;

    [Tooltip("靶子倒下/立起的旋转时长（秒）")]
    [SerializeField, Min(0.01f)]
    private float rotateDuration = 1f;

    // 单具匕首靶的运行时记录
    private class TargetEntry
    {
        public Transform Target;
        public Vector3 BaseLocalEuler;
        public int RequiredHits;
        public Collider HeadCollider;
        public bool CountingEnabled;
        public PrologueTarget Counter;
        public Coroutine CycleRoutine;
        public bool Cycling;
    }

    private readonly List<TargetEntry> entries =
        new List<TargetEntry>();

    private void Start()
    {
        foreach (Transform child in transform)
        {
            PrologueTarget counter = child.GetComponent<PrologueTarget>();

            if (counter == null)
            {
                Debug.LogWarning(
                    "匕首靶缺少 PrologueTarget 组件，跳过：" + child.name,
                    child
                );
                continue;
            }

            TargetEntry entry = new TargetEntry
            {
                Target = child,
                BaseLocalEuler = child.localEulerAngles,
                RequiredHits = counter.requiredHits,
                HeadCollider = counter.headCollider,
                CountingEnabled = counter.countingEnabled
            };

            // 初始计数器保留用户在检查器中的配置，仅订阅命中事件；
            // 倒下循环中才会换成重建的干净计数器
            entry.Counter = counter;
            entry.Counter.OnKnifeHit += () => KnockDown(entry);

            entries.Add(entry);
        }
    }

    private void OnDisable()
    {
        foreach (TargetEntry entry in entries)
        {
            if (entry.CycleRoutine != null)
            {
                StopCoroutine(entry.CycleRoutine);
            }
        }
    }

    // 挂载计数器并订阅匕首命中事件；
    // 使用 entry 中捕获的原配置（初始为用户检查器中的配置）
    private void AttachCounter(TargetEntry entry)
    {
        entry.Counter = entry.Target.gameObject.AddComponent<PrologueTarget>();
        entry.Counter.requiredHits = entry.RequiredHits;
        entry.Counter.headCollider = entry.HeadCollider;
        entry.Counter.countingEnabled = entry.CountingEnabled;
        entry.Counter.knifeCountingEnabled = true;

        entry.Counter.OnKnifeHit += () => KnockDown(entry);
    }

    // 匕首命中：倒下 → 倒地保持 → 换上干净计数器 → 立起
    private void KnockDown(TargetEntry entry)
    {
        if (entry.Cycling)
        {
            return;
        }

        entry.Cycling = true;

        // 倒下/倒地/立起期间关闭计数，避免该期间被再次命中
        // 白白消耗干净计数器的匕首击倒状态
        entry.Counter.countingEnabled = false;
        entry.Counter.knifeCountingEnabled = false;

        entry.CycleRoutine = StartCoroutine(
            KnockDownRoutine(entry)
        );
    }

    private IEnumerator KnockDownRoutine(TargetEntry entry)
    {
        yield return RotateTargetX(entry, 0f, 90f);

        // 完全倒地后换上干净计数器（knifeDown 私有无法重置），
        // 立起前保持计数关闭
        Destroy(entry.Counter);
        AttachCounter(entry);
        entry.Counter.knifeCountingEnabled = false;

        yield return new WaitForSeconds(knifeStayDownDuration);

        yield return RotateTargetX(entry, 90f, 0f);

        entry.Counter.countingEnabled = entry.CountingEnabled;
        entry.Counter.knifeCountingEnabled = true;

        entry.Cycling = false;
    }

    // 局部旋转 X 从 from 转到 to，其余欧拉角保持初始记录的基准值；
    // 靶子的轴心需在底部铰链处
    private IEnumerator RotateTargetX(
        TargetEntry entry,
        float fromX,
        float toX)
    {
        float time = 0f;

        while (time < rotateDuration)
        {
            // 靶子被销毁等异常情况直接终止
            if (entry.Target == null)
            {
                yield break;
            }

            time += Time.deltaTime;

            float x = Mathf.Lerp(
                fromX,
                toX,
                Mathf.Clamp01(time / rotateDuration)
            );

            entry.Target.localRotation = Quaternion.Euler(
                x,
                entry.BaseLocalEuler.y,
                entry.BaseLocalEuler.z
            );

            yield return null;
        }

        if (entry.Target == null)
        {
            yield break;
        }

        entry.Target.localRotation = Quaternion.Euler(
            toX,
            entry.BaseLocalEuler.y,
            entry.BaseLocalEuler.z
        );

        entry.CycleRoutine = null;
    }
}
