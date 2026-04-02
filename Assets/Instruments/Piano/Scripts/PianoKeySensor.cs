using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10020)]
[DisallowMultipleComponent]
public class PianoKeySensor : MonoBehaviour
{
    const string PianoRigRootPath = "PianoModel/Piano_Rig/Root";

    [SerializeField] Transform targetBone;
    [SerializeField] Rigidbody targetBody;
    [SerializeField] BoxCollider sensorCollider;
    [SerializeField] Transform ignoredRoot;
    [SerializeField] Vector3 boneLocalAxis = Vector3.right;
    [SerializeField] float maxPressDegrees = 6f;
    [SerializeField] LayerMask presserLayers = ~0;

    [Header("Piano Input")]
    [SerializeField] int keyIndex = -1;
    [SerializeField] float noteOnThreshold = 0.3f;
    [SerializeField] float noteOffThreshold = 0.15f;
    [SerializeField] float pressStartDepthNormalized = 0.08f;

    readonly HashSet<Collider> m_ActiveColliders = new HashSet<Collider>();
    readonly List<Collider> m_RemovalBuffer = new List<Collider>();

    Quaternion m_InitialBoneLocalRotation = Quaternion.identity;
    float m_CurrentPress;
    bool m_IsNoteOn;
    Piano m_Piano;

    public BoxCollider SensorCollider => sensorCollider;
    public Bounds SensorBounds => sensorCollider != null ? sensorCollider.bounds : new Bounds(transform.position, Vector3.zero);
    public Transform TargetBone => targetBone;
    public Rigidbody TargetBody => targetBody;
    public float CurrentPressNormalized => m_CurrentPress;
    public float CurrentPressAngleDegrees => m_CurrentPress * maxPressDegrees;
    public int KeyIndex => keyIndex;
    public bool IsNoteOn => m_IsNoteOn;

    void Awake()
    {
        if (sensorCollider == null)
            sensorCollider = GetComponent<BoxCollider>();

        if (keyIndex < 0)
            keyIndex = ParseKeyIndexFromName(gameObject.name);

        m_Piano = GetComponentInParent<Piano>();
        if (m_Piano == null)
            Debug.LogError($"[PianoKeySensor] Piano root was not found for {gameObject.name}.", this);

        TryAutoAssignTargets();

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
        pressStartDepthNormalized = Mathf.Clamp(pressStartDepthNormalized, 0f, 0.95f);

        if (keyIndex < 0)
            keyIndex = ParseKeyIndexFromName(gameObject.name);

        TryAutoAssignTargets();
    }

    void LateUpdate()
    {
        if (sensorCollider == null)
            return;

        m_CurrentPress = ComputeTargetPress();
        if (!float.IsFinite(m_CurrentPress))
            m_CurrentPress = 0f;

        UpdateNoteState();
        ApplyBoneRotation();
    }

    void OnTriggerEnter(Collider other)
    {
        RegisterCollider(other);
    }

