using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class DrumHitZone : MonoBehaviour
{
    [SerializeField] DrumPiece targetPiece;
    [SerializeField] int midiNote = 36;
    [Tooltip("이 파츠 노트 패널의 Y 오프셋 (월드 단위). DrumNoteDisplayAdapter의 전역 yOffset에 추가된다.")]
    [SerializeField] float panelYOffset = 0f;
    [SerializeField] BoxCollider triggerCollider;
    [SerializeField] LayerMask allowedLayers = ~0;
    [SerializeField, Min(0f)] float minImpactSpeed = 0.2f;
    [SerializeField, Min(0f)] float maxImpactSpeed = 4f;
    [SerializeField, Min(0f)] float retriggerCooldown = 0.05f;
    [SerializeField] bool useFingertipVelocityFallback = true;

    readonly Dictionary<int, float> m_LastHitTimeByCollider = new Dictionary<int, float>();

    /// <summary>
    /// 이 히트존에 할당된 MIDI 노트 번호.
    /// DrumNoteDisplayAdapter가 패널 배치 대상 파츠를 결정하기 위해 사용한다.
    /// </summary>
    public int MidiNote => midiNote;

    /// <summary>
    /// 이 파츠 전용 패널 Y 오프셋. DrumNoteDisplayAdapter의 전역 yOffset에 더해진다.
    /// </summary>
    public float PanelYOffset => panelYOffset;

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

        if ((allowedLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        if (!TryGetSourceVelocity(other, out Vector3 sourceVelocity))
            return;

        Vector3 zoneUp = transform.up;
        float impactSpeed = Mathf.Max(0f, Vector3.Dot(sourceVelocity, -zoneUp));
        if (impactSpeed < minImpactSpeed)
            return;

        int colliderId = other.GetInstanceID();
        float now = Time.time;
        if (m_LastHitTimeByCollider.TryGetValue(colliderId, out float lastHitTime) &&
            now - lastHitTime < retriggerCooldown)
            return;

        m_LastHitTimeByCollider[colliderId] = now;

        float velocity = maxImpactSpeed <= minImpactSpeed
            ? 1f
            : Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impactSpeed);

        targetPiece.ReportHit(midiNote, Mathf.Clamp01(velocity));
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

        if (other.attachedRigidbody != null)
        {
            sourceVelocity = other.attachedRigidbody.linearVelocity;
            if (sourceVelocity.sqrMagnitude > 0f)
                return true;
        }

        if (!useFingertipVelocityFallback)
            return false;

        Fingertip fingertip = other.GetComponentInParent<Fingertip>();
        if (fingertip == null)
            return false;

        sourceVelocity = fingertip.WorldVelocity;
        return sourceVelocity.sqrMagnitude > 0f;
    }
}
