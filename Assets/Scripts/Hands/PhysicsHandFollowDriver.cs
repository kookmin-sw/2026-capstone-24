using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ConfigurableJoint))]
public class PhysicsHandFollowDriver : MonoBehaviour
{
    public enum HandSide
    {
        Left,
        Right
    }

    const string PhysicsHandLayerName = "PhysicsHand";

    [Header("References")]
    [SerializeField] HandSide side;
    [SerializeField] Rigidbody rootBody;
    [SerializeField] Transform physicsWrist;

    [Header("Linear Drive")]
    [SerializeField] float positionSpring = 12_000f;
    [SerializeField] float positionDamping = 300f;
    [SerializeField] float maxForce = 1_000_000f;

    [Header("Angular Drive")]
    [SerializeField] float rotationSpring = 10_000f;
    [SerializeField] float rotationDamping = 250f;
    [SerializeField] float maxTorque = 1_000_000f;

    [Header("Joint Stability")]
    [SerializeField] JointProjectionMode projectionMode = JointProjectionMode.PositionAndRotation;
    [SerializeField] float projectionDistance = 0.02f;
    [SerializeField] float projectionAngle = 5f;
    [SerializeField] int solverIterations = 12;
    [SerializeField] int solverVelocityIterations = 4;
    [SerializeField] float maxAngularVelocity = 100f;

    Vector3 m_PhysicsWristLocalPosition;
    Quaternion m_PhysicsWristLocalRotation = Quaternion.identity;

    ConfigurableJoint m_FollowJoint;
    Rigidbody m_TargetBody;
    XRHandDynamicPoseProvider m_PoseProvider;
    uint m_LastSourceRevision = uint.MaxValue;
    bool m_HasValidTarget;

    public HandSide Side => side;

    void Reset()
    {
        rootBody = GetComponent<Rigidbody>();
        physicsWrist = transform.Find(side == HandSide.Left ? "L_Wrist" : "R_Wrist");
    }

    void Awake()
    {
        TryAutoAssignReferences();
        CachePhysicsWristLocalPose();
        ConfigureRootBody();
        ConfigurePhysicsHandLayer();
        EnsurePoseProvider();
        EnsureTargetBody();
        EnsureFollowJoint();
        PhysicsHandRuntimeManager.GetOrCreate();
    }

    void OnEnable()
    {
        IgnoreOtherPhysicsHands();
    }

    void OnValidate()
    {
        positionSpring = Mathf.Max(0f, positionSpring);
        positionDamping = Mathf.Max(0f, positionDamping);
        rotationSpring = Mathf.Max(0f, rotationSpring);
        rotationDamping = Mathf.Max(0f, rotationDamping);
        maxForce = Mathf.Max(0f, maxForce);
        maxTorque = Mathf.Max(0f, maxTorque);
        projectionDistance = Mathf.Max(0f, projectionDistance);
        projectionAngle = Mathf.Clamp(projectionAngle, 0f, 180f);
        solverIterations = Mathf.Max(1, solverIterations);
        solverVelocityIterations = Mathf.Max(1, solverVelocityIterations);
        maxAngularVelocity = Mathf.Max(7f, maxAngularVelocity);

        TryAutoAssignReferences();

        if (Application.isPlaying)
        {
            CachePhysicsWristLocalPose();
            ConfigureRootBody();
            EnsureFollowJoint();
        }
    }

