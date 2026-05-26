using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 심벌 piece(crash/ride) root에 부착. DrumPiece.HitOccurred를 구독해
    /// targetTransform을 localTiltAxis 기준으로 maxTiltAngle * velocity 만큼
    /// 짧게 기울인 뒤(Tilting), rest 자세로 부드럽게 복귀(Returning)시킨다.
    /// 회전은 매 프레임 restRotation * AngleAxis(currentAngle, axis)로 절대값을
    /// 다시 적용하므로 hit 사이 누적이 발생하지 않는다.
    /// Returning 도중 새 hit이 들어오면 즉시 새 Tilting 사이클로 진입한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CymbalSpinFeedback : MonoBehaviour
    {
        enum Phase { Idle, Tilting, Returning }

        [SerializeField] DrumPiece drumPiece;
        [SerializeField] Transform targetTransform;
        [SerializeField] Vector3 localTiltAxis = Vector3.up;
        [SerializeField] float maxTiltAngle = 20f;
        [SerializeField, Min(0f)] float tiltDuration = 0.06f;
        [SerializeField] AnimationCurve tiltCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));
        [SerializeField, Min(0f)] float returnDuration = 0.4f;
        [SerializeField] AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        Quaternion m_RestLocalRotation;
        Phase m_Phase = Phase.Idle;
        float m_Elapsed;
        float m_TargetAngle;

        void Awake()
        {
            if (drumPiece == null)
                drumPiece = GetComponent<DrumPiece>();
            if (targetTransform == null)
                targetTransform = transform;
            if (targetTransform != null)
                m_RestLocalRotation = targetTransform.localRotation;
        }

        void OnEnable()
        {
            if (drumPiece != null)
                drumPiece.HitOccurred += OnHit;
        }

        void OnDisable()
        {
            if (drumPiece != null)
                drumPiece.HitOccurred -= OnHit;
            if (targetTransform != null)
                targetTransform.localRotation = m_RestLocalRotation;
            m_Phase = Phase.Idle;
        }

        void OnHit(float velocity)
        {
            m_TargetAngle = maxTiltAngle * Mathf.Clamp01(velocity);
            m_Elapsed = 0f;
            m_Phase = Phase.Tilting;
        }

        void Update()
        {
            if (targetTransform == null || m_Phase == Phase.Idle) return;
            if (localTiltAxis.sqrMagnitude <= 0f) return;

            switch (m_Phase)
            {
                case Phase.Tilting:
                    TickTilting();
                    break;
                case Phase.Returning:
                    TickReturning();
                    break;
            }
        }

        void TickTilting()
        {
            if (tiltDuration <= 0f)
            {
                ApplyAngle(m_TargetAngle);
                StartReturning();
                return;
            }

            m_Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(m_Elapsed / tiltDuration);
            float curveT = tiltCurve.Evaluate(t);
            ApplyAngle(m_TargetAngle * curveT);

            if (t >= 1f)
                StartReturning();
        }

        void StartReturning()
        {
            if (returnDuration <= 0f)
            {
                ApplyAngle(0f);
                m_Phase = Phase.Idle;
                return;
            }
            m_Elapsed = 0f;
            m_Phase = Phase.Returning;
        }

        void TickReturning()
        {
            m_Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(m_Elapsed / returnDuration);
            float curveT = returnCurve.Evaluate(t);
            ApplyAngle(m_TargetAngle * (1f - curveT));
            if (t >= 1f)
            {
                ApplyAngle(0f);
                m_Phase = Phase.Idle;
            }
        }

        void ApplyAngle(float angle)
        {
            targetTransform.localRotation = m_RestLocalRotation * Quaternion.AngleAxis(angle, localTiltAxis.normalized);
        }
    }
}
