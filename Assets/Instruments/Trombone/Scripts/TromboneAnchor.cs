using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Instruments
{
/// <summary>
/// TromboneAnchor scene root에 부착.
/// selectExited → pending 플래그만 설정. locomotionStarted가 pending 윈도우 안에 fire되면 trombone attach,
/// 윈도우 밖에서 fire되면 (anchor 외부로 이동) detach.
/// drum-stick 패턴(ARD 01) 답습: Instantiate/Destroy 대신 displace/restore 모델.
/// </summary>
[RequireComponent(typeof(TeleportationAnchor))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10005)]
public sealed class TromboneAnchor : MonoBehaviour
{
    [SerializeField] PlayHandPoseDriver leftPlayHandDriver;
    [SerializeField] PlayHandPoseDriver rightPlayHandDriver;

    [SerializeField] Transform tromboneRoot;
    [SerializeField] Transform mouthPieceOnTrombone;
    [SerializeField] Transform mouthOnPlayer;

    [SerializeField] GameObject leftPhysicsHand;
    [SerializeField] GameObject rightPhysicsHand;

    const int k_PendingAttachWindowFrames = 2;

    bool m_IsAttached;
    int m_PendingAttachFrame = -1;

    public bool IsAttached => m_IsAttached;

    Vector3 m_CachedRestorePosition;
    Quaternion m_CachedRestoreRotation;

    TeleportationAnchor m_Anchor;
    LocomotionProvider m_LocomotionProvider;

    void OnEnable()
    {
        m_Anchor = GetComponent<TeleportationAnchor>();
        m_Anchor.selectExited.AddListener(OnAnchorSelectExited);

        // UI [이동] 버튼 등 RequestTeleport() 직접 호출 경로에서 pending 윈도우를 설정.
        InstrumentTeleportLink.AnyAnchorTeleported += OnAnyAnchorTeleported;

        // BaseTeleportationInteractable이 teleportationProvider를 OnSelectExited 시점에 lazy-resolve하므로
        // OnEnable에서는 null인 경우가 많다. 일단 한 번 시도하고, 실패하면 OnAnchorSelectExited에서 재시도.
        EnsureLocomotionSubscription();
    }

    void OnDisable()
    {
        if (m_Anchor != null)
            m_Anchor.selectExited.RemoveListener(OnAnchorSelectExited);

        InstrumentTeleportLink.AnyAnchorTeleported -= OnAnyAnchorTeleported;

        if (m_LocomotionProvider != null)
        {
            m_LocomotionProvider.locomotionStarted -= OnLocomotionStarted;
            m_LocomotionProvider = null;
        }

        if (m_IsAttached)
            SetPhysicsHandsActive(true);
    }

    /// <summary>
    /// UI [이동] 버튼처럼 RequestTeleport()를 직접 호출하는 경로에서는 selectExited가 발화되지 않아
    /// pending 윈도우가 설정되지 않는다. AnyAnchorTeleported 이벤트는 RequestTeleport() → teleporting
    /// UnityEvent → InstrumentTeleportLink.OnTeleporting 체인으로 발화되므로, 이 핸들러에서 pending
    /// 윈도우를 설정해 OnLocomotionStarted의 attach 경로를 열어준다.
    /// </summary>
    void OnAnyAnchorTeleported(InstrumentBase instrument)
    {
        var link = GetComponent<InstrumentTeleportLink>();
        if (link == null || link.LinkedInstrument != instrument)
            return;

        // locomotionProvider가 아직 resolve되지 않았을 수 있으므로 여기서도 시도.
        EnsureLocomotionSubscription();
        m_PendingAttachFrame = Time.frameCount;
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
        // cancel exit는 XRI 측에서 SendTeleportRequest를 건너뛰므로
        // 본 핸들러도 pending을 설정하지 않는다.
        if (args.isCanceled)
            return;

        // BaseTeleportationInteractable.OnSelectExited가 teleportationProvider를 lazy-resolve한 직후이므로
        // 여기서 다시 한 번 구독을 시도한다(OnEnable 시점에 null이었던 케이스 self-heal).
        EnsureLocomotionSubscription();

        // 실제 attach는 locomotionStarted가 pending 윈도우 안에 fire될 때 수행된다.
        m_PendingAttachFrame = Time.frameCount;
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

            AttachTromboneToMouth();
            return;
        }

        // pending이 아니면 anchor 외부로 이동 — detach 경로.
        if (m_IsAttached)
            Detach();
    }

    void AttachTromboneToMouth()
    {
        if (tromboneRoot == null || mouthPieceOnTrombone == null || mouthOnPlayer == null)
            return;

        // 최초 attach 직전 원위치를 한 번만 캐시.
        m_CachedRestorePosition = tromboneRoot.position;
        m_CachedRestoreRotation = tromboneRoot.rotation;

        // 즉시 정렬 — LateUpdate와 동일 식으로 1 회 호출해 한 프레임 중간 상태 부재 보장.
        AlignTromboneToMouth();

        // 양손 GripPoseHand source override push.
        PushHandOverride(tromboneRoot, leftPlayHandDriver, "Body/GripPoseHand", "Body/GripPoseHand/L_Wrist");
        PushHandOverride(tromboneRoot, rightPlayHandDriver, "Slide/GripPoseHand", "Slide/GripPoseHand/R_Wrist");

        SetPhysicsHandsActive(false);
        m_IsAttached = true;
    }

    void PushHandOverride(Transform root, PlayHandPoseDriver driver, string gripPoseHandPath, string wristPath)
    {
        if (driver == null || root == null)
            return;

        var gripPoseHand = root.Find(gripPoseHandPath);
        var wristRoot = root.Find(wristPath);

        if (gripPoseHand != null && wristRoot != null)
            driver.PushSourceOverride(gripPoseHand, wristRoot);
    }

    void LateUpdate()
    {
        if (!m_IsAttached)
            return;

        AlignTromboneToMouth();
    }

    void AlignTromboneToMouth()
    {
        if (tromboneRoot == null || mouthPieceOnTrombone == null || mouthOnPlayer == null)
            return;

        // MouthPiece의 trombone-root-local offset을 사용해 역계산:
        // mouthOnPlayer.rotation = tromboneRoot.rotation * mouthPieceLocalRotation
        // → tromboneRoot.rotation = mouthOnPlayer.rotation * Inverse(mouthPieceLocalRotation)
        var mouthPieceLocalRotation = mouthPieceOnTrombone.localRotation;
        var mouthPieceLocalPosition = mouthPieceOnTrombone.localPosition;

        var targetRotation = mouthOnPlayer.rotation * Quaternion.Inverse(mouthPieceLocalRotation);
        var targetPosition = mouthOnPlayer.position - targetRotation * mouthPieceLocalPosition;

        tromboneRoot.SetPositionAndRotation(targetPosition, targetRotation);
    }

    void Detach()
    {
        SetPhysicsHandsActive(true);

        // pop → restore 순서 (deterministic, PlayHand fallback 깜빡임 방지).
        if (leftPlayHandDriver != null)
            leftPlayHandDriver.PopSourceOverride();
        if (rightPlayHandDriver != null)
            rightPlayHandDriver.PopSourceOverride();

        tromboneRoot.SetPositionAndRotation(m_CachedRestorePosition, m_CachedRestoreRotation);

        m_IsAttached = false;
    }
}
}
