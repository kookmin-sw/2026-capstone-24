using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab의 Slide GameObject에 부착.
    /// 오른손 Grip 홀드 동안만 trombone-local 손 x 변화량을 Slide.localPosition.x에 baseline+delta로 더하고 clamp.
    /// TromboneAnchor.IsAttached == true 인 동안에만 동작 (01 plan §Handoff).
    /// </summary>
    [DefaultExecutionOrder(10005)]
    [DisallowMultipleComponent]
    public sealed class TromboneSlideController : MonoBehaviour
    {
        [SerializeField] TromboneAnchor tromboneAnchor;
        [SerializeField] Transform tromboneRoot;
        [SerializeField] Transform slide;
        [SerializeField] Transform rightGhostWristSource;
        [SerializeField] InputActionReference rightGripAction;

        [SerializeField] float slideMinX = -0.5895388f;
        [SerializeField] float slideMaxX = -1.165f;
        [SerializeField] float gripThreshold = 0.5f;

        bool m_IsGripHeld;
        float m_BaselineHandX;
        float m_BaselineSlideX;

        public float NormalizedSlide { get; private set; }

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
    }
}
