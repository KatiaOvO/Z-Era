using UnityEngine;

public class HitPartMarker : MonoBehaviour
{
    [SerializeField]
    [Tooltip("该碰撞体或部位对应的命中部位")]
    private HitPart hitPart = HitPart.Body;

    public HitPart Part => hitPart;
}