    void OnTriggerStay(Collider other)
    {
        RegisterCollider(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (other == null)
            return;

        m_ActiveColliders.Remove(other);
    }

    void OnDisable()
    {
        m_ActiveColliders.Clear();
        m_RemovalBuffer.Clear();
        m_CurrentPress = 0f;

        if (m_IsNoteOn)
        {
            m_IsNoteOn = false;
            m_Piano?.NoteOff(keyIndex);
        }

        ApplyBoneRotation();
    }

    void RegisterCollider(Collider other)
    {
        if (!IsAllowedPresser(other))
            return;

        m_ActiveColliders.Add(other);
    }

    float ComputeTargetPress()
    {
        if (sensorCollider == null || !TryGetSensorDepthRange(out float entranceDepth, out float deepEndDepth))
            return 0f;

        float targetPress = 0f;
        m_RemovalBuffer.Clear();

        foreach (Collider other in m_ActiveColliders)
        {
            if (!IsTrackedColliderValid(other))
            {
                m_RemovalBuffer.Add(other);
                continue;
            }

            float candidatePress = ComputeColliderPress(other, entranceDepth, deepEndDepth);
            if (candidatePress > targetPress)
                targetPress = candidatePress;
        }

        for (int i = 0; i < m_RemovalBuffer.Count; i++)
            m_ActiveColliders.Remove(m_RemovalBuffer[i]);

        return targetPress;
    }

    float ComputeColliderPress(Collider other, float entranceDepth, float deepEndDepth)
    {
        Vector3 size = sensorCollider.size;
        if (!IsFiniteVector(size) || size.y <= 0f)
            return 0f;

        Transform sensorTransform = sensorCollider.transform;
        float probeOffset = Mathf.Max(size.y * 0.02f, 0.001f);
        Vector3 sensorEntranceLocalPoint = new Vector3(sensorCollider.center.x, entranceDepth - probeOffset, sensorCollider.center.z);
        Vector3 sensorEntranceWorldPoint = sensorTransform.TransformPoint(sensorEntranceLocalPoint);
        Vector3 closestWorldPoint = other.ClosestPoint(sensorEntranceWorldPoint);
        Vector3 closestLocalPoint = sensorTransform.InverseTransformPoint(closestWorldPoint);

        if (!IsFiniteVector(closestLocalPoint))
            return 0f;

        float startDepth = Mathf.Lerp(entranceDepth, deepEndDepth, pressStartDepthNormalized);
        float pointDepth = Mathf.Clamp(closestLocalPoint.y, entranceDepth, deepEndDepth);
        return Mathf.Clamp01(Mathf.InverseLerp(startDepth, deepEndDepth, pointDepth));
    }

    bool TryGetSensorDepthRange(out float entranceDepth, out float deepEndDepth)
    {
        entranceDepth = 0f;
        deepEndDepth = 0f;

        if (sensorCollider == null)
            return false;

        Vector3 center = sensorCollider.center;
        Vector3 size = sensorCollider.size;
        if (!IsFiniteVector(center) || !IsFiniteVector(size) || size.y <= 0f)
            return false;

        float halfDepth = size.y * 0.5f;
        entranceDepth = center.y - halfDepth;
        deepEndDepth = center.y + halfDepth;
        return deepEndDepth > entranceDepth;
    }

    bool IsTrackedColliderValid(Collider other)
    {
        if (other == null || !other.enabled)
            return false;

        GameObject otherObject = other.gameObject;
        if (otherObject == null || !otherObject.activeInHierarchy)
            return false;

        return IsAllowedPresser(other);
    }

    bool IsAllowedPresser(Collider other)
    {
        if (other == null)
            return false;

        if ((presserLayers.value & (1 << other.gameObject.layer)) == 0)
            return false;

        return !ShouldIgnoreCollider(other);
    }

    void ApplyBoneRotation()
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

        if (targetBody != null && targetBody.isKinematic)
        {
            Quaternion parentRotation = targetBone.parent != null ? targetBone.parent.rotation : Quaternion.identity;
            targetBody.rotation = parentRotation * targetRotation;
            return;
        }

        targetBone.localRotation = targetRotation;
    }

    void UpdateNoteState()
    {
        if (!m_IsNoteOn && m_CurrentPress >= noteOnThreshold)
        {
            m_IsNoteOn = true;
            m_Piano?.NoteOn(keyIndex, m_CurrentPress);
        }
        else if (m_IsNoteOn && m_CurrentPress <= noteOffThreshold)
        {
            m_IsNoteOn = false;
            m_Piano?.NoteOff(keyIndex);
        }
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

    bool ShouldIgnoreCollider(Collider other)
    {
        if (ignoredRoot != null && other.transform.IsChildOf(ignoredRoot))
            return true;

        if (targetBone != null && other.transform.IsChildOf(targetBone.root))
            return true;

        if (targetBone != null && other.transform.IsChildOf(targetBone))
            return true;

        if (targetBody != null && other.attachedRigidbody == targetBody)
            return true;

        return false;
    }

    static int ParseKeyIndexFromName(string name)
    {
        int underscoreIndex = name.LastIndexOf('_');
        if (underscoreIndex >= 0 && int.TryParse(name.Substring(underscoreIndex + 1), out int sensorNumber))
            return sensorNumber - 1;
        return -1;
    }

    void TryAutoAssignTargets()
    {
        if (targetBone != null && targetBody != null)
            return;

        int resolvedKeyIndex = keyIndex >= 0 ? keyIndex : ParseKeyIndexFromName(gameObject.name);
        if (resolvedKeyIndex < 0)
            return;

        Transform pianoRoot = GetComponentInParent<Piano>()?.transform;
        if (pianoRoot == null)
            pianoRoot = transform.root;

        if (pianoRoot == null)
            return;

        string keyPath = $"{PianoRigRootPath}/key_{resolvedKeyIndex + 1}";
        Transform resolvedBone = pianoRoot.Find(keyPath);
        if (resolvedBone == null)
            return;

        if (targetBone == null)
            targetBone = resolvedBone;

        if (targetBody == null)
            targetBody = resolvedBone.GetComponent<Rigidbody>();
    }
}
