using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// drum_stick_L / drum_stick_R variant root에 부착.
/// attach 중 ghost wrist world pose를 목표로 FixedUpdate에서 Rigidbody velocity/angularVelocity를 할당해 추종한다.
/// non-kinematic Rigidbody이므로 drum piece solid collider에 막혀 표면 정지 가능.
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
    // driver가 stick root world pose를 ghostWrist.world × wristLocalToRoot⁻¹ 로 역산할 때 사용.
    Matrix4x4 m_WristLocalToRoot;
    bool m_HasWristCache;

    // ghost wrist 이동량으로 계산한 attach 중 스틱 속도.
    // DrumHitZone이 이 값으로 hit 속도를 판정한다.
    Vector3 m_Velocity;
    Vector3 m_PrevGhostPos;
    bool m_HasPrevGhostPos;

    Rigidbody m_Rigidbody;

    public Vector3 Velocity => m_IsBound ? m_Velocity : Vector3.zero;

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

        // non-kinematic + 중력 없음으로 설정해 drum piece solid collider에 막혀 자연 정지하게 한다.
        m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody != null)
        {
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.useGravity = false;
        }

        m_HasPrevGhostPos = false;
        m_Velocity = Vector3.zero;

        // XRGrabInteractable을 비활성화해 grip/trigger로 떼어지지 않게 한다.
        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.enabled = false;

        // Bind 시점 1회 초기 정렬 — ghost wrist 기준으로 stick root를 정확히 맞춘다.
        // Instantiate 직후 DrumKitStickAnchor가 ghost wrist 위치로 SetPositionAndRotation하나
        // 그 시점은 m_WristLocalToRoot가 없어 stick 내부 wrist-local 차이만큼 어긋날 수 있다.
        if (m_GhostWristSource != null)
        {
            var ghostWorld = Matrix4x4.TRS(m_GhostWristSource.position, m_GhostWristSource.rotation, Vector3.one);
            Matrix4x4 stickWorld;
            if (m_HasWristCache)
                stickWorld = ghostWorld * m_WristLocalToRoot.inverse;
            else
                stickWorld = ghostWorld;
            transform.SetPositionAndRotation(stickWorld.GetPosition(), stickWorld.rotation);
        }
    }

    void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
        m_HasPrevGhostPos = false;
        m_Velocity = Vector3.zero;
    }

    void FixedUpdate()
    {
        if (!m_IsBound || m_GhostWristSource == null)
            return;

        // ghost wrist 이동량으로 Velocity 산출 (StickHitSweeper hit 판정용).
        Vector3 current = m_GhostWristSource.position;
        if (!m_HasPrevGhostPos)
        {
            m_PrevGhostPos = current;
            m_HasPrevGhostPos = true;
            m_Velocity = Vector3.zero;
        }
        else
        {
            float dt = Time.fixedDeltaTime;
            m_Velocity = dt > 0f ? (current - m_PrevGhostPos) / dt : Vector3.zero;
            m_PrevGhostPos = current;
        }

        // velocity driver: ghost wrist world pose에서 목표 stick world pose를 역산해
        // Rigidbody.linearVelocity / angularVelocity를 직접 할당한다.
        if (m_Rigidbody == null)
            return;

        var ghostWorld = Matrix4x4.TRS(m_GhostWristSource.position, m_GhostWristSource.rotation, Vector3.one);
        Matrix4x4 targetStickWorld;
        if (m_HasWristCache)
            targetStickWorld = ghostWorld * m_WristLocalToRoot.inverse;
        else
            targetStickWorld = ghostWorld;

        float fixedDt = Time.fixedDeltaTime;
        if (fixedDt <= 0f)
            return;

        // 선속도 할당
        Vector3 targetPos = targetStickWorld.GetPosition();
        Vector3 dPos = targetPos - m_Rigidbody.position;
        m_Rigidbody.linearVelocity = dPos / fixedDt;

        // 각속도 할당 (axis-angle, 180°+ wrap 처리)
        Quaternion targetRot = targetStickWorld.rotation;
        Quaternion dRot = targetRot * Quaternion.Inverse(m_Rigidbody.rotation);
        dRot.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
            angle -= 360f;
        m_Rigidbody.angularVelocity = axis * (angle * Mathf.Deg2Rad / fixedDt);
    }

    void LateUpdate()
    {
        // velocity driver 모드: transform 직접 변경 금지 (Tech Spec Invariants).
        // SyncToGhost 호출 제거됨.
    }

    void OnBeforeRender()
    {
        // velocity driver 모드: transform 직접 변경 금지 (Tech Spec Invariants).
        // SyncToGhost 호출 제거됨.
    }
}
