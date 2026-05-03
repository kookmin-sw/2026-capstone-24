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

    // wrist의 stick-root-local 변환 (prefab-fixed 정적 pose) — Bind 시점 1회 캐시.
    // SyncToGhost가 stick root world pose를 ghostWrist.world × wristLocalToRoot⁻¹ 로 역산할 때 사용.
    Matrix4x4 m_WristLocalToRoot;
    bool m_HasWristCache;

    /// <summary>
    /// anchor 컴포넌트가 Instantiate 직후 호출.
    /// </summary>
    public void Bind(Transform ghostWristSource, PlayHandPoseDriver playHandDriver, Transform stickHandWristRoot)
    {
        m_GhostWristSource = ghostWristSource;
        m_PlayHandDriver = playHandDriver;
        m_StickHandWristRoot = stickHandWristRoot;
        m_IsBound = true;

        // wrist의 stick-root-local 변환을 1회 캐시.
        // wristLocalToRoot = stickRoot.worldToLocal × wrist.localToWorld
        // prefab-fixed 정적 값이라 이후 frame에서도 불변.
        if (m_StickHandWristRoot != null)
        {
            m_WristLocalToRoot = transform.worldToLocalMatrix * m_StickHandWristRoot.localToWorldMatrix;
            m_HasWristCache = true;
        }
        else
        {
            m_HasWristCache = false;
        }

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

        if (m_HasWristCache)
        {
            // stickRoot.world = ghostWrist.world × wristLocalToRoot⁻¹
            // 이렇게 두면 stick root 위에서 prefab-fixed wrist가 다시 ghostWrist.world와 정확히 일치.
            // 결과: PlayHandPoseDriver(syncRootTransform=true)가 source root(=GripPoseHand)를 따라가도
            // PlayHand world == ghostWrist world == 컨트롤러 위치로 정렬된다.
            var ghostWorld = Matrix4x4.TRS(m_GhostWristSource.position, m_GhostWristSource.rotation, Vector3.one);
            var stickWorld = ghostWorld * m_WristLocalToRoot.inverse;
            transform.SetPositionAndRotation(stickWorld.GetPosition(), stickWorld.rotation);
        }
        else
        {
            // fallback: Bind가 wrist를 못 받았을 때만. 정상 경로에서는 사용되지 않음.
            transform.SetPositionAndRotation(m_GhostWristSource.position, m_GhostWristSource.rotation);
        }
    }
}
