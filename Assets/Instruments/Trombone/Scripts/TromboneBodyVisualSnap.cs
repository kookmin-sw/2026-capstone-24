using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab의 Rig/Body GameObject에 부착.
    /// IsAttached=true 동안 Rig/Body의 표시 z회전을 TrombonePartialController.PartialIndex에 대응하는
    /// anchor 각도((PartialIndex - centerPartialIndex) * anglePerPartial)로 100ms exponential ease-out 보간.
    /// IsAttached=false 시 Body localEulerAngles를 attach 시점 캐시 값으로 원복.
    /// x/y 회전은 attach 시점 원본 값을 항상 유지(Invariant).
    /// </summary>
    [DefaultExecutionOrder(10006)]
    [DisallowMultipleComponent]
    public sealed class TromboneBodyVisualSnap : MonoBehaviour
    {
        [SerializeField] TromboneAnchor tromboneAnchor;
        [SerializeField] TrombonePartialController partialController;
        [SerializeField] Transform body;
        [SerializeField, Min(0f)] float easeDurationSeconds = 0.1f;
        [SerializeField, Range(1f, 10f)] float easeExponent = 4f;

        float m_StartAngleZ;
        float m_TargetAngleZ;
        float m_ElapsedSeconds;
        int m_LastPartialIndex = -1;
        bool m_IsBoosting;
        Vector3 m_OriginalLocalEulerAnglesAtAttach;
        bool m_HasOriginalCache;

        void Reset()
        {
            body = transform;
        }

        void OnEnable()
        {
            m_LastPartialIndex = -1;
            m_IsBoosting = false;
        }

        void LateUpdate()
        {
            if (body == null || tromboneAnchor == null || partialController == null)
                return;

            // IsAttached=false 분기: Body를 attach 시점 원본 값으로 원복
            if (!tromboneAnchor.IsAttached)
            {
                ApplyOriginalRotation();
                m_LastPartialIndex = -1;
                m_IsBoosting = false;
                return;
            }

            // attach 전이 감지 + 원본 캐시 (첫 LateUpdate: m_LastPartialIndex == -1)
            if (m_LastPartialIndex == -1)
            {
                m_OriginalLocalEulerAnglesAtAttach = body.localEulerAngles;
                m_HasOriginalCache = true;
            }

            // target anchor 계산
            int idx = partialController.PartialIndex;
            float targetZ = (idx - partialController.CenterPartialIndex) * partialController.AnglePerPartial;

            // PartialIndex 변경(또는 첫 프레임) 시 ease 재시작
            if (idx != m_LastPartialIndex)
            {
                m_StartAngleZ = CurrentDisplayAngleZ();
                m_TargetAngleZ = targetZ;
                m_ElapsedSeconds = 0f;
                m_IsBoosting = true;
                m_LastPartialIndex = idx;
            }

            // ease 진행
            if (m_IsBoosting)
            {
                m_ElapsedSeconds += Time.deltaTime;
                float t = (easeDurationSeconds > 0f) ? Mathf.Clamp01(m_ElapsedSeconds / easeDurationSeconds) : 1f;
                float easedT = (easeExponent > 0f) ? 1f - Mathf.Exp(-easeExponent * t) : t;
                // duration 끝 보정: t=1이면 정확히 target 적용
                if (t >= 1f)
                {
                    easedT = 1f;
                    m_IsBoosting = false;
                }
                float z = Mathf.LerpAngle(m_StartAngleZ, m_TargetAngleZ, easedT);
                ApplyZ(z);
            }
            else
            {
                // ease 종료 상태 — target 그대로 유지
                ApplyZ(m_TargetAngleZ);
            }
        }

        /// <summary>
        /// x/y는 attach 시점 원본 값을 유지하고 z만 변경. Invariant(x/y 변경 금지) 준수.
        /// </summary>
        void ApplyZ(float z)
        {
            var e = m_OriginalLocalEulerAnglesAtAttach;
            e.z = z;
            body.localEulerAngles = e;
        }

        /// <summary>
        /// Detach 후 매 프레임 호출 시 cached 원본으로 멱등 복원.
        /// </summary>
        void ApplyOriginalRotation()
        {
            if (m_HasOriginalCache)
                body.localEulerAngles = m_OriginalLocalEulerAnglesAtAttach;
        }

        /// <summary>
        /// 현재 body.localEulerAngles.z를 [-180, 180] 범위로 정규화해 반환.
        /// </summary>
        float CurrentDisplayAngleZ()
        {
            float z = body.localEulerAngles.z;
            if (z > 180f) z -= 360f;
            return z;
        }
    }
}
