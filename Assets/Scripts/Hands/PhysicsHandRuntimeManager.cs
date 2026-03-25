using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

[DefaultExecutionOrder(-1200)]
public class PhysicsHandRuntimeManager : MonoBehaviour
{
    const string RuntimeContainerName = "PhysicsHandsRuntime";

    static PhysicsHandRuntimeManager s_Instance;

    readonly HashSet<LocomotionProvider> m_SubscribedProviders = new HashSet<LocomotionProvider>();
    readonly HashSet<LocomotionProvider> m_ActiveLocomotionProviders = new HashSet<LocomotionProvider>();

    [SerializeField] XRInputModalityManager modalityManager;
    [SerializeField] GameObject leftPhysicsHandPrefab;
    [SerializeField] GameObject rightPhysicsHandPrefab;

    XRHandDynamicPoseProvider m_PoseProvider;
    Transform m_RuntimeContainer;
    PhysicsHandFollowDriver m_LeftRuntimeHand;
    PhysicsHandFollowDriver m_RightRuntimeHand;
    bool m_PendingResumeAfterLocomotion;

    public static PhysicsHandRuntimeManager GetOrCreate()
    {
        if (s_Instance != null)
            return s_Instance;

        s_Instance = FindFirstObjectByType<PhysicsHandRuntimeManager>();
        if (s_Instance != null)
            return s_Instance;

        GameObject vrPlayer = GameObject.Find("VR Player");
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
        DiscoverLocomotionProviders();
        EnsureRuntimeHands();

        if (m_ActiveLocomotionProviders.Count == 0)
            TryActivateRuntimeHands();
    }

    void Update()
    {
        TryAssignReferences();
        DiscoverLocomotionProviders();
        EnsureRuntimeHands();

        if (m_ActiveLocomotionProviders.Count > 0)
            return;

        if (m_PendingResumeAfterLocomotion)
        {
            if (TryResumeRuntimeHands())
                m_PendingResumeAfterLocomotion = false;

            return;
        }

        TryActivateRuntimeHands();
    }

    void OnEnable()
    {
        if (s_Instance == this)
        {
            SubscribeModalityEvents();
            DiscoverLocomotionProviders();
        }
    }

    void OnDisable()
    {
        UnsubscribeModalityEvents();
        UnsubscribeLocomotionProviders();

        if (s_Instance == this)
            s_Instance = null;
    }

    void TryAssignReferences()
    {
        if (modalityManager == null)
            modalityManager = GetComponent<XRInputModalityManager>();

        if (modalityManager == null)
            modalityManager = FindFirstObjectByType<XRInputModalityManager>();

        if (m_PoseProvider == null)
            m_PoseProvider = XRHandDynamicPoseProvider.GetOrCreate();
    }

