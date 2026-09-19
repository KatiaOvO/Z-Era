using INab.Dissolve;
using UnityEngine;

public class ZombieEffects : MonoBehaviour
{
    #region 效果
    [SerializeField]
    [Tooltip("溅血粒子预制体")]
    private ParticleSystem bloodEffectPrefab;
    [Tooltip("渲染器")]
    private Renderer rend;
    [Tooltip("材质")]
    private Material mat;
    #endregion

    #region 参数
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("溅血特效存活时间")]
    private float bloodEffectLifeTime = 0.5f;
    [Tooltip("溶解参数名")]
    private string dissolvePropertyName = "_DissolveAmount";
    [SerializeField]
    [Tooltip("溶解总时长")]
    private float dissolveDuration = 4.0f;
    [Tooltip("溶解已经过去的时间")]
    private float elapsed = 0.5f;
    #endregion

    private void Start()
    {
        rend = GetComponentInChildren<Renderer>();
        mat = rend.material;
    }

    // 方法：在子弹命中部位播放溅血粒子系统
    public void PlayHitEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (bloodEffectPrefab == null)
        {
            return;
        }

        Vector3 effectDirection = hitNormal.sqrMagnitude > 0f
            ? hitNormal
            : Vector3.up;

        ParticleSystem effect = Instantiate(
            bloodEffectPrefab,
            hitPoint,
            Quaternion.LookRotation(effectDirection)
        );

        if (!effect.main.playOnAwake)
        {
            effect.Play();
        }

        Destroy(effect.gameObject, bloodEffectLifeTime);
    }

    // 方法：zombie死亡后溶解
    public void ZombieDissolve()
    {
        elapsed += Time.deltaTime;
        // 归一化到0.5~1，因为溶解效果的前0.5还没开始溶解，故直接跳过，从0.5开始溶解
        float t = Mathf.Clamp01(elapsed / dissolveDuration);
        float dissolveValue = Mathf.Lerp(0.5f, 1.0f, t);
        mat.SetFloat(dissolvePropertyName, dissolveValue);
        if(dissolveValue >= 1.0f)
        {
            dissolveValue = 1.0f;
        }
    }
}
