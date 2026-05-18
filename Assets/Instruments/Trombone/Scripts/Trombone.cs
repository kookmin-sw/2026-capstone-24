using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab root에 부착. InstrumentBase 계약 통과 (NoteOn/NoteOff + MidiTriggered).
    /// 왼손 Grip rising/falling edge → baseToneMidiNote 기반 NoteOn(sustain loop + fadeIn) / NoteOff(fadeOut).
    /// 4개 sample을 음역대 분할 멀티샘플로 두고, NoteOn 시점 effective MIDI에 가장 가까운 root sample을 선택해
    /// pitch shift 폭을 최소화한다. 발음 중에는 sample을 고정하고 slide pitch만 AudioSource.pitch에 직접 갱신.
    /// TromboneAnchor.IsAttached == false면 진행 중 발음을 Choke로 즉시 silence (fade 건너뜀).
    /// </summary>
    [DefaultExecutionOrder(10006)]
    [DisallowMultipleComponent]
    public sealed class Trombone : InstrumentBase
    {
        [Serializable]
        public struct TromboneSample
        {
            public AudioClip clip;
            public int rootMidiNote;
        }

        [SerializeField] TromboneAnchor tromboneAnchor;
        [SerializeField] TromboneSlideController slideController;
        [SerializeField] TrombonePartialController partialController;
        [SerializeField] TromboneSample[] samples = Array.Empty<TromboneSample>();
        [SerializeField] int baseToneMidiNote = 33; // A1
        [SerializeField, Min(0f)] float fadeInDuration = 0.05f;
        [SerializeField, Min(0f)] float fadeOutDuration = 0.15f;
        [SerializeField] InputActionReference leftGripAction;
        [SerializeField] float gripThreshold = 0.5f;

        bool m_IsBlowing;
        int m_LastPartialIndex;
        TromboneSample m_SelectedSample;

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
                m_LastPartialIndex = partialController != null ? partialController.PartialIndex : 0;
            }
            else if (!grip && m_IsBlowing)
            {
                TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, MidiEventType.NoteOff));
                m_IsBlowing = false;
            }

            if (m_IsBlowing && partialController != null
                && partialController.PartialIndex != m_LastPartialIndex)
            {
                TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, MidiEventType.Choke));
                TriggerMidi(new MidiEvent(baseToneMidiNote, 1f, MidiEventType.NoteOn));
                m_LastPartialIndex = partialController.PartialIndex;
            }

            if (m_IsBlowing && audioOutput != null && m_SelectedSample.clip != null)
                audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, ComputePitchForSelectedSample());
        }

        protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)
        {
            playback = default;
            float effectiveMidi = ComputeEffectiveMidi();
            if (!TrySelectSampleForMidi(effectiveMidi, out m_SelectedSample))
                return false;
            float pitch = ComputePitchForSelectedSample(effectiveMidi);
            playback = new NotePlayback(m_SelectedSample.clip, pitch, midiEvent.Velocity,
                                        sustain: true,
                                        fadeInDuration: fadeInDuration,
                                        fadeOutDuration: fadeOutDuration);
            return true;
        }

        protected override void OnChoke(MidiEvent midiEvent)
        {
            if (audioOutput != null)
                audioOutput.StopNoteImmediate(midiEvent.Note);
        }

        float ComputeSlideSemitones()
        {
            if (slideController == null) return 0f;
            float t = slideController.NormalizedSlide;
            return Mathf.Lerp(0f, -6f, t);
        }

        float ComputeEffectiveMidi()
            => baseToneMidiNote
             + (partialController != null ? partialController.PartialOffsetSemitones : 0)
             + ComputeSlideSemitones();

        float ComputePitchForSelectedSample() => ComputePitchForSelectedSample(ComputeEffectiveMidi());

        float ComputePitchForSelectedSample(float effectiveMidi)
        {
            float diff = effectiveMidi - m_SelectedSample.rootMidiNote;
            return Mathf.Pow(2f, diff / 12f);
        }

        bool TrySelectSampleForMidi(float effectiveMidi, out TromboneSample selected)
        {
            selected = default;
            if (samples == null || samples.Length == 0) return false;

            int bestIdx = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i].clip == null) continue;
                float distance = Mathf.Abs(samples[i].rootMidiNote - effectiveMidi);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIdx = i;
                }
            }

            if (bestIdx < 0) return false;
            selected = samples[bestIdx];
            return true;
        }
    }
}