    void EnsureRuntimeHands()
    {
        if (m_RuntimeContainer == null)
        {
            GameObject container = GameObject.Find(RuntimeContainerName);
            if (container == null)
                container = new GameObject(RuntimeContainerName);

            m_RuntimeContainer = container.transform;
            m_RuntimeContainer.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        if (m_LeftRuntimeHand == null && leftPhysicsHandPrefab != null)
            m_LeftRuntimeHand = InstantiateRuntimeHand(leftPhysicsHandPrefab);

        if (m_RightRuntimeHand == null && rightPhysicsHandPrefab != null)
            m_RightRuntimeHand = InstantiateRuntimeHand(rightPhysicsHandPrefab);
    }

    PhysicsHandFollowDriver InstantiateRuntimeHand(GameObject prefab)
    {
        GameObject runtimeObject = Instantiate(prefab, m_RuntimeContainer);
        PhysicsHandFollowDriver runtimeHand = runtimeObject.GetComponent<PhysicsHandFollowDriver>();
        if (runtimeHand == null)
        {
            Destroy(runtimeObject);
            return null;
        }

        runtimeHand.gameObject.SetActive(false);
        return runtimeHand;
    }

    void TryActivateRuntimeHands()
    {
        if (m_PoseProvider == null)
            return;

        m_PoseProvider.RefreshNow();
        TryActivateRuntimeHand(m_LeftRuntimeHand, true);
        TryActivateRuntimeHand(m_RightRuntimeHand, false);
    }

    void TryActivateRuntimeHand(PhysicsHandFollowDriver hand, bool isLeft)
    {
        if (hand == null || hand.gameObject.activeSelf)
            return;

        if (!m_PoseProvider.TryGetWristState(isLeft, out _))
            return;

        hand.SnapToCurrentSource();
        hand.gameObject.SetActive(true);
    }

    void SuspendRuntimeHands()
    {
        if (m_LeftRuntimeHand != null)
            m_LeftRuntimeHand.gameObject.SetActive(false);

        if (m_RightRuntimeHand != null)
            m_RightRuntimeHand.gameObject.SetActive(false);
    }

    bool TryResumeRuntimeHands()
    {
        if (m_ActiveLocomotionProviders.Count > 0 || m_PoseProvider == null)
            return false;

        m_PoseProvider.RefreshNow(XRHandDynamicPoseProvider.RefreshReason.LocomotionResume);

        bool leftReady = TryResumeRuntimeHand(m_LeftRuntimeHand, true);
        bool rightReady = TryResumeRuntimeHand(m_RightRuntimeHand, false);

        return (m_LeftRuntimeHand == null || leftReady) && (m_RightRuntimeHand == null || rightReady);
    }

    bool TryResumeRuntimeHand(PhysicsHandFollowDriver hand, bool isLeft)
    {
        if (hand == null)
            return true;

        if (!m_PoseProvider.TryGetWristState(isLeft, out _))
            return false;

        if (!hand.gameObject.activeSelf)
        {
            hand.SnapToCurrentSource();
            hand.gameObject.SetActive(true);
        }

        return true;
    }

    void SubscribeModalityEvents()
    {
        if (modalityManager == null)
            return;

        modalityManager.trackedHandModeStarted.RemoveListener(OnModalityChanged);
        modalityManager.trackedHandModeEnded.RemoveListener(OnModalityChanged);
        modalityManager.motionControllerModeStarted.RemoveListener(OnModalityChanged);
        modalityManager.motionControllerModeEnded.RemoveListener(OnModalityChanged);

        modalityManager.trackedHandModeStarted.AddListener(OnModalityChanged);
        modalityManager.trackedHandModeEnded.AddListener(OnModalityChanged);
        modalityManager.motionControllerModeStarted.AddListener(OnModalityChanged);
        modalityManager.motionControllerModeEnded.AddListener(OnModalityChanged);
    }

    void UnsubscribeModalityEvents()
    {
        if (modalityManager == null)
            return;

        modalityManager.trackedHandModeStarted.RemoveListener(OnModalityChanged);
        modalityManager.trackedHandModeEnded.RemoveListener(OnModalityChanged);
        modalityManager.motionControllerModeStarted.RemoveListener(OnModalityChanged);
        modalityManager.motionControllerModeEnded.RemoveListener(OnModalityChanged);
    }

    void DiscoverLocomotionProviders()
    {
        LocomotionProvider[] providers = FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None);
        foreach (LocomotionProvider provider in providers)
        {
            if (provider == null || m_SubscribedProviders.Contains(provider))
                continue;

            provider.locomotionStarted += OnLocomotionStarted;
            provider.locomotionEnded += OnLocomotionEnded;
            m_SubscribedProviders.Add(provider);

            if (provider.isLocomotionActive)
                m_ActiveLocomotionProviders.Add(provider);
        }
    }

    void UnsubscribeLocomotionProviders()
    {
        foreach (LocomotionProvider provider in m_SubscribedProviders)
        {
            if (provider == null)
                continue;

            provider.locomotionStarted -= OnLocomotionStarted;
            provider.locomotionEnded -= OnLocomotionEnded;
        }

        m_SubscribedProviders.Clear();
        m_ActiveLocomotionProviders.Clear();
    }

    void OnModalityChanged()
    {
        if (m_ActiveLocomotionProviders.Count == 0)
            TryActivateRuntimeHands();
    }

    void OnLocomotionStarted(LocomotionProvider provider)
    {
        if (provider != null)
            m_ActiveLocomotionProviders.Add(provider);

        m_PendingResumeAfterLocomotion = false;
        SuspendRuntimeHands();
    }

    void OnLocomotionEnded(LocomotionProvider provider)
    {
        if (provider != null)
            m_ActiveLocomotionProviders.Remove(provider);

        if (m_ActiveLocomotionProviders.Count == 0)
            m_PendingResumeAfterLocomotion = true;
    }
}
