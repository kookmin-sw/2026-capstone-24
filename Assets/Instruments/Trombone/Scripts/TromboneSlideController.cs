using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab의 Slide GameObject에 부착.
    /// 오른손 Grip 홀드 동안 trombone-local 손 x 변화량을 raw slide.localPosition.x 에 누적하되,
    /// hysteresis 임계점을 통과할 때만 SlidePositionCount 개의 고정 포지션 중 다음/이전 인덱스로 텔레포트하고
    /// baseline 을 재보정해 시각적 떨림 및 표류를 차단한다.
    /// </summary>
    [DefaultExecutionOrder(10005)]
    [DisallowMultipleComponent]
    public sealed class TromboneSlideController : MonoBehaviour
    {
        public const int SlidePositionCount = 7;

        [SerializeField] TromboneAnchor tromboneAnchor;
        [SerializeField] Transform tromboneRoot;
        [SerializeField] Transform slide;
        [SerializeField] Transform rightGhostWristSource;
        [SerializeField] InputActionReference rightGripAction;

        [SerializeField] float slideMinX = -0.5895388f;
        [SerializeField] float slideMaxX = -1.165f;
        [SerializeField] float gripThreshold = 0.5f;
        [SerializeField, Range(0f, 0.5f)] float hysteresisFraction = 0.15f;

        bool m_IsGripHeld;
        float m_BaselineHandX;
        float m_BaselineSlideX;
        int m_SlideIndex;

        public float NormalizedSlide { get; private set; }
        public int SlideIndex => m_SlideIndex;
        public float SlideMinX => slideMinX;
        public float SlideMaxX => slideMaxX;
        public event Action<int> SlideIndexChanged;

        void Reset()
        {
            slide = transform;
        }

        void OnEnable()
        {
            rightGripAction?.action?.Enable();
        }

        void OnDisable()
        {
            rightGripAction?.action?.Disable();
        }

        void LateUpdate()
        {
            if (tromboneAnchor == null || !tromboneAnchor.IsAttached)
            {
                m_IsGripHeld = false;
                ApplyQuantizedPosition();
                UpdateNormalizedSlide();
                return;
            }

            var action = rightGripAction != null ? rightGripAction.action : null;
            bool grip = action != null && action.ReadValue<float>() >= gripThreshold;

            if (grip && !m_IsGripHeld)
            {
                m_BaselineHandX = ComputeHandLocalX();
                m_BaselineSlideX = slide.localPosition.x;
                m_IsGripHeld = true;
            }
            else if (!grip && m_IsGripHeld)
            {
                m_IsGripHeld = false;
            }

            if (m_IsGripHeld)
                ApplySlide();

            QuantizeSlide();
            UpdateNormalizedSlide();
        }

        void UpdateNormalizedSlide()
        {
            if (slide == null)
            {
                NormalizedSlide = 0f;
                return;
            }
            NormalizedSlide = Mathf.InverseLerp(slideMinX, slideMaxX, slide.localPosition.x);
        }

        float ComputeHandLocalX()
        {
            if (tromboneRoot == null || rightGhostWristSource == null)
                return 0f;
            return tromboneRoot.InverseTransformPoint(rightGhostWristSource.position).x;
        }

        void ApplySlide()
        {
            if (slide == null)
                return;
            var current = ComputeHandLocalX();
            var lo = Mathf.Min(slideMinX, slideMaxX);
            var hi = Mathf.Max(slideMinX, slideMaxX);
            var newX = Mathf.Clamp(m_BaselineSlideX + (current - m_BaselineHandX), lo, hi);
            var p = slide.localPosition;
            p.x = newX;
            slide.localPosition = p;
        }

        void QuantizeSlide()
        {
            if (slide == null) return;

            // 부호 무관 인덱스 좌표(0~6 float)로 환산해 hysteresis 비교를 단순화.
            float rawX = slide.localPosition.x;
            float fractional = (rawX - slideMinX) / (slideMaxX - slideMinX) * (SlidePositionCount - 1);

            int idx = m_SlideIndex;
            while (idx + 1 < SlidePositionCount && fractional > idx + 0.5f + hysteresisFraction)
                idx++;
            while (idx > 0 && fractional < idx - 0.5f - hysteresisFraction)
                idx--;

            bool changed = idx != m_SlideIndex;
            m_SlideIndex = idx;
            ApplyQuantizedPosition();

            if (changed)
            {
                // 인덱스 변경 직후 baseline 을 새 위치로 재보정 → 다음 프레임 raw 누적이 새 인덱스 기준 0에서 시작.
                if (m_IsGripHeld)
                {
                    m_BaselineSlideX = slide.localPosition.x;
                    m_BaselineHandX = ComputeHandLocalX();
                }
                SlideIndexChanged?.Invoke(m_SlideIndex);
            }
        }

        void ApplyQuantizedPosition()
        {
            if (slide == null) return;
            var p = slide.localPosition;
            p.x = PositionX(m_SlideIndex);
            slide.localPosition = p;
        }

        float PositionX(int index)
            => Mathf.Lerp(slideMinX, slideMaxX, (float)index / (SlidePositionCount - 1));
    }
}
