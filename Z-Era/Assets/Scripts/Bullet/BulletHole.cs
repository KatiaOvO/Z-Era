using UnityEngine;

// 场景中的单个弹孔贴花，到达存活时间后自行销毁。
public class BulletHole : MonoBehaviour
{
    private float expireTime;

    public void Init(float lifeTime)
    {
        expireTime = Time.time + Mathf.Max(0.1f, lifeTime);
    }

    private void Update()
    {
        if (Time.time >= expireTime)
        {
            Destroy(gameObject);
        }
    }
}
