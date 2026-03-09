using UnityEngine;

[DisallowMultipleComponent]
public class PianoKeySensor : MonoBehaviour
{
    [SerializeField] Transform targetBone;
    [SerializeField] BoxCollider sensorCollider;
    [SerializeField] Transform ignoredRoot;
    [SerializeField] Vector3 boneLocalAxis = Vector3.right;
    [SerializeField] float maxPressDegrees = 6f;
    [SerializeField] float pressDistance = 0.008f;
    [SerializeField] float pressSpeed = 18f;
    [SerializeField] float releaseSpeed = 26f;
    [SerializeField] LayerMask presserLayers = ~0;

    readonly Collider[] m_OverlapBuffer = new Collider[12];
    Quaternion m_InitialBoneLocalRotation;
    float m_CurrentPress;

    public BoxCollider SensorCollider => sensorCollider;
    public Bounds SensorBounds => sensorCollider != null ? sensorCollider.bounds : new Bounds(transform.position, Vector3.zero);

    void Awake()
    {
        if (sensorCollider == null)
            sensorCollider = GetComponent<BoxCollider>();

        if (targetBone != null)
            m_InitialBoneLocalRotation = targetBone.localRotation;
    }

    void OnValidate()
    {
        if (sensorCollider == null)
            sensorCollider = GetComponent<BoxCollider>();

        if (!IsFiniteVector(boneLocalAxis) || boneLocalAxis.sqrMagnitude < 0.0001f)
            boneLocalAxis = Vector3.right;

        maxPressDegrees = Mathf.Max(0f, maxPressDegrees);
        pressDistance = Mathf.Max(0.0005f, pressDistance);
        pressSpeed = Mathf.Max(0.01f, pressSpeed);
        releaseSpeed = Mathf.Max(0.01f, releaseSpeed);
    }

    void FixedUpdate()
    {
        if (sensorCollider == null)
            return;

        Bounds bounds = sensorCollider.bounds;
        int overlapCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            m_OverlapBuffer,
            transform.rotation,
            presserLayers,
            QueryTriggerInteraction.Ignore);

        float targetPress = 0f;
        float sensorTopY = bounds.max.y;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider other = m_OverlapBuffer[i];
            if (other == null)
                continue;

            if (ignoredRoot != null && other.transform.IsChildOf(ignoredRoot))
                continue;

            Bounds otherBounds = other.bounds;
            if (!IsFiniteBounds(otherBounds))
                continue;

            float penetration = sensorTopY - otherBounds.min.y;
            if (penetration <= 0f)
                continue;

            float candidatePress = Mathf.Clamp01(penetration / pressDistance);
            if (candidatePress > targetPress)
                targetPress = candidatePress;
        }

        float speed = targetPress > m_CurrentPress ? pressSpeed : releaseSpeed;
        m_CurrentPress = Mathf.MoveTowards(m_CurrentPress, targetPress, speed * Time.fixedDeltaTime);

        if (!float.IsFinite(m_CurrentPress))
            m_CurrentPress = 0f;
    }

    void LateUpdate()
    {
        if (targetBone == null)
            return;

        Vector3 axis = boneLocalAxis;
        if (!IsFiniteVector(axis) || axis.sqrMagnitude < 0.0001f)
            axis = Vector3.right;

        float angle = -maxPressDegrees * m_CurrentPress;
        if (!float.IsFinite(angle))
            angle = 0f;

        Quaternion targetRotation = m_InitialBoneLocalRotation * Quaternion.AngleAxis(angle, axis.normalized);
        if (!IsFiniteQuaternion(targetRotation))
            return;

        targetBone.localRotation = targetRotation;
    }

    static bool IsFiniteVector(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    static bool IsFiniteQuaternion(Quaternion value)
    {
        return float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }

    static bool IsFiniteBounds(Bounds value)
    {
        return IsFiniteVector(value.center) && IsFiniteVector(value.size);
    }
}
