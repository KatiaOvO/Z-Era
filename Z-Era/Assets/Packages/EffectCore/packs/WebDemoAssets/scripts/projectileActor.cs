using UnityEngine;
using System.Collections;

public class projectileActor : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float projectileSpeed = 10f;
    public float lifetime = 5f;
    public GameObject impactEffect;
    public AudioClip impactSound;

    [Header("Simulation Settings")]
    public bool usePhysicsSimulation = true;
    public float gravity = -9.81f;

    // 这个字段虽然被赋值但未使用，可以移除或添加使用逻辑
    // public float projectileSimFire = 1.0f;

    private Rigidbody rb;
    private float timer;
    private bool hasImpacted = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (usePhysicsSimulation && rb != null)
        {
            rb.velocity = transform.forward * projectileSpeed;
        }
        else
        {
            // 非物理模拟，直接移动
            StartCoroutine(StraightMovement());
        }

        // 如果需要使用projectileSimFire，可以在这里启用：
        // if (projectileSimFire > 0)
        // {
        //     GetComponent<ParticleSystem>()?.main.simulationSpeed = projectileSimFire;
        // }
    }

    IEnumerator StraightMovement()
    {
        while (timer < lifetime && !hasImpacted)
        {
            transform.position += transform.forward * projectileSpeed * Time.deltaTime;
            timer += Time.deltaTime;
            yield return null;
        }

        if (!hasImpacted)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (usePhysicsSimulation && rb != null)
        {
            timer += Time.deltaTime;

            if (timer >= lifetime || hasImpacted)
            {
                Destroy(gameObject);
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;

        hasImpacted = true;

        // 创建碰撞效果
        if (impactEffect != null)
        {
            Instantiate(impactEffect, collision.contacts[0].point, Quaternion.LookRotation(collision.contacts[0].normal));
        }

        // 播放碰撞声音
        if (impactSound != null)
        {
            AudioSource.PlayClipAtPoint(impactSound, collision.contacts[0].point);
        }

        // 碰撞后停止物理模拟
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // 延迟销毁
        Destroy(gameObject, 0.5f);
    }
}