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

    readonly List<Transform> m_PhysicsJoints = new List<Transform>();
    readonly List<Transform> m_HandTrackingJoints = new List<Transform>();
    readonly List<Transform> m_ControllerJoints = new List<Transform>();

    Rigidbody m_Rigidbody;
    bool m_IsInitialized;

    void OnEnable()
    {
        CacheComponents();
        Application.onBeforeRender += OnBeforeRender;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    void LateUpdate()
    {
        if (!TryEnsureInitialized())
            return;

        if (SyncFromActiveSource())
            Physics.SyncTransforms();
    }

    void OnBeforeRender()
    {
        if (!isActiveAndEnabled || !TryEnsureInitialized())
            return;

        SyncFromActiveSource();
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

    bool SyncFromActiveSource()
    {
        var sourceMode = ResolveSourceMode();
        if (sourceMode == GhostSourceMode.None)
            return false;

        var sourceRoot = sourceMode == GhostSourceMode.HandTracking ? handTrackingGhostRoot : controllerGhostRoot;
        var sourceJoints = sourceMode == GhostSourceMode.HandTracking ? m_HandTrackingJoints : m_ControllerJoints;

        transform.SetPositionAndRotation(sourceRoot.position, sourceRoot.rotation);

        for (var i = 0; i < m_PhysicsJoints.Count; i++)
        {
            var targetJoint = m_PhysicsJoints[i];
            var sourceJoint = sourceJoints[i];
            targetJoint.localPosition = sourceJoint.localPosition;
            targetJoint.localRotation = sourceJoint.localRotation;
        }

        return true;
    }

    GhostSourceMode ResolveSourceMode()
    {
        var isHandTrackingActive = handTrackingRoot.activeInHierarchy;
        var isControllerActive = controllerRoot.activeInHierarchy;

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
