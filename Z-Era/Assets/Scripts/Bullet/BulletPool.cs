using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class BulletPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField, Min(0)]
    private int defaultCapacity = 30;

    [SerializeField, Min(1)]
    private int maxSize = 300;

#if UNITY_EDITOR
    private const bool CollectionCheck = true;
#else
    private const bool CollectionCheck = false;
#endif

    // 每种子弹预制体对应一个独立对象池
    private readonly Dictionary<GameObject, ObjectPool<BulletHandle>> pools =
        new Dictionary<GameObject, ObjectPool<BulletHandle>>();

    // 记录每个子弹实例所属的对象池，便于回收
    private readonly Dictionary<BulletHandle, ObjectPool<BulletHandle>> bulletPools =
        new Dictionary<BulletHandle, ObjectPool<BulletHandle>>();

    /// <summary>
    /// 从对象池获取一颗子弹。
    /// </summary>
    public BulletHandle Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("BulletPool.Get 失败：子弹预制体为空。");
            return null;
        }

        if (prefab.GetComponent<BulletHandle>() == null)
        {
            Debug.LogError($"BulletPool.Get 失败：{prefab.name} 没有挂载 BulletHandle。");
            return null;
        }

        ObjectPool<BulletHandle> pool = GetOrCreatePool(prefab);
        BulletHandle bullet = pool.Get();

        if (bullet == null)
        {
            return null;
        }

        bulletPools[bullet] = pool;
        bullet.SetPool(this);
        bullet.transform.SetPositionAndRotation(position, rotation);
        bullet.gameObject.SetActive(true);

        return bullet;
    }

    /// <summary>
    /// 将子弹回收到所属对象池。
    /// </summary>
    public void Release(BulletHandle bullet)
    {
        // 已回收或已经销毁的对象直接返回，避免重复回收
        if (bullet == null)
        {
            return;
        }
        if (!bulletPools.TryGetValue(bullet, out ObjectPool<BulletHandle> pool))
        {
            Debug.LogWarning($"BulletPool.Release 失败：{bullet.name} 不属于当前对象池。");
            return;
        }
        // 回收前删除映射，避免池满时子弹被销毁后留下无效字典项
        bulletPools.Remove(bullet);
        pool.Release(bullet);
    }

    /// <summary>
    /// 获取指定预制体对应的对象池，不存在时创建。
    /// </summary>
    private ObjectPool<BulletHandle> GetOrCreatePool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<BulletHandle> existingPool))
        {
            return existingPool;
        }

        ObjectPool<BulletHandle> pool = new ObjectPool<BulletHandle>(
            createFunc: () => CreateBullet(prefab),
            actionOnGet: null,
            actionOnRelease: bullet => bullet.gameObject.SetActive(false),
            actionOnDestroy: bullet => Destroy(bullet.gameObject),
            collectionCheck: CollectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );

        pools.Add(prefab, pool);
        return pool;
    }

    /// <summary>
    /// 创建一颗新的池内子弹。
    /// </summary>
    private BulletHandle CreateBullet(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.name = prefab.name;
        instance.SetActive(false);
        return instance.GetComponent<BulletHandle>();
    }

    private void OnDestroy()
    {
        pools.Clear();
        bulletPools.Clear();
    }
}