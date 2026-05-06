using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class DrumHitZone : MonoBehaviour
{
    [SerializeField] DrumPiece targetPiece;
    [SerializeField] int midiNote = 36;
    [SerializeField] BoxCollider triggerCollider;
    [SerializeField] LayerMask allowedLayers = ~0;
    [SerializeField, Min(0f)] float minImpactSpeed = 0.2f;
    [SerializeField, Min(0f)] float maxImpactSpeed = 4f;
    [SerializeField, Min(0f)] float retriggerCooldown = 0.05f;
    [SerializeField] bool useFingertipVelocityFallback = true;

    readonly Dictionary<int, float> m_LastHitTimeByCollider = new Dictionary<int, float>();

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
        minImpactSpeed = Mathf.Max(0f, minImpactSpeed);
        maxImpactSpeed = Mathf.Max(minImpactSpeed, maxImpactSpeed);
        retriggerCooldown = Mathf.Max(0f, retriggerCooldown);
    }

    void OnDisable()
    {
        m_LastHitTimeByCollider.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (targetPiece == null || other == null)
            return;

        if (!TryGetSourceVelocity(other, out Vector3 sourceVelocity))
            return;

        TryProcessHit(other, sourceVelocity);
    }

    public bool TryProcessHit(Collider other, Vector3 sourceVelocity)
    {
        if (targetPiece == null || other == null)
            return false;

        if ((allowedLayers.value & (1 << other.gameObject.layer)) == 0)
            return false;

        Vector3 zoneUp = transform.up;
        float impactSpeed = Mathf.Max(0f, Vector3.Dot(sourceVelocity, -zoneUp));
        if (impactSpeed < minImpactSpeed)
            return false;

        int colliderId = other.GetInstanceID();
        float now = Time.time;
        if (m_LastHitTimeByCollider.TryGetValue(colliderId, out float lastHitTime) &&
            now - lastHitTime < retriggerCooldown)
            return false;

        m_LastHitTimeByCollider[colliderId] = now;

        float velocity = maxImpactSpeed <= minImpactSpeed
            ? 1f
            : Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impactSpeed);

        targetPiece.ReportHit(midiNote, Mathf.Clamp01(velocity));
        return true;
    }

    void ResolveReferences()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        if (targetPiece == null)
            targetPiece = GetComponentInParent<DrumPiece>();
    }

    bool TryGetSourceVelocity(Collider other, out Vector3 sourceVelocity)
    {
        sourceVelocity = Vector3.zero;

        if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic)
        {
            sourceVelocity = other.attachedRigidbody.linearVelocity;
            if (sourceVelocity.sqrMagnitude > 0f)
                return true;
        }

        if (useFingertipVelocityFallback)
        {
            Fingertip fingertip = other.GetComponentInParent<Fingertip>();
            if (fingertip != null)
            {
                sourceVelocity = fingertip.WorldVelocity;
                if (sourceVelocity.sqrMagnitude > 0f)
                    return true;
            }
        }

        AnchoredStickGhostFollower stickFollower = other.GetComponentInParent<AnchoredStickGhostFollower>();
        if (stickFollower != null)
        {
            sourceVelocity = stickFollower.Velocity;
            return sourceVelocity.sqrMagnitude > 0f;
        }

        return false;
    }
}
