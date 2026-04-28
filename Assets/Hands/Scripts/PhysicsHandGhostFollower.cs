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

    struct OneEuroFilterVector3
    {
        public float mincutoff;
        public float beta;
        public float dcutoff;
        Vector3 m_PrevX;
        Vector3 m_PrevDx;
        bool m_Initialized;

        public Vector3 Filter(Vector3 x, float dt)
        {
            if (!m_Initialized)
            {
                m_PrevX = x;
                m_PrevDx = Vector3.zero;
                m_Initialized = true;
                return x;
            }
            var dx = (x - m_PrevX) / dt;
            var alphaD = AlphaFor(dcutoff, dt);
            var dxHat = Vector3.Lerp(m_PrevDx, dx, alphaD);
            var cutoff = mincutoff + beta * dxHat.magnitude;
            var alpha = AlphaFor(cutoff, dt);
            var xHat = Vector3.Lerp(m_PrevX, x, alpha);
            m_PrevX = xHat;
            m_PrevDx = dxHat;
            return xHat;
        }

        public void Reset() => m_Initialized = false;
    }

    struct OneEuroFilterQuaternion
    {
        public float mincutoff;
        public float beta;
        public float dcutoff;
        Quaternion m_PrevQ;
        float m_PrevAngularSpeed;
        bool m_Initialized;

        public Quaternion Filter(Quaternion q, float dt)
        {
            if (!m_Initialized)
            {
                m_PrevQ = q;
                m_PrevAngularSpeed = 0f;
                m_Initialized = true;
                return q;
            }
            var delta = q * Quaternion.Inverse(m_PrevQ);
            delta.ToAngleAxis(out float angleDeg, out _);
            if (angleDeg > 180f) angleDeg -= 360f;
            var omega = Mathf.Abs(angleDeg) * Mathf.Deg2Rad / dt;
            var alphaD = AlphaFor(dcutoff, dt);
            var omegaHat = Mathf.Lerp(m_PrevAngularSpeed, omega, alphaD);
            var cutoff = mincutoff + beta * omegaHat;
            var alpha = AlphaFor(cutoff, dt);
            var qHat = Quaternion.Slerp(m_PrevQ, q, alpha);
            m_PrevQ = qHat;
            m_PrevAngularSpeed = omegaHat;
            return qHat;
        }

        public void Reset() => m_Initialized = false;
    }

    static float AlphaFor(float cutoff, float dt)
    {
        var tau = 1f / (2f * Mathf.PI * cutoff);
        return 1f / (1f + tau / dt);
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

    [Header("HandTracking Source Smoothing (OneEuro)")]
    [SerializeField] bool smoothHandTrackingSource = true;
    [SerializeField] float oneEuroMinCutoff = 1f;
    [SerializeField] float oneEuroBeta = 0.05f;
    [SerializeField] float oneEuroDCutoff = 1f;

    readonly List<Transform> m_PhysicsJoints = new List<Transform>();
    readonly List<Transform> m_HandTrackingJoints = new List<Transform>();
    readonly List<Transform> m_ControllerJoints = new List<Transform>();

    Rigidbody m_Rigidbody;
    bool m_IsInitialized;

    OneEuroFilterVector3 m_PosFilter;
    OneEuroFilterQuaternion m_RotFilter;
    GhostSourceMode m_LastFilteredSourceMode;

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

        // 필터 초기화 (SetActive 재활성 포함)
        m_PosFilter.Reset();
        m_RotFilter.Reset();
        m_LastFilteredSourceMode = GhostSourceMode.None;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    void FixedUpdate()
    {
        if (!TryEnsureInitialized())
            return;

        var sourceMode = ResolveSourceMode();
        if (sourceMode == GhostSourceMode.None)
            return;

        var sourceRoot = ResolveSourceRoot();
        if (sourceRoot == null)
            return;

        // source 전환 시 필터 상태 리셋 (활성화 직후 점프 방지)
        if (sourceMode != m_LastFilteredSourceMode)
        {
            m_PosFilter.Reset();
            m_RotFilter.Reset();
            m_LastFilteredSourceMode = sourceMode;
        }

        Vector3 targetPos = sourceRoot.position;
        Quaternion targetRot = sourceRoot.rotation;

        if (smoothHandTrackingSource && sourceMode == GhostSourceMode.HandTracking)
        {
            // 매 FixedUpdate에서 파라미터를 struct에 복사 (인스펙터 라이브 튜닝 허용)
            m_PosFilter.mincutoff = oneEuroMinCutoff;
            m_PosFilter.beta = oneEuroBeta;
            m_PosFilter.dcutoff = oneEuroDCutoff;
            m_RotFilter.mincutoff = oneEuroMinCutoff;
            m_RotFilter.beta = oneEuroBeta;
            m_RotFilter.dcutoff = oneEuroDCutoff;

            targetPos = m_PosFilter.Filter(targetPos, Time.fixedDeltaTime);
            targetRot = m_RotFilter.Filter(targetRot, Time.fixedDeltaTime);
        }

        // root 위치 추종 — velocity 기반 (필터링된 target 사용)
        var deltaPos = targetPos - m_Rigidbody.position;
        m_Rigidbody.linearVelocity = Vector3.ClampMagnitude(deltaPos / Time.fixedDeltaTime, maxLinearSpeed);

        // root 회전 추종 — angularVelocity 기반 (필터링된 target 사용)
        var deltaRot = targetRot * Quaternion.Inverse(m_Rigidbody.rotation);
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
