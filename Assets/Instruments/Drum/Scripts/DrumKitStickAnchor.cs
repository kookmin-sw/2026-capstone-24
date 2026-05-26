using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Instruments
{
/// <summary>
/// DrumKitAnchor scene root에 부착.
/// selectExited → pending 플래그만 설정. locomotionStarted가 pending 윈도우 안에 fire되면 stick attach,
/// 윈도우 밖에서 fire되면 (anchor 외부로 이동) stick detach.
/// pending 우회는 SendTeleportRequest가 silent fail해도 stick이 잘못 attach되지 않도록 막는다.
/// ARD 01: anchor 자체-컴포넌트로 신호 발행.
/// ARD 02: PlayHandPoseDriver.PushSourceOverride / PopSourceOverride로 grip override 슬롯 점유.
/// ARD 03: Instantiate / Destroy 모델.
/// </summary>
[RequireComponent(typeof(TeleportationAnchor))]
[DisallowMultipleComponent]
public sealed class DrumKitStickAnchor : MonoBehaviour
{
    [SerializeField] GameObject leftStickPrefab;
    [SerializeField] GameObject rightStickPrefab;

    [SerializeField] Transform leftGhostWristSource;
    [SerializeField] Transform rightGhostWristSource;

    [SerializeField] PlayHandPoseDriver leftPlayHandDriver;
    [SerializeField] PlayHandPoseDriver rightPlayHandDriver;

    [SerializeField] GameObject leftPhysicsHand;
    [SerializeField] GameObject rightPhysicsHand;

    [SerializeField] HapticImpulsePlayer leftHapticImpulsePlayer;
    [SerializeField] HapticImpulsePlayer rightHapticImpulsePlayer;

    const int k_PendingAttachWindowFrames = 2;

    bool m_IsAttached;
    int m_PendingAttachFrame = -1;

    public bool IsAttached => m_IsAttached;
    GameObject m_LeftStickInstance;
    GameObject m_RightStickInstance;

    TeleportationAnchor m_Anchor;
    LocomotionProvider m_LocomotionProvider;

    void OnEnable()
    {
        m_Anchor = GetComponent<TeleportationAnchor>();
        m_Anchor.selectExited.AddListener(OnAnchorSelectExited);

        // BaseTeleportationInteractable이 teleportationProvider를 OnSelectExited 시점에 lazy-resolve하므로
        // OnEnable에서는 null인 경우가 많다. 일단 한 번 시도하고, 실패하면 OnAnchorSelectExited에서 재시도.
        EnsureLocomotionSubscription();
    }

    void OnDisable()
    {
        if (m_Anchor != null)
            m_Anchor.selectExited.RemoveListener(OnAnchorSelectExited);

        if (m_LocomotionProvider != null)
        {
            m_LocomotionProvider.locomotionStarted -= OnLocomotionStarted;
            m_LocomotionProvider = null;
        }

        if (m_IsAttached)
            SetPhysicsHandsActive(true);
    }

    void EnsureLocomotionSubscription()
    {
        if (m_LocomotionProvider != null || m_Anchor == null)
            return;

        m_LocomotionProvider = m_Anchor.teleportationProvider as LocomotionProvider;
        if (m_LocomotionProvider != null)
            m_LocomotionProvider.locomotionStarted += OnLocomotionStarted;
    }

    void SetPhysicsHandsActive(bool active)
    {
        if (leftPhysicsHand != null)
            leftPhysicsHand.SetActive(active);
        if (rightPhysicsHand != null)
            rightPhysicsHand.SetActive(active);
    }

    void OnAnchorSelectExited(SelectExitEventArgs args)
    {
        // cancel exit는 XRI 측에서 SendTeleportRequest를 건너뛰므로 (BaseTeleportationInteractable.OnSelectExited
        // line 418), 본 핸들러도 pending을 설정하지 않는다.
        if (args.isCanceled)
            return;

        // BaseTeleportationInteractable.OnSelectExited가 teleportationProvider를 lazy-resolve한 직후이므로
        // 여기서 다시 한 번 구독을 시도한다(OnEnable 시점에 null이었던 케이스 self-heal).
        EnsureLocomotionSubscription();

        // 실제 attach는 locomotionStarted가 pending 윈도우 안에 fire될 때 수행된다.
        // SendTeleportRequest가 silent fail (ray drift 등)하는 케이스에서는 locomotionStarted가 fire되지 않아
        // pending이 자연 expire → stick attach가 일어나지 않는다.
        m_PendingAttachFrame = Time.frameCount;
    }

    void AttachSticks()
    {
        if (leftStickPrefab == null || rightStickPrefab == null)
            return;

        m_LeftStickInstance = Instantiate(leftStickPrefab);
        if (leftGhostWristSource != null)
            m_LeftStickInstance.transform.SetPositionAndRotation(leftGhostWristSource.position, leftGhostWristSource.rotation);

        m_RightStickInstance = Instantiate(rightStickPrefab);
        if (rightGhostWristSource != null)
            m_RightStickInstance.transform.SetPositionAndRotation(rightGhostWristSource.position, rightGhostWristSource.rotation);

        BindStickAndPushOverride(m_LeftStickInstance, leftGhostWristSource, leftPlayHandDriver, "L_Wrist", leftHapticImpulsePlayer);
        BindStickAndPushOverride(m_RightStickInstance, rightGhostWristSource, rightPlayHandDriver, "R_Wrist", rightHapticImpulsePlayer);

        SetPhysicsHandsActive(false);
        m_IsAttached = true;
    }

    void BindStickAndPushOverride(GameObject stickInstance, Transform ghostWristSource, PlayHandPoseDriver driver, string wristChildName, HapticImpulsePlayer hapticImpulsePlayer)
    {
        if (stickInstance == null || driver == null)
            return;

        var follower = stickInstance.GetComponent<AnchoredStickGhostFollower>();
        if (follower == null)
            return;

        // GripPoseHand 자식 찾기
        var gripPoseHand = stickInstance.transform.Find("GripPoseHand");
        Transform stickHandWristRoot = null;
        if (gripPoseHand != null)
            stickHandWristRoot = gripPoseHand.Find(wristChildName);

        follower.Bind(ghostWristSource, driver, stickHandWristRoot);

        stickInstance.GetComponent<StickHitSweeper>()?.SetHapticImpulsePlayer(hapticImpulsePlayer);

        // PlayHand source override push
        // source root = GripPoseHand, source wrist root = GripPoseHand/L_Wrist (or R_Wrist)
        if (gripPoseHand != null && stickHandWristRoot != null)
            driver.PushSourceOverride(gripPoseHand, stickHandWristRoot);
    }

    void OnLocomotionStarted(LocomotionProvider _)
    {
        var frame = Time.frameCount;
        var withinPendingWindow =
            m_PendingAttachFrame >= 0 &&
            frame - m_PendingAttachFrame <= k_PendingAttachWindowFrames;

        if (withinPendingWindow)
        {
            // 본 anchor로 가는 텔레포트가 실제 발동됨.
            m_PendingAttachFrame = -1;

            // 같은 anchor 재텔레포트면 no-op.
            if (m_IsAttached)
                return;

            AttachSticks();
            return;
        }

        // pending이 아니면 anchor 외부로 이동 — 기존 detach 경로.
        if (m_IsAttached)
            Detach();
    }

    void Detach()
    {
        SetPhysicsHandsActive(true);

        // pop → destroy 순서 (deterministic, PlayHand fallback 깜빡임 방지).
        if (leftPlayHandDriver != null)
            leftPlayHandDriver.PopSourceOverride();
        if (rightPlayHandDriver != null)
            rightPlayHandDriver.PopSourceOverride();

        if (m_LeftStickInstance != null)
            Destroy(m_LeftStickInstance);
        if (m_RightStickInstance != null)
            Destroy(m_RightStickInstance);

        m_LeftStickInstance = null;
        m_RightStickInstance = null;
        m_IsAttached = false;
    }
}
}
