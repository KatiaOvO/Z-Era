using UnityEngine;
using System.Collections;

public class particleColorChangerMaster : MonoBehaviour
{
    public ParticleSystem[] particleSystems;
    public float colorChangeInterval = 0.5f;
    public Color[] colors;

    private int currentColorIndex = 0;
    private float timer;

    void Start()
    {
        // 确保particleSystems不为空
        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>();
        }

        // 初始化颜色
        if (colors != null && colors.Length > 0)
        {
            ChangeParticleColors(colors[0]);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= colorChangeInterval && colors != null && colors.Length > 0)
        {
            timer = 0f;
            currentColorIndex = (currentColorIndex + 1) % colors.Length;
            ChangeParticleColors(colors[currentColorIndex]);
        }
    }

    void ChangeParticleColors(Color newColor)
    {
        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps != null)
            {
                // 修复废弃的playbackSpeed，使用simulationSpeed替代
                var mainModule = ps.main;
                mainModule.simulationSpeed = mainModule.simulationSpeed; // 保持当前速度

                // 通过修改材质颜色来改变粒子颜色（最兼容的方法）
                ChangeParticleColorThroughMaterial(ps, newColor);
            }
        }
    }

    // 通过材质方式改变粒子颜色
    void ChangeParticleColorThroughMaterial(ParticleSystem ps, Color newColor)
    {
        Renderer renderer = ps.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            // 检查材质是否有颜色属性
            if (renderer.material.HasProperty("_Color"))
            {
                renderer.material.color = newColor;
            }
            else if (renderer.material.HasProperty("_TintColor"))
            {
                renderer.material.SetColor("_TintColor", newColor);
            }
        }
    }
}