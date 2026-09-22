using INab.Dissolve;
using UnityEngine;

public class ZombieEffects : MonoBehaviour
{
    [Header("受击特效")]

    [Tooltip("子弹命中的溅血粒子预制体")]
    [SerializeField]
    private ParticleSystem bloodEffectPrefab;

    [Tooltip("匕首命中的溅血粒子预制体，留空时使用子弹溅血粒子")]
    [SerializeField]
    private ParticleSystem knifeHitBloodEffectPrefab;

    [Tooltip("渲染器")]
    [SerializeField]
    private Renderer rend;

    [Tooltip("材质")]
    [SerializeField]
    private Material mat;

    [Header("参数")]

    [Tooltip("子弹溅血特效存活时间")]
    [SerializeField, Min(0.01f)]
    private float bloodEffectLifeTime = 0.5f;

    [Tooltip("匕首溅血特效存活时间")]
    [SerializeField, Min(0.01f)]
    private float knifeHitBloodEffectLifetime = 0.5f;

    [Tooltip("匕首溅血进入敌人身体内部的距离")]
    [SerializeField, Min(0f)]
    private float knifeBloodSurfaceOffset = 0.02f;

    [Tooltip("溶解参数名")]
    private string dissolvePropertyName =
        "_DissolveAmount";

    [Tooltip("溶解总时长")]
    [SerializeField]
    private float dissolveDuration = 4f;

    [Tooltip("溶解已经过去的时间")]
    private float elapsed = 0.5f;

    [Tooltip("是否完全溶解")]
    public bool isFullyDissolved = false;

    private void Start()
    {
        if (rend == null)
        {
            rend = GetComponentInChildren<Renderer>();
        }

        if (rend != null)
        {
            mat = rend.material;
        }
    }

    /// <summary>
    /// 子弹命中时播放溅血效果。
    /// </summary>
    public void PlayHitEffect(
        Vector3 hitPoint,
        Vector3 hitNormal
    )
    {
        if (bloodEffectPrefab == null)
        {
            return;
        }

        Vector3 effectDirection =
            hitNormal.sqrMagnitude > 0.0001f
                ? hitNormal.normalized
                : Vector3.up;

        PlayBloodEffect(
            bloodEffectPrefab,
            hitPoint,
            effectDirection,
            bloodEffectLifeTime
        );
    }

    /// <summary>
    /// 匕首命中时播放溅血效果。
    /// attackDirection 应该是从玩家指向敌人命中点的方向。
    /// </summary>
    public void PlayKnifeHitEffect(
        Vector3 hitPoint,
        Vector3 attackDirection
    )
    {
        ParticleSystem effectPrefab =
            knifeHitBloodEffectPrefab != null
                ? knifeHitBloodEffectPrefab
                : bloodEffectPrefab;

        if (effectPrefab == null)
        {
            return;
        }

        Vector3 direction =
            attackDirection.sqrMagnitude > 0.0001f
                ? attackDirection.normalized
                : Vector3.forward;

        // 向敌人内部偏移，避免粒子生成在模型表面外侧。
        Vector3 spawnPoint =
            hitPoint +
            direction *
            Mathf.Max(0f, knifeBloodSurfaceOffset);

        // 血液朝攻击方向的反方向喷出，也就是朝玩家一侧喷出。
        Vector3 effectDirection = -direction;

        PlayBloodEffect(
            effectPrefab,
            spawnPoint,
            effectDirection,
            knifeHitBloodEffectLifetime
        );
    }

    private void PlayBloodEffect(
        ParticleSystem effectPrefab,
        Vector3 spawnPoint,
        Vector3 effectDirection,
        float lifeTime
    )
    {
        ParticleSystem effect = Instantiate(
            effectPrefab,
            spawnPoint,
            Quaternion.LookRotation(effectDirection)
        );

        if (!effect.main.playOnAwake)
        {
            effect.Play();
        }

        Destroy(
            effect.gameObject,
            Mathf.Max(0.01f, lifeTime)
        );
    }

    /// <summary>
    /// Zombie 死亡后开始溶解。
    /// </summary>
    public void ZombieDissolve()
    {
        if (mat == null)
        {
            return;
        }

        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(
            elapsed / Mathf.Max(
                0.01f,
                dissolveDuration
            )
        );

        float dissolveValue =
            Mathf.Lerp(0.5f, 1f, t);

        mat.SetFloat(
            dissolvePropertyName,
            dissolveValue
        );

        if (dissolveValue >= 1f)
        {
            dissolveValue = 1f;
            isFullyDissolved = true;
        }
    }
}