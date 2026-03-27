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
    Renderer[] m_Renderers = Array.Empty<Renderer>();
    Collider[] m_Colliders = Array.Empty<Collider>();
    bool m_IsInitialized;
    bool m_WarnedMissingReferences;
    bool m_WarnedBothActive;
    string m_LastInitializationError;

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

        if (m_Renderers.Length == 0)
            m_Renderers = GetComponentsInChildren<Renderer>(true);

        if (m_Colliders.Length == 0)
            m_Colliders = GetComponentsInChildren<Collider>(true);
    }

    bool TryEnsureInitialized()
    {
        CacheComponents();

        if (m_IsInitialized)
            return true;

        if (m_Rigidbody == null)
        {
            LogInitializationError($"[{nameof(PhysicsHandGhostFollower)}] {name} requires a Rigidbody on the same GameObject.");
            return false;
        }

        if (!HasRequiredReferences())
        {
            if (!m_WarnedMissingReferences)
            {
                Debug.LogWarning($"[{nameof(PhysicsHandGhostFollower)}] {name} has unassigned references. Assign them in the scene instance before use.", this);
                m_WarnedMissingReferences = true;
            }

            return false;
        }

        m_WarnedMissingReferences = false;

        var physicsMap = BuildJointMap(physicsWristRoot);
        var handTrackingMap = BuildJointMap(handTrackingGhostWristRoot);
        var controllerMap = BuildJointMap(controllerGhostWristRoot);

        if (physicsMap.Count == 0)
        {
            LogInitializationError($"[{nameof(PhysicsHandGhostFollower)}] {name} could not build a physics joint map from {physicsWristRoot.name}.");
            return false;
        }

        m_PhysicsJoints.Clear();
        m_HandTrackingJoints.Clear();
        m_ControllerJoints.Clear();

        var missingHandTrackingJoints = new List<string>();
        var missingControllerJoints = new List<string>();

        foreach (var pair in physicsMap)
        {
            if (!handTrackingMap.TryGetValue(pair.Key, out var handTrackingJoint))
                missingHandTrackingJoints.Add(pair.Key);

            if (!controllerMap.TryGetValue(pair.Key, out var controllerJoint))
                missingControllerJoints.Add(pair.Key);

            if (missingHandTrackingJoints.Count > 0 || missingControllerJoints.Count > 0)
                continue;

            m_PhysicsJoints.Add(pair.Value);
            m_HandTrackingJoints.Add(handTrackingJoint);
            m_ControllerJoints.Add(controllerJoint);
        }

        if (missingHandTrackingJoints.Count > 0)
        {
            LogInitializationError(
                $"[{nameof(PhysicsHandGhostFollower)}] {name} hand-tracking ghost wrist hierarchy is missing physics joints: {string.Join(", ", missingHandTrackingJoints)}");
            return false;
        }

        if (missingControllerJoints.Count > 0)
        {
            LogInitializationError(
                $"[{nameof(PhysicsHandGhostFollower)}] {name} controller ghost wrist hierarchy is missing physics joints: {string.Join(", ", missingControllerJoints)}");
            return false;
        }

        ConfigureRigidbody();
        m_LastInitializationError = null;
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
        {
            SetPhysicsHandActive(false);
            return false;
        }

        ConfigureRigidbody();
        SetPhysicsHandActive(true);

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

        if (isHandTrackingActive && isControllerActive)
        {
            if (!m_WarnedBothActive)
            {
                Debug.LogWarning(
                    $"[{nameof(PhysicsHandGhostFollower)}] {name} has both hand-tracking and controller roots active. Hand tracking will be used.",
                    this);
                m_WarnedBothActive = true;
            }

            return GhostSourceMode.HandTracking;
        }

        if (isHandTrackingActive)
        {
            m_WarnedBothActive = false;
            return GhostSourceMode.HandTracking;
        }

        if (isControllerActive)
        {
            m_WarnedBothActive = false;
            return GhostSourceMode.Controller;
        }

        m_WarnedBothActive = false;
        return GhostSourceMode.None;
    }

    void ConfigureRigidbody()
    {
        m_Rigidbody.isKinematic = true;
        m_Rigidbody.interpolation = RigidbodyInterpolation.None;
    }

    void SetPhysicsHandActive(bool active)
    {
        m_Rigidbody.detectCollisions = active;

        foreach (var renderer in m_Renderers)
        {
            if (renderer != null)
                renderer.enabled = active;
        }

        foreach (var collider in m_Colliders)
        {
            if (collider != null)
                collider.enabled = active;
        }
    }

    void LogInitializationError(string message)
    {
        if (m_LastInitializationError == message)
            return;

        m_LastInitializationError = message;
        Debug.LogError(message, this);
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
