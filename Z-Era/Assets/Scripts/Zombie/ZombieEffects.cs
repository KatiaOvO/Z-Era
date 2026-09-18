using UnityEngine;

public class ZombieEffects : MonoBehaviour
{
    [Header("命中特效")]
    [SerializeField]
    [Tooltip("溅血粒子预制体")]
    private ParticleSystem bloodEffectPrefab;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("溅血特效存活时间")]
    private float bloodEffectLifeTime = 0.5f;

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
}
