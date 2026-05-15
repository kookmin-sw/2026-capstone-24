using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 트럼본 앵커 진입·이탈을 감지하고, 진입 시 Mouth 추적·양손 grip override·PhysicsHand 비활성화를 수행한다.
    /// 이탈 시 모든 상태를 Start 시점 baseline으로 복귀한다.
    /// ARD 01: PhysicsHand SetActive 토글은 본 컴포넌트 단독 책임.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10005)]
    [AddComponentMenu("Instruments/Trombone Grip Controller")]
    public class TromboneGripController : MonoBehaviour
    {
        [Header("트럼본 참조")]
        [Tooltip("이 컨트롤러가 관리하는 Trombone 컴포넌트.")]
        [SerializeField] Trombone trombone;

        [Tooltip("Mouth 추적 시 SetPositionAndRotation 대상이 되는 트럼본 root Transform.")]
        [SerializeField] Transform tromboneRoot;

        [Header("Mouth 추적")]
        [Tooltip("VR Player Camera 자식. 트럼본이 매 프레임 정렬할 입 위치 기준점.")]
        [SerializeField] Transform mouthAnchor;

        [Tooltip("트럼본 자식. 매 프레임 mouthAnchor.world와 일치하도록 tromboneRoot가 역계산된다.")]
        [SerializeField] Transform mouthpieceAnchor;

        [Header("왼손 Grip")]
        [Tooltip("트럼본 바디 자식. 왼손 PlayHand source override root.")]
        [SerializeField] Transform leftGripRoot;

        [Tooltip("leftGripRoot 자식 WristRoot. PlayHandPoseDriver joint map 매칭 기준.")]
        [SerializeField] Transform leftGripWristRoot;

        [Header("오른손 Grip")]
        [Tooltip("슬라이드 GameObject 자식. 오른손 PlayHand source override root.")]
        [SerializeField] Transform rightGripRoot;

        [Tooltip("rightGripRoot 자식 WristRoot. PlayHandPoseDriver joint map 매칭 기준.")]
        [SerializeField] Transform rightGripWristRoot;

        [Header("PlayHand Drivers")]
        [SerializeField] PlayHandPoseDriver leftPlayHandDriver;
        [SerializeField] PlayHandPoseDriver rightPlayHandDriver;

        [Header("PhysicsHands")]
        [Tooltip("트럼본 활성 동안 SetActive(false) 대상. 이탈 시 SetActive(true) 복원.")]
        [SerializeField] GameObject leftPhysicsHand;
        [SerializeField] GameObject rightPhysicsHand;

        [Header("Provider")]
        [Tooltip("ActiveInstrumentChanged 이벤트 구독원.")]
        [SerializeField] TeleportInstrumentProvider activeInstrumentProvider;

        // Start 시점 캡처된 tromboneRoot world baseline.
        Vector3 m_BaselinePos;
        Quaternion m_BaselineRot;

        // Start 시점 캡처된 mouthpieceAnchor → tromboneRoot 상대 행렬 (역계산에 사용).
        Matrix4x4 m_MouthpieceLocalToTrombone;

        bool m_IsActive;

        void Start()
        {
            if (tromboneRoot != null)
                tromboneRoot.GetPositionAndRotation(out m_BaselinePos, out m_BaselineRot);

            // mouthpieceAnchor의 tromboneRoot 기준 상대 행렬을 1회 캐시.
            // mouthpieceAnchor가 tromboneRoot 아래 정적 계층에 있다는 가정 하.
            if (tromboneRoot != null && mouthpieceAnchor != null)
                m_MouthpieceLocalToTrombone = tromboneRoot.worldToLocalMatrix * mouthpieceAnchor.localToWorldMatrix;
        }

        void OnEnable()
        {
            if (activeInstrumentProvider != null)
                activeInstrumentProvider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
        }

        void OnDisable()
        {
            if (activeInstrumentProvider != null)
                activeInstrumentProvider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;

            // 활성 상태에서 비활성/언로드되면 Exit로 invariant 보전.
            if (m_IsActive)
                Exit();
        }

        void LateUpdate()
        {
            if (!m_IsActive)
                return;

            SyncMouthTracking();
        }

        void OnActiveInstrumentChanged(IActiveInstrument next)
        {
            bool nextIsMine = trombone != null && ReferenceEquals(next, (IActiveInstrument)trombone);

            if (!m_IsActive && nextIsMine)
            {
                Enter();
            }
            else if (m_IsActive && !nextIsMine)
            {
                Exit();
            }
            // 그 외 (inactive → not mine, active → mine 재진입): no-op.
            // 재텔레포트는 TeleportInstrumentProvider.ReferenceEquals 가드로 이벤트 자체가 fire 안 됨.
        }

        void Enter()
        {
            // Enter 4-step (Tech Spec §Data Flow)
            // 1. PhysicsHand 비활성화.
            if (leftPhysicsHand != null)
                leftPhysicsHand.SetActive(false);
            if (rightPhysicsHand != null)
                rightPhysicsHand.SetActive(false);

            // 2. Grip override push.
            if (leftPlayHandDriver != null)
                leftPlayHandDriver.PushSourceOverride(leftGripRoot, leftGripWristRoot);
            if (rightPlayHandDriver != null)
                rightPlayHandDriver.PushSourceOverride(rightGripRoot, rightGripWristRoot);

            // 3. 추적 활성.
            m_IsActive = true;

            // 4. 첫 프레임 정렬은 다음 LateUpdate에서 자동 적용.
        }

        void Exit()
        {
            // Exit 4-step (Tech Spec §Data Flow)
            // 1. tromboneRoot baseline 복귀.
            if (tromboneRoot != null)
                tromboneRoot.SetPositionAndRotation(m_BaselinePos, m_BaselineRot);

            // 2. Grip override pop (DrumKitStickAnchor.Detach 패턴: pop → SetActive 순서).
            if (leftPlayHandDriver != null)
                leftPlayHandDriver.PopSourceOverride();
            if (rightPlayHandDriver != null)
                rightPlayHandDriver.PopSourceOverride();

            // 3. PhysicsHand 복원 → PlayHandPoseDriver가 physics source로 자동 fallback.
            if (leftPhysicsHand != null)
                leftPhysicsHand.SetActive(true);
            if (rightPhysicsHand != null)
                rightPhysicsHand.SetActive(true);

            // 4. 추적 플래그 off.
            m_IsActive = false;
        }

        void SyncMouthTracking()
        {
            if (tromboneRoot == null || mouthAnchor == null || mouthpieceAnchor == null)
                return;

            // Tech Spec §Invariants 2: mouthpieceAnchor.world == mouthAnchor.world 유지.
            // tromboneRoot.world = mouthAnchor.world * mouthpieceLocalToTrombone.inverse
            // AnchoredStickGhostFollower의 ghost-wrist 역산 패턴 차용.
            Matrix4x4 targetWorld = mouthAnchor.localToWorldMatrix * m_MouthpieceLocalToTrombone.inverse;
            tromboneRoot.SetPositionAndRotation(
                targetWorld.GetPosition(),
                targetWorld.rotation
            );
        }
    }
}
