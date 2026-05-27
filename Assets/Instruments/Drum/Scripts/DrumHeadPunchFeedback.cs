using System.Collections;
using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 드럼 piece(스네어/탐/킥) root에 부착. DrumPiece.HitOccurred를 구독해
    /// targetTransform의 localPosition을 hit 방향으로 짧게 눌렀다가 복귀시킨다.
    /// 연속 hit은 진행 중 코루틴을 중단하고 새 depth로 재시작한다(누적 X).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DrumHeadPunchFeedback : MonoBehaviour
    {
        [SerializeField] DrumPiece drumPiece;
        [SerializeField] Transform targetTransform;
        [SerializeField] Vector3 localPunchAxis = new Vector3(0f, -1f, 0f);
        [SerializeField, Min(0f)] float maxDepth = 0.01f;
        [SerializeField, Min(0f)] float duration = 0.08f;
        [SerializeField] AnimationCurve punchCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0f));

        Vector3 m_RestLocalPos;
        Coroutine m_Routine;

        void Awake()
        {
            if (drumPiece == null)
                drumPiece = GetComponent<DrumPiece>();
            if (targetTransform == null)
                targetTransform = transform;
            if (targetTransform != null)
                m_RestLocalPos = targetTransform.localPosition;
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
            if (m_Routine != null)
            {
                StopCoroutine(m_Routine);
                m_Routine = null;
            }
            if (targetTransform != null)
                targetTransform.localPosition = m_RestLocalPos;
        }

        void OnHit(float velocity)
        {
            if (targetTransform == null) return;
            if (duration <= 0f || maxDepth <= 0f) return;

            float depth = maxDepth * Mathf.Clamp01(velocity);
            if (m_Routine != null)
                StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(PunchRoutine(depth));
        }

        IEnumerator PunchRoutine(float depth)
        {
            Vector3 axis = localPunchAxis.sqrMagnitude > 0f
                ? localPunchAxis.normalized
                : Vector3.down;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveValue = punchCurve.Evaluate(t);
                targetTransform.localPosition = m_RestLocalPos + axis * (depth * curveValue);
                yield return null;
            }
            targetTransform.localPosition = m_RestLocalPos;
            m_Routine = null;
        }
    }
}
