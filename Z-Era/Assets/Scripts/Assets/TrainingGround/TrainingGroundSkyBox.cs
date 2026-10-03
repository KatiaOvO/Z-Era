using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 训练场天空盒切换：挂在 SkyBoxRoot 上（其下为挂了触发器的
/// 3D TextMeshPro 文字，如 DefaultText、PurpleText 等）。子弹命中
/// 哪块文字，场景天空盒就切换为检查器中配置的对应材质。
/// 子弹命中当帧即被对象池回收，物理触发器事件不一定触发，因此这里
/// 订阅 BulletHandle 的 BulletLaunched/BulletSettled 静态事件，用
/// "发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短射线，
/// 找出子弹实际命中的碰撞体。
/// 只订阅事件、只修改 RenderSettings.skybox，不修改任何根代码；
/// 场景卸载时反订阅事件，不影响其他场景。
/// </summary>
public class TrainingGroundSkyBox : MonoBehaviour
{
    [System.Serializable]
    private class SkyBoxEntry
    {
        [Tooltip("3D 文字物体的名称（SkyBoxRoot 的直接子物体）")]
        public string textName;

        [Tooltip("命中该文字后切换到的天空盒材质")]
        public Material skyBox;
    }

    [Header("天空盒配置")]

    [Tooltip("文字物体名与天空盒材质的对应关系")]
    [SerializeField]
    private SkyBoxEntry[] skyBoxEntries;

    [Header("选中表现")]

    [Tooltip("选中时的文字颜色")]
    [SerializeField]
    private Color selectedColor = new Color(0.25f, 1f, 0.35f);

    [Tooltip("未选中时的文字颜色")]
    [SerializeField]
    private Color normalColor = Color.white;

    private readonly Dictionary<BulletHandle, Vector3> launchPositions =
        new Dictionary<BulletHandle, Vector3>();

    private TMP_Text selectedText;

    // 落点反查射线的参数：子弹停在命中点表面，稍微沿反方向退一点
    // 再沿飞行方向打短射线即可命中原表面
    private const float SettleRayBackOffset = 0.05f;
    private const float SettleRayDistance = 0.15f;

    // 落点反查时排除的图层名，与 BulletHandle 默认排除的图层保持一致，
    // 避免反查射线命中子弹本身被忽略的玩家/武器碰撞体；
    // 图层号在 OnEnable 中查询（Unity API 不允许在字段初始化器中调用）
    private static readonly string[] SettleRayIgnoredLayerNames =
    {
        "Bullet",
        "Player",
        "Weapon",
        "Ignore Raycast"
    };

    private int settleRayIgnoreMask;

    private void OnEnable()
    {
        settleRayIgnoreMask = 0;
        foreach (string layerName in SettleRayIgnoredLayerNames)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer >= 0)
            {
                settleRayIgnoreMask |= 1 << layer;
            }
        }

        BulletHandle.BulletLaunched += OnBulletLaunched;
        BulletHandle.BulletSettled += OnBulletSettled;

        // 保险：无论从哪里进入本场景，都基于当前天空盒重建一次
        // 环境光照与默认反射。运行时切换场景不会自动重算这些数据，
        // 不重算会出现墙体背光面明暗异常（下方射击切换天空盒时
        // 也会再调用一次，效果一致）
        DynamicGI.UpdateEnvironment();
    }

    private void OnDisable()
    {
        BulletHandle.BulletLaunched -= OnBulletLaunched;
        BulletHandle.BulletSettled -= OnBulletSettled;

        launchPositions.Clear();
    }

    private void OnBulletLaunched(BulletHandle bullet)
    {
        if (bullet == null)
        {
            return;
        }

        launchPositions[bullet] = bullet.transform.position;
    }

    private void OnBulletSettled(BulletHandle bullet)
    {
        if (bullet == null ||
            !launchPositions.TryGetValue(
                bullet,
                out Vector3 startPosition))
        {
            return;
        }

        launchPositions.Remove(bullet);

        // 超时回收的子弹没有命中任何表面，落点反查打不到东西时会
        // 自然忽略，这里无需额外区分
        TrySwitchSkyBoxAtHit(
            bullet.transform.position,
            startPosition);
    }

    // 用"发射点 → 落点"还原飞行方向，再从落点向后退一点打一根短
    // 射线，找出子弹实际命中的碰撞体；命中文字时切换天空盒
    private void TrySwitchSkyBoxAtHit(
        Vector3 settlePosition,
        Vector3 launchPosition)
    {
        Vector3 direction = settlePosition - launchPosition;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        if (!Physics.Raycast(
            settlePosition - direction * SettleRayBackOffset,
            direction,
            out RaycastHit hit,
            SettleRayDistance,
            ~settleRayIgnoreMask,
            QueryTriggerInteraction.Collide))
        {
            return;
        }

        Transform display =
            GetDirectChildUnderRoot(hit.collider.transform);

        if (display == null)
        {
            return;
        }

        foreach (SkyBoxEntry entry in skyBoxEntries)
        {
            if (entry != null &&
                entry.skyBox != null &&
                entry.textName == display.name)
            {
                RenderSettings.skybox = entry.skyBox;

                // 更新环境光照，让环境反射与 Lit 颜色跟随新天空盒
                DynamicGI.UpdateEnvironment();

                // 命中的文字变为选中色，其余文字恢复常规色
                SetTextSelected(selectedText, false);
                selectedText =
                    display.GetComponent<TMP_Text>();
                SetTextSelected(selectedText, true);

                return;
            }
        }
    }

    private void SetTextSelected(TMP_Text text, bool selected)
    {
        if (text != null)
        {
            text.color = selected ? selectedColor : normalColor;
        }
    }

    // 从命中的碰撞体向上找到 SkyBoxRoot 的直接子物体
    // （即文字物体本体，命中可能发生在文字网格的子碰撞体上）
    private Transform GetDirectChildUnderRoot(Transform current)
    {
        while (current.parent != null &&
               current.parent != transform)
        {
            current = current.parent;
        }

        return current.parent == transform ? current : null;
    }
}
