using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Instruments
{
// 10006 = AnchoredStickGhostFollower(10005)의 LateUpdate SyncToGhost·UpdateVelocity 직후.
// stick이 이번 프레임 위치로 동기화되고 Velocity가 갱신된 뒤에 sweep하도록 강제한다.
[DefaultExecutionOrder(10006)]
[DisallowMultipleComponent]
public sealed class StickHitSweeper : MonoBehaviour
{
    [SerializeField] BoxCollider stickCollider;
    [SerializeField] AnchoredStickGhostFollower ghostFollower;
    [SerializeField] LayerMask sweepLayerMask = ~0;

    [SerializeField] HapticImpulsePlayer hapticImpulsePlayer;
    [SerializeField, Range(0f, 1f)] float hapticMinAmplitude = 0.2f;
    [SerializeField, Range(0f, 1f)] float hapticMaxAmplitude = 0.8f;
    [SerializeField, Min(0f)] float hapticDuration = 0.08f;

    public void SetHapticImpulsePlayer(HapticImpulsePlayer player)
    {
        hapticImpulsePlayer = player;
    }

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

    void LateUpdate()
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
                if (hitZone == null)
                    continue;
                if (!hitZone.TryProcessHit(stickCollider, velocity))
                    continue;
                if (hapticImpulsePlayer == null)
                    continue;
                float normalized = hitZone.NormalizeImpactSpeed(velocity);
                float amplitude = Mathf.Lerp(hapticMinAmplitude, hapticMaxAmplitude, normalized);
                hapticImpulsePlayer.SendHapticImpulse(amplitude, hapticDuration);
            }
        }

        m_PrevCenter = currentCenter;
    }
}
}
