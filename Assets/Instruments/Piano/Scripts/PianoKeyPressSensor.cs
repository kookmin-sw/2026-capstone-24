using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(10020)]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class PianoKeyPressSensor : MonoBehaviour
{
    const string PianoRigRootPath = "PianoModel/Piano_Rig/Root";
    const float PressSmoothTime = 0.01f;
    const float ReleaseSmoothTime = 0.03f;
    const float CaptureTolerance = 1e-4f;

    enum PressLocalAxis
    {
        X,
        Y,
        Z
    }

    [SerializeField] int keyIndex = -1;
    [FormerlySerializedAs("targetBone")]
    [SerializeField] Transform keyBone;
    [FormerlySerializedAs("targetBody")]
    [SerializeField] Rigidbody keyBody;
    [FormerlySerializedAs("sensorCollider")]
    [SerializeField] BoxCollider triggerCollider;
    [FormerlySerializedAs("ignoredRoot")]
    [SerializeField] Transform ignoredRoot;
    [SerializeField] Vector3 boneLocalAxis = Vector3.right;
    [SerializeField] float maxPressDegrees = 6f;
    [SerializeField] PressLocalAxis pressLocalAxis = PressLocalAxis.Z;
    [SerializeField] bool pressAxisPositive;
    [FormerlySerializedAs("pressStartDepthNormalized")]
    [SerializeField, Range(0f, 0.95f)] float pressStartDepthNormalized = 0.0f;
    [SerializeField, Range(0.05f, 1f)] float fullPressDepthNormalized = 0.85f;
    [SerializeField] float noteOnThreshold = 0.3f;
    [SerializeField] float noteOffThreshold = 0.15f;
    [FormerlySerializedAs("presserLayers")]
    [SerializeField] LayerMask pressLayers = ~0;

    readonly HashSet<Collider> m_Active = new HashSet<Collider>();
    readonly List<Collider> m_RemovalScratch = new List<Collider>();

    Piano m_Piano;
    Quaternion m_RestRotation = Quaternion.identity;
    float m_RawPress;
    float m_SmoothPress;
    float m_SmoothVelocity;
    bool m_NoteOn;

    public int KeyIndex => keyIndex;
    public float PressValue => m_SmoothPress;
    public bool IsNoteOn => m_NoteOn;

    void Awake()
    {
        ResolveReferences();

        if (keyBone != null)
            m_RestRotation = keyBone.localRotation;
    }

    void OnValidate()
    {
        SanitizeSerializedFields();
        ResolveReferences();
    }

    void LateUpdate()
    {
        ComputePress();

        float smoothTime = m_RawPress >= m_SmoothPress ? PressSmoothTime : ReleaseSmoothTime;
        m_SmoothPress = Mathf.SmoothDamp(m_SmoothPress, m_RawPress, ref m_SmoothVelocity, smoothTime);

        UpdateNote();
        ApplyRotation();
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
        // Removal is handled in ComputePress so colliders remain captured while pressing through the key.
    }

    void OnDisable()
    {
        m_Active.Clear();
        m_RawPress = 0f;
        m_SmoothPress = 0f;
        m_SmoothVelocity = 0f;

        if (m_NoteOn)
        {
            m_NoteOn = false;
            if (m_Piano != null)
                m_Piano.NoteOff(keyIndex);
        }

        ApplyRotation();
    }

    void ResolveReferences()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();

        if (keyIndex < 0)
            keyIndex = ParseKeyIndexFromName(gameObject.name);

        m_Piano = GetComponentInParent<Piano>();
        AutoAssignBone();
    }

    void SanitizeSerializedFields()
    {
        if (boneLocalAxis.sqrMagnitude < 0.0001f)
            boneLocalAxis = Vector3.right;

        maxPressDegrees = Mathf.Max(0f, maxPressDegrees);
        pressStartDepthNormalized = Mathf.Clamp(pressStartDepthNormalized, 0f, 0.95f);
        fullPressDepthNormalized = Mathf.Clamp(fullPressDepthNormalized, pressStartDepthNormalized + 0.01f, 1f);
        noteOnThreshold = Mathf.Clamp01(noteOnThreshold);
        noteOffThreshold = Mathf.Clamp(noteOffThreshold, 0f, noteOnThreshold);
    }

    void RegisterCollider(Collider other)
    {
        if (!IsAllowedPresser(other))
            return;

        m_Active.Add(other);
    }

    void ComputePress()
    {
        m_RawPress = 0f;

        if (m_Active.Count == 0)
            return;

        if (!TryGetSensorDepthRange(out float entranceDepth, out float fullDepth))
            return;

        m_RemovalScratch.Clear();
        foreach (Collider other in m_Active)
        {
            if (!IsTrackedColliderValid(other))
            {
                m_RemovalScratch.Add(other);
                continue;
            }

            if (!IsStillCapturedByBox(other, entranceDepth))
                m_RemovalScratch.Add(other);
        }

        for (int i = 0; i < m_RemovalScratch.Count; i++)
            m_Active.Remove(m_RemovalScratch[i]);
        m_RemovalScratch.Clear();

        if (m_Active.Count == 0)
            return;

        float maxPress = 0f;
        foreach (Collider other in m_Active)
        {
            float press = ComputeColliderPress(other, entranceDepth, fullDepth);
            if (press > maxPress)
                maxPress = press;
        }

        m_RawPress = maxPress;
    }

    void UpdateNote()
    {
        if (!m_NoteOn)
        {
            if (m_RawPress >= noteOnThreshold)
            {
                m_NoteOn = true;
                if (m_Piano != null)
                    m_Piano.NoteOn(keyIndex, m_RawPress);
            }
        }
        else if (m_RawPress <= noteOffThreshold)
        {
            m_NoteOn = false;
            if (m_Piano != null)
                m_Piano.NoteOff(keyIndex);
        }
    }

    void ApplyRotation()
    {
        if (keyBone == null)
            return;

        Vector3 axis = boneLocalAxis.sqrMagnitude < 0.0001f ? Vector3.right : boneLocalAxis.normalized;
        Quaternion targetRotation = m_RestRotation * Quaternion.AngleAxis(-maxPressDegrees * m_SmoothPress, axis);

        if (keyBody != null && keyBody.isKinematic)
        {
            Quaternion parentRotation = keyBone.parent != null ? keyBone.parent.rotation : Quaternion.identity;
            keyBody.rotation = parentRotation * targetRotation;
            return;
        }

        keyBone.localRotation = targetRotation;
    }

    static int ParseKeyIndexFromName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return -1;

        int underscoreIndex = objectName.LastIndexOf('_');
        if (underscoreIndex < 0 || underscoreIndex >= objectName.Length - 1)
            return -1;

        int sensorNumber;
        return int.TryParse(objectName.Substring(underscoreIndex + 1), out sensorNumber)
            ? sensorNumber - 1
            : -1;
    }

    void AutoAssignBone()
    {
        if (keyBone != null && keyBody != null)
            return;

        int resolvedKeyIndex = keyIndex >= 0 ? keyIndex : ParseKeyIndexFromName(gameObject.name);
        if (resolvedKeyIndex < 0)
            return;

        Transform pianoRoot = null;
        if (m_Piano != null)
            pianoRoot = m_Piano.transform;
        else
        {
            Piano piano = GetComponentInParent<Piano>();
            if (piano != null)
                pianoRoot = piano.transform;
        }

        if (pianoRoot == null)
            pianoRoot = transform.root;

        Transform resolvedBone = pianoRoot != null
            ? pianoRoot.Find(string.Format("{0}/key_{1}", PianoRigRootPath, resolvedKeyIndex + 1))
            : null;
        if (resolvedBone == null)
            return;

        if (keyBone == null)
            keyBone = resolvedBone;

        if (keyBody == null)
            keyBody = resolvedBone.GetComponent<Rigidbody>();
    }

    float ComputeColliderPress(Collider other, float entranceDepth, float fullDepth)
    {
        if (!TryGetLocalSphereData(other, out Vector3 localCenter, out Vector3 localRadii))
            return 0f;

        float localDepth = GetAxisComponent(localCenter, pressLocalAxis);
        float localRadius = GetAxisComponent(localRadii, pressLocalAxis);
        float deepestDepth = pressAxisPositive
            ? localDepth + localRadius
            : localDepth - localRadius;
        float startDepth = Mathf.Lerp(entranceDepth, fullDepth, pressStartDepthNormalized);
        float depthRange = fullDepth - startDepth;
        if (Mathf.Abs(depthRange) < Mathf.Epsilon)
            return 0f;

        float pointDepth = ClampBetween(deepestDepth, entranceDepth, fullDepth);
        return Mathf.Clamp01((pointDepth - startDepth) / depthRange);
    }

    bool TryGetSensorDepthRange(out float entranceDepth, out float fullDepth)
    {
        entranceDepth = 0f;
        fullDepth = 0f;

        if (triggerCollider == null)
            return false;

        Vector3 center = triggerCollider.center;
        Vector3 size = triggerCollider.size;
        float axisCenter = GetAxisComponent(center, pressLocalAxis);
        float axisSize = GetAxisComponent(size, pressLocalAxis);
        if (!IsFiniteVector(center) || !IsFiniteVector(size) || axisSize <= 0f)
            return false;

        float halfDepth = axisSize * 0.5f;
        float negativeEndDepth = axisCenter - halfDepth;
        float positiveEndDepth = axisCenter + halfDepth;
        entranceDepth = pressAxisPositive ? negativeEndDepth : positiveEndDepth;
        float deepEndDepth = pressAxisPositive ? positiveEndDepth : negativeEndDepth;
        fullDepth = Mathf.Lerp(entranceDepth, deepEndDepth, fullPressDepthNormalized);
        return !Mathf.Approximately(fullDepth, entranceDepth);
    }

    bool IsStillCapturedByBox(Collider other, float entranceDepth)
    {
        if (!TryGetLocalSphereData(other, out Vector3 localCenter, out Vector3 localRadii))
            return false;

        float localDepth = GetAxisComponent(localCenter, pressLocalAxis);
        float localRadius = GetAxisComponent(localRadii, pressLocalAxis);
        float shallowestDepth = pressAxisPositive
            ? localDepth - localRadius
            : localDepth + localRadius;
        if (pressAxisPositive)
        {
            if (shallowestDepth < entranceDepth - CaptureTolerance)
                return false;
        }
        else if (shallowestDepth > entranceDepth + CaptureTolerance)
        {
            return false;
        }

        Vector3 boxCenter = triggerCollider.center;
        Vector3 boxHalfSize = triggerCollider.size * 0.5f;
        for (int axisIndex = 0; axisIndex < 3; axisIndex++)
        {
            PressLocalAxis axis = (PressLocalAxis)axisIndex;
            if (axis == pressLocalAxis)
                continue;

            float localOffset = Mathf.Abs(GetAxisComponent(localCenter, axis) - GetAxisComponent(boxCenter, axis));
            float maxOffset = GetAxisComponent(boxHalfSize, axis) + GetAxisComponent(localRadii, axis) + CaptureTolerance;
            if (localOffset > maxOffset)
                return false;
        }

        return true;
    }

    bool TryGetLocalSphereData(Collider other, out Vector3 localCenter, out Vector3 localRadii)
    {
        localCenter = Vector3.zero;
        localRadii = Vector3.zero;

        if (triggerCollider == null || other == null)
            return false;

        SphereCollider sphere = other as SphereCollider;
        if (sphere == null)
            return false;

        Transform triggerTransform = triggerCollider.transform;
        Transform sphereTransform = sphere.transform;
        Vector3 sphereScale = sphereTransform.lossyScale;
        float worldRadius = sphere.radius * Mathf.Max(Mathf.Abs(sphereScale.x), Mathf.Abs(sphereScale.y), Mathf.Abs(sphereScale.z));
        if (!float.IsFinite(worldRadius))
            return false;

        Vector3 worldCenter = sphereTransform.TransformPoint(sphere.center);
        localCenter = triggerTransform.InverseTransformPoint(worldCenter);
        if (!IsFiniteVector(localCenter))
            return false;

        Vector3 triggerScale = triggerTransform.lossyScale;
        float scaleX = Mathf.Abs(triggerScale.x);
        float scaleY = Mathf.Abs(triggerScale.y);
        float scaleZ = Mathf.Abs(triggerScale.z);
        if (scaleX < Mathf.Epsilon || scaleY < Mathf.Epsilon || scaleZ < Mathf.Epsilon)
            return false;

        localRadii = new Vector3(
            worldRadius / scaleX,
            worldRadius / scaleY,
            worldRadius / scaleZ);

        return IsFiniteVector(localRadii);
    }

    static float GetAxisComponent(Vector3 value, PressLocalAxis axis)
    {
        switch (axis)
        {
            case PressLocalAxis.X:
                return value.x;
            case PressLocalAxis.Y:
                return value.y;
            default:
                return value.z;
        }
    }

    static float ClampBetween(float value, float a, float b)
    {
        return Mathf.Clamp(value, Mathf.Min(a, b), Mathf.Max(a, b));
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

        if (!(other is SphereCollider))
            return false;

        if (other.GetComponentInParent<Fingertip>() == null)
            return false;

        if ((pressLayers.value & (1 << other.gameObject.layer)) == 0)
            return false;

        return !ShouldIgnoreCollider(other);
    }

    bool ShouldIgnoreCollider(Collider other)
    {
        if (ignoredRoot != null && other.transform.IsChildOf(ignoredRoot))
            return true;

        if (keyBone != null && other.transform.IsChildOf(keyBone.root))
            return true;

        if (keyBone != null && other.transform.IsChildOf(keyBone))
            return true;

        if (keyBody != null && other.attachedRigidbody == keyBody)
            return true;

        return false;
    }

    static bool IsFiniteVector(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
