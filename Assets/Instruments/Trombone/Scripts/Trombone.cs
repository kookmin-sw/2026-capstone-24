using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab root에 부착. InstrumentBase 계약 통과 (NoteOn/NoteOff + MidiTriggered).
    /// 왼손 Grip rising/falling edge → C4 NoteOn/NoteOff.
    /// Grip 홀드 동안 매 LateUpdate Slide.localPosition.x → semitones=Lerp(0,-6,t) → pitch=2^(s/12)
    /// 를 활성 voice의 AudioSource.pitch에 직접 적용 (ARD 02 결정).
    /// TromboneAnchor.IsAttached == false 면 새 NoteOn 무시 + 진행 중 발음 강제 NoteOff.
    /// </summary>
    [DefaultExecutionOrder(10006)]
    [DisallowMultipleComponent]
    public sealed class Trombone : InstrumentBase
    {
        [SerializeField] TromboneAnchor tromboneAnchor;
        [SerializeField] Transform slide;
        [SerializeField] AudioClip baseToneClip;
        [SerializeField] int baseToneMidiNote = 60;
        [SerializeField] InputActionReference leftGripAction;

        [SerializeField] float slideMinX = -0.7f;
        [SerializeField] float slideMaxX = -0.3f;
        [SerializeField] float gripThreshold = 0.5f;

        bool m_IsBlowing;

        void OnEnable()
        {
            leftGripAction?.action?.Enable();
        }

        protected override void OnDisable()
        {
            leftGripAction?.action?.Disable();
            base.OnDisable();
            m_IsBlowing = false;
        }

        void LateUpdate()
        {
            if (tromboneAnchor == null || !tromboneAnchor.IsAttached)
            {
                if (m_IsBlowing)
                {
                    TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, MidiEventType.NoteOff));
                    m_IsBlowing = false;
                }
                return;
            }

            var action = leftGripAction != null ? leftGripAction.action : null;
            bool grip = action != null && action.ReadValue<float>() >= gripThreshold;

            if (grip && !m_IsBlowing)
            {
                TriggerMidi(new MidiEvent(baseToneMidiNote, 1f, MidiEventType.NoteOn));
                m_IsBlowing = true;
            }
            else if (!grip && m_IsBlowing)
            {
                TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, MidiEventType.NoteOff));
                m_IsBlowing = false;
            }

            if (m_IsBlowing && audioOutput != null)
                audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, ComputePitchFromSlide());
        }

        float ComputePitchFromSlide()
        {
            if (slide == null) return 1f;
            float t = Mathf.InverseLerp(slideMinX, slideMaxX, slide.localPosition.x);
            float semitones = Mathf.Lerp(0f, -6f, t);
            return Mathf.Pow(2f, semitones / 12f);
        }

        protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)
        {
            playback = default;
            if (baseToneClip == null) return false;
            float pitch = ComputePitchFromSlide();
            playback = new NotePlayback(baseToneClip, pitch, midiEvent.Velocity);
            return true;
        }
    }
}
