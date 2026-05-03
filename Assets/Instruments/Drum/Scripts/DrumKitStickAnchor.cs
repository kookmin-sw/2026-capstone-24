using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// DrumKitAnchor scene root에 부착.
/// selectExited → stick attach, locomotionStarted → stick detach.
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

    bool m_IsAttached;
    int m_AttachFrame;
    GameObject m_LeftStickInstance;
    GameObject m_RightStickInstance;

    TeleportationAnchor m_Anchor;
    LocomotionProvider m_LocomotionProvider;

    void OnEnable()
    {
        m_Anchor = GetComponent<TeleportationAnchor>();
        m_Anchor.selectExited.AddListener(OnAnchorSelectExited);

        // teleportationProvider는 TeleportationAnchor의 public property로 접근.
        m_LocomotionProvider = m_Anchor.teleportationProvider as LocomotionProvider;
        if (m_LocomotionProvider != null)
            m_LocomotionProvider.locomotionStarted += OnLocomotionStarted;
    }

    void OnDisable()
    {
        if (m_Anchor != null)
            m_Anchor.selectExited.RemoveListener(OnAnchorSelectExited);

        if (m_LocomotionProvider != null)
            m_LocomotionProvider.locomotionStarted -= OnLocomotionStarted;
    }

    void OnAnchorSelectExited(SelectExitEventArgs args)
    {
        // 이미 attach 상태면 재선택 방어.
        if (m_IsAttached)
            return;

        if (leftStickPrefab == null || rightStickPrefab == null)
            return;

        // Left stick instantiate
        m_LeftStickInstance = Instantiate(leftStickPrefab);
        if (leftGhostWristSource != null)
            m_LeftStickInstance.transform.SetPositionAndRotation(leftGhostWristSource.position, leftGhostWristSource.rotation);

        // Right stick instantiate
        m_RightStickInstance = Instantiate(rightStickPrefab);
        if (rightGhostWristSource != null)
            m_RightStickInstance.transform.SetPositionAndRotation(rightGhostWristSource.position, rightGhostWristSource.rotation);

        // Left: bind follower + push source override
        BindStickAndPushOverride(m_LeftStickInstance, leftGhostWristSource, leftPlayHandDriver, "L_Wrist");

        // Right: bind follower + push source override
        BindStickAndPushOverride(m_RightStickInstance, rightGhostWristSource, rightPlayHandDriver, "R_Wrist");

        m_IsAttached = true;
        m_AttachFrame = Time.frameCount;
    }

    void BindStickAndPushOverride(GameObject stickInstance, Transform ghostWristSource, PlayHandPoseDriver driver, string wristChildName)
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

        // PlayHand source override push
        // source root = GripPoseHand, source wrist root = GripPoseHand/L_Wrist (or R_Wrist)
        if (gripPoseHand != null && stickHandWristRoot != null)
            driver.PushSourceOverride(gripPoseHand, stickHandWristRoot);
    }

    void OnLocomotionStarted(LocomotionProvider _)
    {
        if (!m_IsAttached)
            return;

        // frame guard: attach와 동일 텔레포트의 locomotionStarted (Frame N 또는 N+1) 무시.
        var frame = Time.frameCount;
        if (frame == m_AttachFrame || frame == m_AttachFrame + 1)
            return;

        Detach();
    }

    void Detach()
    {
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
