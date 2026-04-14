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
    [FormerlySerializedAs("pressStartDepthNormalized")]
    [SerializeField, Range(0f, 0.95f)] float pressStartDepthNormalized = 0.08f;
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
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();

        if (keyIndex < 0)
            keyIndex = ParseKeyIndexFromName(gameObject.name);

        m_Piano = GetComponentInParent<Piano>();
        AutoAssignBone();

        if (keyBone != null)
            m_RestRotation = keyBone.localRotation;
    }

    void OnValidate()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();

        if (keyIndex < 0)
            keyIndex = ParseKeyIndexFromName(gameObject.name);

        if (boneLocalAxis.sqrMagnitude < 0.0001f)
            boneLocalAxis = Vector3.right;

        maxPressDegrees = Mathf.Max(0f, maxPressDegrees);
        pressStartDepthNormalized = Mathf.Clamp(pressStartDepthNormalized, 0f, 0.95f);
        fullPressDepthNormalized = Mathf.Clamp(fullPressDepthNormalized, pressStartDepthNormalized + 0.01f, 1f);
        noteOnThreshold = Mathf.Clamp01(noteOnThreshold);
        noteOffThreshold = Mathf.Clamp(noteOffThreshold, 0f, noteOnThreshold);

        AutoAssignBone();
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
        if (other != null)
            m_Active.Remove(other);
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

        m_RemovalScratch.Clear();
        foreach (Collider other in m_Active)
        {
            if (!IsTrackedColliderValid(other))
                m_RemovalScratch.Add(other);
        }

        for (int i = 0; i < m_RemovalScratch.Count; i++)
            m_Active.Remove(m_RemovalScratch[i]);
        m_RemovalScratch.Clear();

        if (m_Active.Count == 0 || !TryGetSensorDepthRange(out float entranceDepth, out float fullDepth))
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
        if (triggerCollider == null || other == null)
            return 0f;

        Vector3 size = triggerCollider.size;
        if (!IsFiniteVector(size) || size.y <= 0f)
            return 0f;

        Transform triggerTransform = triggerCollider.transform;
        float probeOffset = Mathf.Max(size.y * 0.02f, 0.001f);
        Vector3 entranceLocalPoint = new Vector3(triggerCollider.center.x, entranceDepth - probeOffset, triggerCollider.center.z);
        Vector3 entranceWorldPoint = triggerTransform.TransformPoint(entranceLocalPoint);
        Vector3 closestWorldPoint = other.ClosestPoint(entranceWorldPoint);
        Vector3 closestLocalPoint = triggerTransform.InverseTransformPoint(closestWorldPoint);

        if (!IsFiniteVector(closestLocalPoint))
            return 0f;

        float startDepth = Mathf.Lerp(entranceDepth, fullDepth, pressStartDepthNormalized);
        float pointDepth = Mathf.Clamp(closestLocalPoint.y, entranceDepth, fullDepth);
        return Mathf.Clamp01(Mathf.InverseLerp(startDepth, fullDepth, pointDepth));
    }

    bool TryGetSensorDepthRange(out float entranceDepth, out float fullDepth)
    {
        entranceDepth = 0f;
        fullDepth = 0f;

        if (triggerCollider == null)
            return false;

        Vector3 center = triggerCollider.center;
        Vector3 size = triggerCollider.size;
        if (!IsFiniteVector(center) || !IsFiniteVector(size) || size.y <= 0f)
            return false;

        float halfDepth = size.y * 0.5f;
        entranceDepth = center.y - halfDepth;
        float deepEndDepth = center.y + halfDepth;
        fullDepth = Mathf.Lerp(entranceDepth, deepEndDepth, fullPressDepthNormalized);
        return fullDepth > entranceDepth;
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
