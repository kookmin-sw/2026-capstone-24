using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10010)]
[DisallowMultipleComponent]
public sealed class PlayHandPoseDriver : MonoBehaviour
{
    struct JointPose
    {
        public Vector3 localPosition;
        public Quaternion localRotation;

        public JointPose(Vector3 localPosition, Quaternion localRotation)
        {
            this.localPosition = localPosition;
            this.localRotation = localRotation;
        }
    }

    [SerializeField]
    Transform sourceRoot;

    [SerializeField]
    Transform sourceWristRoot;

    [SerializeField]
    Transform targetWristRoot;

    [SerializeField]
    bool syncRootTransform = true;

    readonly List<Transform> m_SourceJoints = new List<Transform>();
    readonly List<Transform> m_TargetJoints = new List<Transform>();
    readonly List<JointPose> m_PoseBuffer = new List<JointPose>();

    bool m_IsInitialized;

    void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    void OnValidate()
    {
        ResetInitialization();
    }

    void LateUpdate()
    {
        if (!TryEnsureInitialized())
            return;

        SyncPose();
    }

    void OnBeforeRender()
    {
        if (!isActiveAndEnabled || !TryEnsureInitialized())
            return;

        SyncPose();
    }

    bool TryEnsureInitialized()
    {
        if (m_IsInitialized)
            return true;

        if (sourceRoot == null || sourceWristRoot == null || targetWristRoot == null)
            return false;

        var sourceMap = BuildJointMap(sourceWristRoot, ignoreColliderTransforms: true);
        var targetMap = BuildJointMap(targetWristRoot, ignoreColliderTransforms: false);

        if (sourceMap.Count == 0 || targetMap.Count == 0)
            return false;

        m_SourceJoints.Clear();
        m_TargetJoints.Clear();
        m_PoseBuffer.Clear();

        foreach (var pair in sourceMap)
        {
            if (!targetMap.TryGetValue(pair.Key, out var targetJoint))
            {
                ResetInitialization();
                return false;
            }

            m_SourceJoints.Add(pair.Value);
            m_TargetJoints.Add(targetJoint);
            m_PoseBuffer.Add(default);
        }

        m_IsInitialized = true;
        return true;
    }

    void SyncPose()
    {
        if (syncRootTransform)
            SyncRootTransform();

        ReadSourcePoseBuffer();
        ProcessPoseBuffer();
        ApplyPoseBuffer();
    }

    void SyncRootTransform()
    {
        transform.SetPositionAndRotation(sourceRoot.position, sourceRoot.rotation);
    }

    void ReadSourcePoseBuffer()
    {
        for (var i = 0; i < m_SourceJoints.Count; i++)
        {
            var sourceJoint = m_SourceJoints[i];
            m_PoseBuffer[i] = new JointPose(sourceJoint.localPosition, sourceJoint.localRotation);
        }
    }

    void ProcessPoseBuffer()
    {
        // Future pose modifiers can update m_PoseBuffer here before it is applied.
    }

    void ApplyPoseBuffer()
    {
        for (var i = 0; i < m_TargetJoints.Count; i++)
        {
            var targetJoint = m_TargetJoints[i];
            var pose = m_PoseBuffer[i];
            targetJoint.localPosition = pose.localPosition;
            targetJoint.localRotation = pose.localRotation;
        }
    }

    void ResetInitialization()
    {
        m_IsInitialized = false;
        m_SourceJoints.Clear();
        m_TargetJoints.Clear();
        m_PoseBuffer.Clear();
    }

    static Dictionary<string, Transform> BuildJointMap(Transform root, bool ignoreColliderTransforms)
    {
        var jointMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
        AddJointRecursive(root, jointMap, ignoreColliderTransforms);
        return jointMap;
    }

    static void AddJointRecursive(Transform current, Dictionary<string, Transform> jointMap, bool ignoreColliderTransforms)
    {
        if (current == null)
            return;

        if (ignoreColliderTransforms && current.name.StartsWith("__Collider_", StringComparison.Ordinal))
            return;

        jointMap[current.name] = current;

        for (var i = 0; i < current.childCount; i++)
            AddJointRecursive(current.GetChild(i), jointMap, ignoreColliderTransforms);
    }
}