    void FixedUpdate()
    {
        if (rootBody == null || physicsWrist == null)
            return;

        EnsurePoseProvider();
        EnsureTargetBody();
        EnsureFollowJoint();

        if (!m_PoseProvider.TryGetWristState(side == HandSide.Left, out XRHandDynamicPoseProvider.WristPoseState wristState))
        {
            m_HasValidTarget = false;
            return;
        }

        Pose desiredRootPose = CalculateDesiredRootPose(wristState.wristPose);
        bool sourceChanged = wristState.sourceRevision != m_LastSourceRevision;
        if (!m_HasValidTarget || sourceChanged)
        {
            TeleportToRootPose(desiredRootPose);
            m_LastSourceRevision = wristState.sourceRevision;
            m_HasValidTarget = true;
        }
        else
        {
            m_TargetBody.MovePosition(desiredRootPose.position);
            m_TargetBody.MoveRotation(desiredRootPose.rotation);
        }

        Vector3 rootAngularVelocity = wristState.angularVelocity;
        Vector3 wristOffset = desiredRootPose.rotation * m_PhysicsWristLocalPosition;
        Vector3 rootLinearVelocity = wristState.linearVelocity - Vector3.Cross(rootAngularVelocity, wristOffset);

        m_FollowJoint.targetPosition = Vector3.zero;
        m_FollowJoint.targetRotation = Quaternion.identity;
        m_FollowJoint.targetVelocity = rootLinearVelocity - rootBody.linearVelocity;
        m_FollowJoint.targetAngularVelocity = rootAngularVelocity - rootBody.angularVelocity;
    }

    public void SnapToCurrentSource()
    {
        EnsurePoseProvider();
        EnsureTargetBody();
        EnsureFollowJoint();

        if (!m_PoseProvider.TryGetWristState(side == HandSide.Left, out XRHandDynamicPoseProvider.WristPoseState wristState))
            return;

        Pose desiredRootPose = CalculateDesiredRootPose(wristState.wristPose);
        TeleportToRootPose(desiredRootPose);
        m_LastSourceRevision = wristState.sourceRevision;
        m_HasValidTarget = true;
    }

    void TeleportToRootPose(Pose desiredRootPose)
    {
        rootBody.position = desiredRootPose.position;
        rootBody.rotation = desiredRootPose.rotation;
        rootBody.linearVelocity = Vector3.zero;
        rootBody.angularVelocity = Vector3.zero;
        rootBody.Sleep();
        rootBody.WakeUp();

        m_TargetBody.position = desiredRootPose.position;
        m_TargetBody.rotation = desiredRootPose.rotation;
    }

    Pose CalculateDesiredRootPose(Pose wristPose)
    {
        Quaternion desiredRootRotation = wristPose.rotation * Quaternion.Inverse(m_PhysicsWristLocalRotation);
        Vector3 desiredRootPosition = wristPose.position - desiredRootRotation * m_PhysicsWristLocalPosition;
        return new Pose(desiredRootPosition, desiredRootRotation);
    }

    void TryAutoAssignReferences()
    {
        if (rootBody == null)
            rootBody = GetComponent<Rigidbody>();

        if (physicsWrist == null)
            physicsWrist = transform.Find(side == HandSide.Left ? "L_Wrist" : "R_Wrist");
    }

    void CachePhysicsWristLocalPose()
    {
        if (physicsWrist == null)
            return;

        m_PhysicsWristLocalPosition = physicsWrist.localPosition;
        m_PhysicsWristLocalRotation = physicsWrist.localRotation;
    }

    void ConfigureRootBody()
    {
        if (rootBody == null)
            return;

        rootBody.useGravity = false;
        rootBody.isKinematic = false;
        rootBody.interpolation = RigidbodyInterpolation.Interpolate;
        rootBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rootBody.solverIterations = Mathf.Max(rootBody.solverIterations, solverIterations);
        rootBody.solverVelocityIterations = Mathf.Max(rootBody.solverVelocityIterations, solverVelocityIterations);
        rootBody.maxAngularVelocity = Mathf.Max(rootBody.maxAngularVelocity, maxAngularVelocity);
    }

    void EnsurePoseProvider()
    {
        if (m_PoseProvider == null)
            m_PoseProvider = XRHandDynamicPoseProvider.GetOrCreate();
    }

