using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ZombieAnimationVariants : MonoBehaviour
{
    /// <summary>
    /// 表示一种动作类别，例如 Idle、Walk、Run 或 Attack。
    /// 
    /// originalClip：
    /// Animator Controller 中该状态原本引用的动画，作为 Override 的替换键。
    /// 
    /// variants：
    /// 运行时可以从中随机选择的动画集合。
    /// </summary>
    [Serializable]
    public class VariantGroup
    {
        [Tooltip("动作类别名称，只用于检查器中区分不同动画组")]
        public string groupName;

        [Tooltip("Animator Controller 中该状态原本使用的动画")]
        public AnimationClip originalClip;

        [Tooltip("该动作类别可以随机选择的动画")]
        public AnimationClip[] variants;
    }

    [Header("Animation Groups")]
    [Tooltip("按 Idle、Walk、Run、Attack 分别配置动画组")]
    [SerializeField]
    private VariantGroup[] groups;

    [Tooltip("每次对象启用时重新随机。使用对象池复用 Zombie 时建议开启")]
    [SerializeField]
    private bool randomizeOnEnable = true;

    [Tooltip("随机完成后重置 Animator，确保默认 Idle 状态也使用随机到的动画")]
    [SerializeField]
    private bool rebindAfterRandomize = true;

    private Animator animator;

    // 每只 Zombie 都拥有自己的 AnimatorOverrideController。
    // 它只影响当前 Zombie 实例，不会修改项目中的共享 Animator Controller。
    private AnimatorOverrideController overrideController;

    public AnimatorOverrideController OverrideController =>
        overrideController;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Awake 中先建立当前实例自己的 Override Controller。
        // OnEnable 随后可以在这份 Override 上执行随机替换。
        InitializeOverrideController();
    }

    private void OnEnable()
    {
        // Zombie 首次生成或从对象池重新启用时，重新随机整套动画。
        if (randomizeOnEnable)
        {
            RandomizeVariants();
        }
    }

    /// <summary>
    /// 为每个动作类别重新随机选择一个动画。
    /// 
    /// 随机结果会一直保存在当前 Zombie 的 Override Controller 中，
    /// 所以 Zombie 后续反复进入 Idle、Walk、Run 或 Attack 时，
    /// 都会继续使用本次随机到的版本，除非再次调用此方法。
    /// </summary>
    public void RandomizeVariants()
    {
        if (animator == null || overrideController == null)
        {
            return;
        }

        if (groups == null || groups.Length == 0)
        {
            Debug.LogWarning(
                "ZombieAnimationVariants：没有配置动画组。",
                this
            );
            return;
        }

        foreach (VariantGroup group in groups)
        {
            // 没有配置完整的分组直接跳过。
            // 这样可以只随机配置好的动作类别，不影响其他 Animator 状态。
            if (group == null ||
                group.originalClip == null ||
                group.variants == null ||
                group.variants.Length == 0)
            {
                continue;
            }

            AnimationClip selectedClip =
                GetRandomVariant(group.variants);

            if (selectedClip == null)
            {
                continue;
            }

            // AnimatorOverrideController 使用“原始动画”作为键。
            // 这里相当于告诉 Animator：
            // “凡是原本要播放 originalClip 的状态，现在改为播放 selectedClip。”
            overrideController[group.originalClip] = selectedClip;
        }

        if (rebindAfterRandomize)
        {
            // Animator 可能已经进入默认 Idle 状态。
            // Rebind 会重新初始化 Animator 状态机，让当前实例立即读取新的动画覆盖。
            animator.Rebind();

            // 立即推进一步，使默认状态在重新绑定后马上生效，
            // 避免第一帧仍引用重新绑定前的动画状态。
            animator.Update(0f);
        }
    }

    /// <summary>
    /// 从动画数组中随机选择一个非空动画。
    /// 
    /// 不使用简单的 Random.Range(0, variants.Length)，
    /// 是因为数组中可能存在没有填写的空引用。
    /// 当前实现只会在有效动画之间均匀随机。
    /// </summary>
    private AnimationClip GetRandomVariant(AnimationClip[] variants)
    {
        int validCount = 0;

        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        // 在 0 到 validCount - 1 之间生成随机下标。
        int randomIndex =
            UnityEngine.Random.Range(0, validCount);

        // 将“有效动画中的随机编号”转换为数组中的实际下标。
        // 例如数组为 [null, Walk01, Walk02]，
        // randomIndex 为 1 时最终应返回 Walk02。
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] == null)
            {
                continue;
            }

            if (randomIndex == 0)
            {
                return variants[i];
            }

            randomIndex--;
        }

        return null;
    }

    /// <summary>
    /// 初始化当前 Zombie 实例自己的 AnimatorOverrideController。
    /// 
    /// 原 Animator 可能直接使用 AnimatorController，
    /// 也可能已经使用过 AnimatorOverrideController。
    /// 两种情况都需要兼容。
    /// </summary>
    private void InitializeOverrideController()
    {
        if (animator == null)
        {
            return;
        }

        RuntimeAnimatorController currentController =
            animator.runtimeAnimatorController;

        if (currentController == null)
        {
            Debug.LogError(
                "ZombieAnimationVariants：Animator 没有设置 Animator Controller。",
                this
            );
            return;
        }

        RuntimeAnimatorController baseController = currentController;

        AnimatorOverrideController existingOverride =
            currentController as AnimatorOverrideController;

        // 如果原本已经是 Override Controller，
        // 需要找到它下面的基础 Controller，再创建新的实例级 Override。
        if (existingOverride != null)
        {
            baseController = existingOverride.runtimeAnimatorController;
        }

        if (baseController == null)
        {
            Debug.LogError(
                "ZombieAnimationVariants：找不到基础 Animator Controller。",
                this
            );
            return;
        }

        // 必须传入基础 Controller，而不是直接复用原来的 Controller 资源。
        // 这样每只 Zombie 才会得到独立的动画映射。
        overrideController =
            new AnimatorOverrideController(baseController);

        // 如果原先已经存在 Override，先复制已有映射，
        // 避免创建新 Override 后丢失之前配置的动画替换。
        if (existingOverride != null)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();

            existingOverride.GetOverrides(overrides);
            overrideController.ApplyOverrides(overrides);
        }

        // 将当前 Zombie 的 Animator 切换到独立 Override Controller。
        animator.runtimeAnimatorController = overrideController;
    }
}