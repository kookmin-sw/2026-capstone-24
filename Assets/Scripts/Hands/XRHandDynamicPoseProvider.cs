using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

[DefaultExecutionOrder(-1100)]
public class XRHandDynamicPoseProvider : MonoBehaviour
{
    public enum RefreshReason
    {
        Default,
        LocomotionResume,
    }

    public enum PoseSourceKind
    {
        None,
        Controller,
        HandTracking,
    }

    public struct WristPoseState
    {
        public bool hasPose;
        public PoseSourceKind sourceKind;
        public uint sourceRevision;
        public Pose wristPose;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
    }

    const string VrPlayerPath = "VR Player/Camera Offset/Hands";
    const int LeftIndex = 0;
    const int RightIndex = 1;

    struct BufferedWristPose
    {
        public bool hasPose;
        public Pose pose;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public float sampleTime;
    }

    static XRHandDynamicPoseProvider s_Instance;
    static readonly List<XRHandSubsystem> s_SubsystemsReuse = new List<XRHandSubsystem>();

    readonly BufferedWristPose[] m_TrackingWristPoses = new BufferedWristPose[2];
    readonly BufferedWristPose[] m_ControllerWristPoses = new BufferedWristPose[2];
    readonly WristPoseState[] m_ResolvedStates = new WristPoseState[2];

    XRHandSubsystem m_Subsystem;
    XROrigin m_XrOrigin;
    Transform m_LeftControllerWrist;
    Transform m_RightControllerWrist;

    public static XRHandDynamicPoseProvider GetOrCreate()
    {
        if (s_Instance != null)
            return s_Instance;

        s_Instance = FindFirstObjectByType<XRHandDynamicPoseProvider>();
        if (s_Instance != null)
            return s_Instance;

        GameObject providerObject = new GameObject(nameof(XRHandDynamicPoseProvider));
        providerObject.hideFlags = HideFlags.HideInHierarchy;
        s_Instance = providerObject.AddComponent<XRHandDynamicPoseProvider>();
        return s_Instance;
    }

