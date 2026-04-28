using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class PhysicsHandGhostFollower : MonoBehaviour
{
    enum GhostSourceMode
    {
        None,
        HandTracking,
        Controller,
    }

    [SerializeField]
    Transform physicsWristRoot;

    [SerializeField]
    GameObject handTrackingRoot;

    [SerializeField]
    Transform handTrackingGhostRoot;

    [SerializeField]
    Transform handTrackingGhostWristRoot;

    [SerializeField]
    GameObject controllerRoot;

    [SerializeField]
    Transform controllerGhostRoot;

    [SerializeField]
    Transform controllerGhostWristRoot;

    [SerializeField]
    float maxLinearSpeed = 20f;

    [SerializeField]
    float maxAngularSpeed = 50f;

    readonly List<Transform> m_PhysicsJoints = new List<Transform>();
    readonly List<Transform> m_HandTrackingJoints = new List<Transform>();
    readonly List<Transform> m_ControllerJoints = new List<Transform>();

    Rigidbody m_Rigidbody;
    bool m_IsInitialized;

    void OnEnable()
    {
        CacheComponents();
        Application.onBeforeRender += OnBeforeRender;

        // 첫 프레임 점프 흡수: rigidbody를 ghost 위치로 즉시 동기화
        var sourceRoot = ResolveSourceRoot();
        if (sourceRoot != null && m_Rigidbody != null)
        {
            m_Rigidbody.position = sourceRoot.position;
            m_Rigidbody.rotation = sourceRoot.rotation;
        }
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    void FixedUpdate()
    {
        if (!TryEnsureInitialized())
            return;

        var sourceRoot = ResolveSourceRoot();
        if (sourceRoot == null)
            return;

        // root 위치 추종 — velocity 기반
        var deltaPos = sourceRoot.position - m_Rigidbody.position;
        m_Rigidbody.linearVelocity = Vector3.ClampMagnitude(deltaPos / Time.fixedDeltaTime, maxLinearSpeed);

        // root 회전 추종 — angularVelocity 기반
        var deltaRot = sourceRoot.rotation * Quaternion.Inverse(m_Rigidbody.rotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        var angularVel = axis.normalized * (angleDeg * Mathf.Deg2Rad / Time.fixedDeltaTime);
        m_Rigidbody.angularVelocity = Vector3.ClampMagnitude(angularVel, maxAngularSpeed);
    }

    void LateUpdate()
    {
        if (!TryEnsureInitialized())
            return;

        if (SyncFingersFromActiveSource())
            Physics.SyncTransforms();
    }

    void OnBeforeRender()
    {
        if (!isActiveAndEnabled || !TryEnsureInitialized())
            return;

        SyncFingersFromActiveSource();
    }

    void CacheComponents()
    {
        if (m_Rigidbody == null)
            m_Rigidbody = GetComponent<Rigidbody>();
    }

    bool TryEnsureInitialized()
    {
        CacheComponents();

        if (m_IsInitialized)
            return true;

        if (m_Rigidbody == null)
            return false;

        if (!HasRequiredReferences())
            return false;

        var physicsMap = BuildJointMap(physicsWristRoot);
        var handTrackingMap = BuildJointMap(handTrackingGhostWristRoot);
        var controllerMap = BuildJointMap(controllerGhostWristRoot);

        if (physicsMap.Count == 0)
            return false;

        m_PhysicsJoints.Clear();
        m_HandTrackingJoints.Clear();
        m_ControllerJoints.Clear();

        foreach (var pair in physicsMap)
        {
            if (!handTrackingMap.TryGetValue(pair.Key, out var handTrackingJoint))
                return false;

            if (!controllerMap.TryGetValue(pair.Key, out var controllerJoint))
                return false;

            m_PhysicsJoints.Add(pair.Value);
            m_HandTrackingJoints.Add(handTrackingJoint);
            m_ControllerJoints.Add(controllerJoint);
        }

        m_IsInitialized = true;
        return true;
    }

    bool HasRequiredReferences()
    {
        return physicsWristRoot != null
            && handTrackingRoot != null
            && handTrackingGhostRoot != null
            && handTrackingGhostWristRoot != null
            && controllerRoot != null
            && controllerGhostRoot != null
            && controllerGhostWristRoot != null;
    }

    // 손가락 본만 ghost로부터 transform copy. root는 rigidbody가 관리하므로 건드리지 않는다.
    bool SyncFingersFromActiveSource()
    {
        var sourceMode = ResolveSourceMode();
        if (sourceMode == GhostSourceMode.None)
            return false;

        var sourceJoints = sourceMode == GhostSourceMode.HandTracking ? m_HandTrackingJoints : m_ControllerJoints;

        for (var i = 0; i < m_PhysicsJoints.Count; i++)
        {
            var targetJoint = m_PhysicsJoints[i];
            var sourceJoint = sourceJoints[i];
            targetJoint.localPosition = sourceJoint.localPosition;
            targetJoint.localRotation = sourceJoint.localRotation;
        }

        return true;
    }

    // OnEnable 및 FixedUpdate에서 source root를 구하기 위한 헬퍼
    Transform ResolveSourceRoot()
    {
        var sourceMode = ResolveSourceMode();
        if (sourceMode == GhostSourceMode.None)
            return null;
        return sourceMode == GhostSourceMode.HandTracking ? handTrackingGhostRoot : controllerGhostRoot;
    }

    GhostSourceMode ResolveSourceMode()
    {
        var isHandTrackingActive = handTrackingRoot != null && handTrackingRoot.activeInHierarchy;
        var isControllerActive = controllerRoot != null && controllerRoot.activeInHierarchy;

        if (isHandTrackingActive)
            return GhostSourceMode.HandTracking;

        if (isControllerActive)
            return GhostSourceMode.Controller;

        return GhostSourceMode.None;
    }

    static Dictionary<string, Transform> BuildJointMap(Transform root)
    {
        var jointMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
        AddJointRecursive(root, jointMap);
        return jointMap;
    }

    static void AddJointRecursive(Transform current, Dictionary<string, Transform> jointMap)
    {
        if (current == null || ShouldIgnoreTransform(current.name))
            return;

        jointMap[current.name] = current;

        for (var i = 0; i < current.childCount; i++)
            AddJointRecursive(current.GetChild(i), jointMap);
    }

    static bool ShouldIgnoreTransform(string transformName)
    {
        return transformName.StartsWith("__Collider_", StringComparison.Ordinal)
            || transformName == "LeftHand"
            || transformName == "RightHand";
    }
}
