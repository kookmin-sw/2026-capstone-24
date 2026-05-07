using UnityEngine;

[DisallowMultipleComponent]
public sealed class StickHitSweeper : MonoBehaviour
{
    [SerializeField] BoxCollider stickCollider;
    [SerializeField] AnchoredStickGhostFollower ghostFollower;
    [SerializeField] LayerMask sweepLayerMask = ~0;

    Vector3 m_PrevCenter;
    bool m_HasPrev;

    void Awake()
    {
        if (stickCollider == null)
            stickCollider = GetComponent<BoxCollider>();
        if (ghostFollower == null)
            ghostFollower = GetComponent<AnchoredStickGhostFollower>();
    }

    void OnEnable()
    {
        m_HasPrev = false;
    }

    void FixedUpdate()
    {
        if (stickCollider == null || ghostFollower == null)
            return;

        Vector3 currentCenter = stickCollider.transform.TransformPoint(stickCollider.center);

        if (!m_HasPrev)
        {
            m_PrevCenter = currentCenter;
            m_HasPrev = true;
            return;
        }

        Vector3 delta = currentCenter - m_PrevCenter;
        float distance = delta.magnitude;

        if (distance > 0.001f)
        {
            Vector3 lossyScale = stickCollider.transform.lossyScale;
            Vector3 halfExtents = new Vector3(
                Mathf.Abs(stickCollider.size.x * lossyScale.x * 0.5f),
                Mathf.Abs(stickCollider.size.y * lossyScale.y * 0.5f),
                Mathf.Abs(stickCollider.size.z * lossyScale.z * 0.5f));

            RaycastHit[] hits = Physics.BoxCastAll(
                m_PrevCenter,
                halfExtents,
                delta / distance,
                stickCollider.transform.rotation,
                distance,
                sweepLayerMask,
                QueryTriggerInteraction.Collide);

            Vector3 velocity = ghostFollower.Velocity;
            foreach (RaycastHit hit in hits)
            {
                DrumHitZone hitZone = hit.collider.GetComponent<DrumHitZone>();
                hitZone?.TryProcessHit(stickCollider, velocity);
            }
        }

        m_PrevCenter = currentCenter;
    }
}