    void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        TryAssignSceneReferences();
    }

    void Update()
    {
        RefreshNow();
    }

    void OnDisable()
    {
        UnsubscribeSubsystem();
        if (s_Instance == this)
            s_Instance = null;
    }

    public bool TryGetWristState(bool isLeft, out WristPoseState state)
    {
        state = m_ResolvedStates[isLeft ? LeftIndex : RightIndex];
        return state.hasPose;
    }

    public void RefreshNow(RefreshReason reason = RefreshReason.Default)
    {
        TryAssignSceneReferences();
        EnsureSubsystem();

        bool zeroVelocity = reason == RefreshReason.LocomotionResume;
        SampleControllerWrist(LeftIndex, m_LeftControllerWrist, zeroVelocity);
        SampleControllerWrist(RightIndex, m_RightControllerWrist, zeroVelocity);
        ResolveState(LeftIndex, zeroVelocity, zeroVelocity);
        ResolveState(RightIndex, zeroVelocity, zeroVelocity);
    }

    void TryAssignSceneReferences()
    {
        if (m_XrOrigin == null)
            m_XrOrigin = FindFirstObjectByType<XROrigin>();

        if (m_LeftControllerWrist == null)
            m_LeftControllerWrist = FindSceneTransform($"{VrPlayerPath}/Left/LeftControllerHandRoot/LeftControllerGhostHand/L_Wrist");

        if (m_RightControllerWrist == null)
            m_RightControllerWrist = FindSceneTransform($"{VrPlayerPath}/Right/RightControllerHandRoot/RightControllerGhostHand/R_Wrist");
    }

    void EnsureSubsystem()
    {
        if (m_Subsystem != null && m_Subsystem.running)
            return;

        SubsystemManager.GetSubsystems(s_SubsystemsReuse);
        for (int i = 0; i < s_SubsystemsReuse.Count; i++)
        {
            XRHandSubsystem candidate = s_SubsystemsReuse[i];
            if (!candidate.running)
                continue;

            if (m_Subsystem == candidate)
                return;

            UnsubscribeSubsystem();
            m_Subsystem = candidate;
            m_Subsystem.updatedHands += OnUpdatedHands;
            return;
        }
    }

    void UnsubscribeSubsystem()
    {
        if (m_Subsystem == null)
            return;

        m_Subsystem.updatedHands -= OnUpdatedHands;
        m_Subsystem = null;
    }

    void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags successFlags, XRHandSubsystem.UpdateType updateType)
    {
        if (updateType != XRHandSubsystem.UpdateType.Dynamic)
            return;

        UpdateTrackingPose(
            LeftIndex,
            subsystem.leftHand,
            (successFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0);

        UpdateTrackingPose(
            RightIndex,
            subsystem.rightHand,
            (successFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0);

        ResolveState(LeftIndex);
        ResolveState(RightIndex);
    }

    void UpdateTrackingPose(int index, XRHand hand, bool jointsUpdated)
    {
        if (!hand.isTracked || !jointsUpdated)
        {
            m_TrackingWristPoses[index].hasPose = false;
            return;
        }

        XRHandJoint wristJoint = hand.GetJoint(XRHandJointID.Wrist);
        if (!wristJoint.TryGetPose(out Pose wristOriginPose))
        {
            m_TrackingWristPoses[index].hasPose = false;
            return;
        }

        BufferedWristPose bufferedPose = m_TrackingWristPoses[index];
        bufferedPose.hasPose = true;
        bufferedPose.pose = wristOriginPose;
        bufferedPose.linearVelocity = wristJoint.TryGetLinearVelocity(out Vector3 linearVelocity) ? linearVelocity : Vector3.zero;
        bufferedPose.angularVelocity = wristJoint.TryGetAngularVelocity(out Vector3 angularVelocity) ? angularVelocity : Vector3.zero;
        bufferedPose.sampleTime = Time.time;
        m_TrackingWristPoses[index] = bufferedPose;
    }

    void SampleControllerWrist(int index, Transform wrist, bool zeroVelocity = false)
    {
        if (wrist == null || !wrist.gameObject.activeInHierarchy)
        {
            m_ControllerWristPoses[index].hasPose = false;
            return;
        }

        Pose currentPose = TransformWorldPoseToOrigin(new Pose(wrist.position, wrist.rotation));
        float currentTime = Time.time;

        BufferedWristPose previousPose = m_ControllerWristPoses[index];
        Vector3 linearVelocity = Vector3.zero;
        Vector3 angularVelocity = Vector3.zero;
        if (previousPose.hasPose && !zeroVelocity)
        {
            float deltaTime = currentTime - previousPose.sampleTime;
            if (deltaTime > Mathf.Epsilon)
            {
                linearVelocity = (currentPose.position - previousPose.pose.position) / deltaTime;
                angularVelocity = CalculateAngularVelocity(previousPose.pose.rotation, currentPose.rotation, deltaTime);
            }
        }

        previousPose.hasPose = true;
        previousPose.pose = currentPose;
        previousPose.linearVelocity = linearVelocity;
        previousPose.angularVelocity = angularVelocity;
        previousPose.sampleTime = currentTime;
        m_ControllerWristPoses[index] = previousPose;
    }

    void ResolveState(int index, bool forceRevisionIncrement = false, bool zeroVelocity = false)
    {
        PoseSourceKind nextSourceKind = PoseSourceKind.None;
        BufferedWristPose sourcePose = default;

        if (m_TrackingWristPoses[index].hasPose)
        {
            nextSourceKind = PoseSourceKind.HandTracking;
            sourcePose = m_TrackingWristPoses[index];
        }
        else if (m_ControllerWristPoses[index].hasPose)
        {
            nextSourceKind = PoseSourceKind.Controller;
            sourcePose = m_ControllerWristPoses[index];
        }

        WristPoseState previousState = m_ResolvedStates[index];
        WristPoseState nextState = previousState;
        nextState.hasPose = nextSourceKind != PoseSourceKind.None;
        nextState.sourceKind = nextSourceKind;
        nextState.wristPose = nextState.hasPose ? TransformOriginPoseToWorld(sourcePose.pose) : default;

        if (zeroVelocity)
        {
            nextState.linearVelocity = Vector3.zero;
            nextState.angularVelocity = Vector3.zero;
        }
        else
        {
            Quaternion originRotation = GetOriginRotation();
            nextState.linearVelocity = originRotation * sourcePose.linearVelocity;
            nextState.angularVelocity = originRotation * sourcePose.angularVelocity;
        }

        bool sourceChanged = previousState.sourceKind != nextSourceKind;
        bool reacquired = !previousState.hasPose && nextState.hasPose;
        if (sourceChanged || reacquired || forceRevisionIncrement)
            nextState.sourceRevision = previousState.sourceRevision + 1;

        m_ResolvedStates[index] = nextState;
    }

    Pose TransformOriginPoseToWorld(Pose originSpacePose)
    {
        if (m_XrOrigin == null || m_XrOrigin.Origin == null)
            return originSpacePose;

        Transform originTransform = m_XrOrigin.Origin.transform;
        Pose xrOriginPose = new Pose(originTransform.position, originTransform.rotation);
        return originSpacePose.GetTransformedBy(xrOriginPose);
    }

    Pose TransformWorldPoseToOrigin(Pose worldPose)
    {
        if (m_XrOrigin == null || m_XrOrigin.Origin == null)
            return worldPose;

        Transform originTransform = m_XrOrigin.Origin.transform;
        Vector3 localPosition = originTransform.InverseTransformPoint(worldPose.position);
        Quaternion localRotation = Quaternion.Inverse(originTransform.rotation) * worldPose.rotation;
        return new Pose(localPosition, localRotation);
    }

    Quaternion GetOriginRotation()
    {
        if (m_XrOrigin == null || m_XrOrigin.Origin == null)
            return Quaternion.identity;

        return m_XrOrigin.Origin.transform.rotation;
    }

    static Vector3 CalculateAngularVelocity(Quaternion from, Quaternion to, float deltaTime)
    {
        Quaternion delta = to * Quaternion.Inverse(from);
        delta.ToAngleAxis(out float angleInDegrees, out Vector3 axis);

        if (float.IsNaN(axis.x) || axis.sqrMagnitude < Mathf.Epsilon)
            return Vector3.zero;

        if (angleInDegrees > 180f)
            angleInDegrees -= 360f;

        return axis.normalized * (angleInDegrees * Mathf.Deg2Rad / deltaTime);
    }

    static Transform FindSceneTransform(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0)
            return null;

        GameObject rootObject = GameObject.Find(parts[0]);
        if (rootObject == null)
            return null;

        Transform current = rootObject.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null)
                return null;
        }

        return current;
    }
}
