using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// Rig(tromboneRoot) 자체를 회전시켜 magnetic snap을 구현.
    /// Body/Slide 개별 조작 대신 Rig 전체를 돌려 모든 자식이 함께 이동.
    /// TromboneAnchor(10005)가 Rig를 입력 각도로 정렬한 뒤, 이 컴포넌트(10006)가
    /// Rig.localEulerAngles.z를 snap 각도로 교체하고 MouthPiece 위치를 보정한다.
    /// </summary>
    [DefaultExecutionOrder(10006)]
    [DisallowMultipleComponent]
    public sealed class TromboneBodyVisualSnap : MonoBehaviour
    {
        [SerializeField] TromboneAnchor tromboneAnchor;
        [SerializeField] TrombonePartialController partialController;
        [SerializeField] Transform mouthPieceOnTrombone;
        [SerializeField, Min(0f)] float easeDurationSeconds = 0.1f;
        [SerializeField, Range(1f, 10f)] float easeExponent = 4f;

        float m_StartAngleZ;
        float m_TargetAngleZ;
        float m_ElapsedSeconds;
        int m_LastPartialIndex = -1;
        bool m_IsBoosting;
        float m_CurrentDisplayedZ;

        void OnEnable()
        {
            m_LastPartialIndex = -1;
            m_IsBoosting = false;
            m_CurrentDisplayedZ = 0f;
        }

        void LateUpdate()
        {
            if (tromboneAnchor == null || partialController == null)
                return;

            if (!tromboneAnchor.IsAttached)
            {
                m_LastPartialIndex = -1;
                m_IsBoosting = false;
                return;
            }

            if (m_LastPartialIndex == -1)
                m_CurrentDisplayedZ = partialController.UpTiltDegrees;

            int idx = partialController.PartialIndex;
            float effectiveTargetZ = (idx - partialController.CenterPartialIndex) * partialController.AnglePerPartial;

            bool targetChanged = idx != m_LastPartialIndex ||
                                 Mathf.Abs(Mathf.DeltaAngle(effectiveTargetZ, m_TargetAngleZ)) > 0.01f;
            if (targetChanged)
            {
                m_StartAngleZ = m_CurrentDisplayedZ;
                m_TargetAngleZ = effectiveTargetZ;
                m_ElapsedSeconds = 0f;
                m_IsBoosting = true;
                m_LastPartialIndex = idx;
            }

            if (m_IsBoosting)
            {
                m_ElapsedSeconds += Time.deltaTime;
                float t = (easeDurationSeconds > 0f) ? Mathf.Clamp01(m_ElapsedSeconds / easeDurationSeconds) : 1f;
                float easedT = (easeExponent > 0f) ? 1f - Mathf.Exp(-easeExponent * t) : t;
                if (t >= 1f) { easedT = 1f; m_IsBoosting = false; }
                ApplyRigTilt(Mathf.LerpAngle(m_StartAngleZ, m_TargetAngleZ, easedT));
            }
            else
            {
                ApplyRigTilt(m_TargetAngleZ);
            }
        }

        /// <summary>
        /// Rig.localEulerAngles.z를 z(UpTiltDegrees 공간)에 대응하는 값으로 교체.
        /// MouthPiece의 world 위치가 유지되도록 Rig position도 보정한다.
        /// </summary>
        void ApplyRigTilt(float z)
        {
            m_CurrentDisplayedZ = z;

            Transform rig = partialController.TromboneRoot;
            if (rig == null) return;

            float sign = partialController.AngleSignMultiplier;
            float targetRigZ = (sign != 0f) ? z / sign : z;

            // Rig.localEulerAngles.z만 snap 각도로 교체해 재구성
            Vector3 rigEuler = rig.localEulerAngles;
            rigEuler.z = targetRigZ;
            Quaternion snapLocalRot = Quaternion.Euler(rigEuler);

            Quaternion parentRot = rig.parent != null ? rig.parent.rotation : Quaternion.identity;
            Quaternion snapWorldRot = parentRot * snapLocalRot;

            // MouthPiece world 위치 보존 (입으로부터 트롬본이 이탈하지 않도록)
            Vector3 mouthLocalPos = mouthPieceOnTrombone != null
                ? mouthPieceOnTrombone.localPosition
                : Vector3.zero;
            Vector3 mouthWorldPos = rig.position + rig.rotation * mouthLocalPos;
            Vector3 newRigPos = mouthWorldPos - snapWorldRot * mouthLocalPos;

            rig.SetPositionAndRotation(newRigPos, snapWorldRot);
        }
    }
}
