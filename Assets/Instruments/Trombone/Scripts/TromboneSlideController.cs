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

        [SerializeField] float slideMinX = -0.7f;
        [SerializeField] float slideMaxX = -0.3f;
        [SerializeField] float gripThreshold = 0.5f;

        bool m_IsGripHeld;
        float m_BaselineHandX;
        float m_BaselineSlideX;

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
            var newX = Mathf.Clamp(m_BaselineSlideX + (current - m_BaselineHandX), slideMinX, slideMaxX);
            var p = slide.localPosition;
            p.x = newX;
            slide.localPosition = p;
        }
    }
}
