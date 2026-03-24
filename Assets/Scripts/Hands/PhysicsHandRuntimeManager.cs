using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

[DefaultExecutionOrder(-1001)]
public class PhysicsHandRuntimeManager : MonoBehaviour
{
    const string VrPlayerName = "VR Player";
    const string LeftPhysicsHandName = "LeftPhysicsHand";
    const string RightPhysicsHandName = "RightPhysicsHand";

    static PhysicsHandRuntimeManager s_Instance;

    XRInputModalityManager m_ModalityManager;
    PhysicsHandFollowDriver m_LeftHand;
    PhysicsHandFollowDriver m_RightHand;
    bool m_InitialSnapDone;
    bool m_ModalityEventsSubscribed;

    public static PhysicsHandRuntimeManager GetOrCreate()
    {
        if (s_Instance != null)
            return s_Instance;

        s_Instance = FindFirstObjectByType<PhysicsHandRuntimeManager>();
        if (s_Instance != null)
            return s_Instance;

        GameObject vrPlayer = GameObject.Find(VrPlayerName);
        GameObject owner = vrPlayer != null ? vrPlayer : new GameObject(nameof(PhysicsHandRuntimeManager));
        s_Instance = owner.GetComponent<PhysicsHandRuntimeManager>();
        if (s_Instance == null)
            s_Instance = owner.AddComponent<PhysicsHandRuntimeManager>();

        return s_Instance;
    }

    void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(this);
            return;
        }

        s_Instance = this;
        TryAssignReferences();
        SubscribeModalityEvents();
    }

    void Start()
    {
        SnapAllToCurrentSource();
    }

    void Update()
    {
        TryAssignReferences();
        SubscribeModalityEvents();

        if (!m_InitialSnapDone)
            m_InitialSnapDone = SnapAllToCurrentSource();
    }

    void OnDisable()
    {
        UnsubscribeModalityEvents();
        if (s_Instance == this)
            s_Instance = null;
    }

    void TryAssignReferences()
    {
        if (m_ModalityManager == null)
            m_ModalityManager = FindFirstObjectByType<XRInputModalityManager>();

        if (m_LeftHand == null)
        {
            GameObject leftHandObject = GameObject.Find(LeftPhysicsHandName);
            if (leftHandObject != null)
                m_LeftHand = leftHandObject.GetComponent<PhysicsHandFollowDriver>();
        }

        if (m_RightHand == null)
        {
            GameObject rightHandObject = GameObject.Find(RightPhysicsHandName);
            if (rightHandObject != null)
                m_RightHand = rightHandObject.GetComponent<PhysicsHandFollowDriver>();
        }
    }

    void SubscribeModalityEvents()
    {
        if (m_ModalityManager == null || m_ModalityEventsSubscribed)
            return;

        m_ModalityManager.trackedHandModeStarted.AddListener(OnModalityChanged);
        m_ModalityManager.trackedHandModeEnded.AddListener(OnModalityChanged);
        m_ModalityManager.motionControllerModeStarted.AddListener(OnModalityChanged);
        m_ModalityManager.motionControllerModeEnded.AddListener(OnModalityChanged);
        m_ModalityEventsSubscribed = true;
    }

    void UnsubscribeModalityEvents()
    {
        if (m_ModalityManager == null || !m_ModalityEventsSubscribed)
            return;

        m_ModalityManager.trackedHandModeStarted.RemoveListener(OnModalityChanged);
        m_ModalityManager.trackedHandModeEnded.RemoveListener(OnModalityChanged);
        m_ModalityManager.motionControllerModeStarted.RemoveListener(OnModalityChanged);
        m_ModalityManager.motionControllerModeEnded.RemoveListener(OnModalityChanged);
        m_ModalityEventsSubscribed = false;
    }

    void OnModalityChanged()
    {
        SnapAllToCurrentSource();
    }

    bool SnapAllToCurrentSource()
    {
        bool snapped = false;

        if (m_LeftHand != null)
        {
            m_LeftHand.SnapToCurrentSource();
            snapped = true;
        }

        if (m_RightHand != null)
        {
            m_RightHand.SnapToCurrentSource();
            snapped = true;
        }

        return snapped;
    }
}