    void EnsureTargetBody()
    {
        if (m_TargetBody != null)
            return;

        string targetName = side == HandSide.Left ? "LeftPhysicsHandTarget" : "RightPhysicsHandTarget";
        Transform targetTransform = transform.parent != null ? transform.parent.Find(targetName) : null;
        if (targetTransform == null)
        {
            GameObject targetObject = new GameObject(targetName);
            targetObject.hideFlags = HideFlags.HideInHierarchy;
            targetTransform = targetObject.transform;
        }

        m_TargetBody = targetTransform.GetComponent<Rigidbody>();
        if (m_TargetBody == null)
            m_TargetBody = targetTransform.gameObject.AddComponent<Rigidbody>();

        m_TargetBody.isKinematic = true;
        m_TargetBody.useGravity = false;
        m_TargetBody.interpolation = RigidbodyInterpolation.None;
        m_TargetBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        m_TargetBody.position = rootBody != null ? rootBody.position : transform.position;
        m_TargetBody.rotation = rootBody != null ? rootBody.rotation : transform.rotation;
    }

    void EnsureFollowJoint()
    {
        if (rootBody == null)
            return;

        if (m_FollowJoint == null)
            m_FollowJoint = GetComponent<ConfigurableJoint>();

        m_FollowJoint.connectedBody = m_TargetBody;
        m_FollowJoint.autoConfigureConnectedAnchor = false;
        m_FollowJoint.anchor = Vector3.zero;
        m_FollowJoint.connectedAnchor = Vector3.zero;
        m_FollowJoint.axis = Vector3.right;
        m_FollowJoint.secondaryAxis = Vector3.up;
        m_FollowJoint.configuredInWorldSpace = false;
        m_FollowJoint.xMotion = ConfigurableJointMotion.Free;
        m_FollowJoint.yMotion = ConfigurableJointMotion.Free;
        m_FollowJoint.zMotion = ConfigurableJointMotion.Free;
        m_FollowJoint.angularXMotion = ConfigurableJointMotion.Free;
        m_FollowJoint.angularYMotion = ConfigurableJointMotion.Free;
        m_FollowJoint.angularZMotion = ConfigurableJointMotion.Free;
        m_FollowJoint.rotationDriveMode = RotationDriveMode.Slerp;
        m_FollowJoint.projectionMode = projectionMode;
        m_FollowJoint.projectionDistance = projectionDistance;
        m_FollowJoint.projectionAngle = projectionAngle;

        JointDrive linearDrive = new JointDrive
        {
            positionSpring = positionSpring,
            positionDamper = positionDamping,
            maximumForce = maxForce,
            useAcceleration = true,
        };

        JointDrive angularDrive = new JointDrive
        {
            positionSpring = rotationSpring,
            positionDamper = rotationDamping,
            maximumForce = maxTorque,
            useAcceleration = true,
        };

        m_FollowJoint.xDrive = linearDrive;
        m_FollowJoint.yDrive = linearDrive;
        m_FollowJoint.zDrive = linearDrive;
        m_FollowJoint.slerpDrive = angularDrive;
    }

    void ConfigurePhysicsHandLayer()
    {
        int physicsHandLayer = LayerMask.NameToLayer(PhysicsHandLayerName);
        if (physicsHandLayer < 0)
            return;

        SetLayerRecursively(transform, physicsHandLayer);
        Physics.IgnoreLayerCollision(physicsHandLayer, physicsHandLayer, true);
    }

    void IgnoreOtherPhysicsHands()
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>(true);
        PhysicsHandFollowDriver[] allDrivers = FindObjectsByType<PhysicsHandFollowDriver>(FindObjectsSortMode.None);
        foreach (PhysicsHandFollowDriver otherDriver in allDrivers)
        {
            if (otherDriver == this)
                continue;

            Collider[] otherColliders = otherDriver.GetComponentsInChildren<Collider>(true);
            foreach (Collider ownCollider in ownColliders)
            {
                foreach (Collider otherCollider in otherColliders)
                {
                    Physics.IgnoreCollision(ownCollider, otherCollider, true);
                }
            }
        }
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
