using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab root에 부착. InstrumentBase 계약 통과 (NoteOn/NoteOff + MidiTriggered).
    /// 왼손 Grip rising/falling edge → C4 NoteOn(sustain loop chain) / NoteOff(release one-shot).
    /// Grip 홀드 동안 매 LateUpdate slideController.NormalizedSlide → semitones=Lerp(0,-6,t)
    /// → pitch=2^(s/12) 를 sustain voice의 AudioSource.pitch에 직접 적용. release voice는 NoteOff 시점 pitch 고정.
    /// TromboneAnchor.IsAttached == false 면 새 NoteOn 무시 + 진행 중 발음 Choke (release 건너뜀, 즉시 silence).
    /// </summary>
    [DefaultExecutionOrder(10006)]
    [DisallowMultipleComponent]
    public sealed class Trombone : InstrumentBase
    {
        [SerializeField] TromboneAnchor tromboneAnchor;
        [SerializeField] TromboneSlideController slideController;
        [SerializeField] AudioClip sustainClip;
        [SerializeField] AudioClip releaseClip;
        [SerializeField] int baseToneMidiNote = 60;
        [SerializeField] InputActionReference leftGripAction;

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
                    // anchor 이탈은 "악기에서 멀어짐"이므로 release 건너뛰고 즉시 silence (Choke 경로)
                    TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, MidiEventType.Choke));
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
            if (slideController == null) return 1f;
            float t = slideController.NormalizedSlide;
            float semitones = Mathf.Lerp(0f, -6f, t);
            return Mathf.Pow(2f, semitones / 12f);
        }

        protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)
        {
            playback = default;
            if (sustainClip == null || releaseClip == null) return false;
            float pitch = ComputePitchFromSlide();
            playback = new NotePlayback(sustainClip, pitch, midiEvent.Velocity, sustain: true, releaseClip: releaseClip);
            return true;
        }
    }
}
