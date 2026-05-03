using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// drum_stick_L / drum_stick_R variant root에 부착.
/// attach 중 ghost wrist world pose를 매 frame stick root에 강제한다.
/// push/pop 책임은 DrumKitStickAnchor가 담당; 본 컴포넌트는 정상 경로에서 PlayHandPoseDriver를 직접 호출하지 않는다.
/// </summary>
[DefaultExecutionOrder(10005)]
[DisallowMultipleComponent]
public sealed class AnchoredStickGhostFollower : MonoBehaviour
{
    Transform m_GhostWristSource;

    // emergency pop 용으로만 보유 — 정상 경로에서는 호출 안 함.
    PlayHandPoseDriver m_PlayHandDriver;

    // stick-hand wrist root (GripPoseHand/L_Wrist 또는 R_Wrist)
    Transform m_StickHandWristRoot;

    bool m_IsBound;

    /// <summary>
    /// anchor 컴포넌트가 Instantiate 직후 호출.
    /// </summary>
    public void Bind(Transform ghostWristSource, PlayHandPoseDriver playHandDriver, Transform stickHandWristRoot)
    {
        m_GhostWristSource = ghostWristSource;
        m_PlayHandDriver = playHandDriver;
        m_StickHandWristRoot = stickHandWristRoot;
        m_IsBound = true;

        // attach 중 Rigidbody가 물리 영향 받지 않도록 kinematic 강제.
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        // XRGrabInteractable을 비활성화해 grip/trigger로 떼어지지 않게 한다.
        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.enabled = false;
    }

    void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    void LateUpdate()
    {
        SyncToGhost();
    }

    void OnBeforeRender()
    {
        if (!isActiveAndEnabled)
            return;
        SyncToGhost();
    }

    void SyncToGhost()
    {
        if (!m_IsBound || m_GhostWristSource == null)
            return;

        transform.SetPositionAndRotation(m_GhostWristSource.position, m_GhostWristSource.rotation);
    }
}
