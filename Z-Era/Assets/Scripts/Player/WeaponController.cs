using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    #region 组件
    [Tooltip("武器动画器")]
    private Animator animator;
    #endregion

    #region 判断参数
    [Tooltip("行走判断参数")]
    private bool isWalk;
    [Tooltip("腰射开火判断参数")]
    private bool isFire;
    [Tooltip("检视判断参数")]
    private bool isInspect;
    [Tooltip("匕首攻击判断参数")]
    private bool isKnifeAttack;
    [Tooltip("是否能开火判断参数")]
    private bool isCanFire;
    [Tooltip("弹匣是否完全打空")]
    private bool isMagazineEmpty;
    #endregion

    #region 子弹
    [Tooltip("当前备弹子弹数")]
    private int carriedAmmo = 120;
    [Tooltip("当前弹匣子弹数")]
    private int magazineAmmo = 20;
    [Tooltip("枪支弹匣容量")]
    private int magazineSize = 20;
    #endregion

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        ParameterJudgment();
        AnimatorController();
        // test：动画测试
        if (Input.GetKeyDown(KeyCode.P)) animator.Play("Test", 1, 0.0f);
    }

    // 方法：参数判断
    private void ParameterJudgment()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        // 判断isWalking：按下移动键则为正在行走
        isWalk = h != 0 || v != 0;
        // 判断isHipFire：按下左键则为开火
        isFire = Input.GetKeyDown(KeyCode.Mouse0);
        // 判断isInspecting：按下V键则为检视武器
        isInspect = Input.GetKeyDown(KeyCode.V);
        // 判断isKnifeAttack：按下F键则为匕首攻击
        isKnifeAttack = Input.GetKeyDown(KeyCode.F);
    }

    // 方法：武器动画控制器
    private void AnimatorController()
    {
        animator.SetBool("walk", isWalk);
        if (isFire) animator.Play("Fire", 1, 0.0f);   // **修改**：若无后两个参数，即层级和从头（0%）开始播放，则无法实现快速连点射击
        if (isInspect) animator.Play("Inspect", 1, 0.0f);
        if (isKnifeAttack) animator.Play("KnifeAttack", 1, 0.0f);
    }

    // 方法：换弹逻辑
    private void Reload()
    {

    }
}
