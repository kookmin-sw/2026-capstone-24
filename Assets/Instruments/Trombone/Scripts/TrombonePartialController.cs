using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab에 부착. tromboneRoot.localEulerAngles.z 를 LateUpdate 마다 읽어
    /// partialOffsetsSemitones 배열의 인덱스를 균등 anglePerPartial° 간격 + 히스테리시스로 선택한다.
    /// Trombone.cs 가 PartialOffsetSemitones / PartialIndex 를 읽어 effective MIDI 및 자동 재트리거에 사용.
    /// TromboneAnchor.IsAttached == false 인 동안은 initialPartialIndex 로 리셋.
    /// </summary>
    [DefaultExecutionOrder(10005)]
    [DisallowMultipleComponent]
    public sealed class TrombonePartialController : MonoBehaviour
    {
        [SerializeField] TromboneAnchor tromboneAnchor;
        [SerializeField] Transform tromboneRoot;

        [SerializeField] int[] partialOffsetsSemitones = { 12, 19, 24, 28, 31 };
        [SerializeField, Min(0.01f)] float anglePerPartial = 15f;
        [SerializeField] float angleSignMultiplier = 1f;
        [SerializeField, Min(0f)] float hysteresisAngle = 2f;
        [SerializeField] int initialPartialIndex = 0;
        [SerializeField] int centerPartialIndex = 2;

        int m_PartialIndex;

        public int PartialIndex => m_PartialIndex;
        public float AnglePerPartial => anglePerPartial;
        public int PartialCount => (partialOffsetsSemitones != null) ? partialOffsetsSemitones.Length : 0;

        public int PartialOffsetSemitones
        {
            get
            {
                int n = (partialOffsetsSemitones != null) ? partialOffsetsSemitones.Length : 0;
                if (n == 0) return 0;
                return partialOffsetsSemitones[Mathf.Clamp(m_PartialIndex, 0, n - 1)];
            }
        }

        void OnEnable()
        {
            m_PartialIndex = ClampInitial(initialPartialIndex);
        }

        void LateUpdate()
        {
            if (tromboneAnchor == null || !tromboneAnchor.IsAttached || tromboneRoot == null)
            {
                m_PartialIndex = ClampInitial(initialPartialIndex);
                return;
            }

            int n = (partialOffsetsSemitones != null) ? partialOffsetsSemitones.Length : 0;
            if (n == 0) return;

            float upTilt = angleSignMultiplier * NormalizeSignedAngle(tromboneRoot.localEulerAngles.z);

            int idx = m_PartialIndex;
            while (idx + 1 < n && upTilt >= ThresholdFor(idx + 1) + hysteresisAngle)
                idx++;
            while (idx > 0 && upTilt < ThresholdFor(idx) - hysteresisAngle)
                idx--;

            m_PartialIndex = idx;
        }

        float ThresholdFor(int i) => (i - centerPartialIndex - 0.5f) * anglePerPartial;

        int ClampInitial(int idx)
        {
            int n = (partialOffsetsSemitones != null) ? partialOffsetsSemitones.Length : 0;
            if (n == 0) return 0;
            return Mathf.Clamp(idx, 0, n - 1);
        }

        static float NormalizeSignedAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            else if (a < -180f) a += 360f;
            return a;
        }
    }
}
