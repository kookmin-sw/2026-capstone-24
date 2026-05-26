using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab root에 부착. InstrumentBase 계약 통과 (NoteOn/NoteOff + MidiTriggered).
    /// 왼손 Grip rising/falling edge → 현재 partial/slide 로 계산한 **effective MIDI** 를 NoteOn(sustain loop + fadeIn) /
    /// NoteOff(fadeOut) 의 MidiEvent.Note 에 직접 박아 발행한다 (멀티플레이 수신측이 자기 로컬 partial/slide 로
    /// 재계산하면 음정이 어긋나므로 wire payload 자체에 effective 값을 동봉).
    /// 4개 sample을 음역대 분할 멀티샘플로 두고, NoteOn 시점 effective MIDI에 가장 가까운 root sample을 선택해
    /// pitch shift 폭을 최소화한다. Partial/Slide 인덱스 변경 시에는 Choke(이전 effective) + NoteOn(새 effective)으로
    /// 재트리거해 sample 재선택과 발음 페어링을 보장. m_LastEffectiveMidi 로 활성 발음의 effective 값을 박제해
    /// NoteOff/Choke 가 항상 같은 키로 매칭되도록 한다.
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
        [SerializeField] HapticImpulsePlayer leftHapticImpulsePlayer;
        [SerializeField] HapticImpulsePlayer rightHapticImpulsePlayer;
        [SerializeField, Range(0f, 1f)] float hapticAmplitude = 0.15f;
        [SerializeField, Min(0.01f)] float hapticPulseDuration = 0.1f;
        [SerializeField, Min(0.01f)] float hapticPulseInterval = 0.08f;

        bool m_IsBlowing;
        int m_LastPartialIndex;
        int m_LastSlideIndex;
        int m_LastEffectiveMidi;
        TromboneSample m_SelectedSample;
        float m_NextHapticPulseTime;

        public int CurrentMidiNote => Mathf.RoundToInt(ComputeEffectiveMidi());
        public bool IsBlowing => m_IsBlowing;

        protected override void OnEnable()
        {
            base.OnEnable();
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
                    TriggerMidi(new MidiEvent(m_LastEffectiveMidi, 0f, MidiEventType.Choke));
                    m_IsBlowing = false;
                }
                return;
            }

            var action = leftGripAction != null ? leftGripAction.action : null;
            bool grip = action != null && action.ReadValue<float>() >= gripThreshold;

            if (grip && !m_IsBlowing)
            {
                int newEffective = Mathf.RoundToInt(ComputeEffectiveMidi());
                TriggerMidi(new MidiEvent(newEffective, 1f, MidiEventType.NoteOn));
                m_LastEffectiveMidi = newEffective;
                m_IsBlowing = true;
                m_LastPartialIndex = partialController != null ? partialController.PartialIndex : 0;
                m_LastSlideIndex = slideController != null ? slideController.SlideIndex : 0;
                m_NextHapticPulseTime = 0f;
            }
            else if (!grip && m_IsBlowing)
            {
                TriggerMidi(new MidiEvent(m_LastEffectiveMidi, 0f, MidiEventType.NoteOff));
                m_IsBlowing = false;
            }

            // Partial 또는 Slide 인덱스 변경 시 1회만 retrigger (둘이 동시에 바뀌어도 NoteOn 한 번).
            bool partialChanged = partialController != null && partialController.PartialIndex != m_LastPartialIndex;
            bool slideChanged = slideController != null && slideController.SlideIndex != m_LastSlideIndex;
            if (m_IsBlowing && (partialChanged || slideChanged))
            {
                TriggerMidi(new MidiEvent(m_LastEffectiveMidi, 0f, MidiEventType.Choke));
                int newEffective = Mathf.RoundToInt(ComputeEffectiveMidi());
                TriggerMidi(new MidiEvent(newEffective, 1f, MidiEventType.NoteOn));
                m_LastEffectiveMidi = newEffective;
                if (partialController != null) m_LastPartialIndex = partialController.PartialIndex;
                if (slideController != null) m_LastSlideIndex = slideController.SlideIndex;
            }

            if (m_IsBlowing && Time.unscaledTime >= m_NextHapticPulseTime)
            {
                if (leftHapticImpulsePlayer != null)
                    leftHapticImpulsePlayer.SendHapticImpulse(hapticAmplitude, hapticPulseDuration);
                if (rightHapticImpulsePlayer != null)
                    rightHapticImpulsePlayer.SendHapticImpulse(hapticAmplitude, hapticPulseDuration);
                m_NextHapticPulseTime = Time.unscaledTime + hapticPulseInterval;
            }
        }

        protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)
        {
            playback = default;
            // sender 가 LateUpdate 에서 effective MIDI 를 MidiEvent.Note 에 직접 박아 보내므로
            // 로컬·원격 모두 midiEvent.Note 를 그대로 effective MIDI 로 신뢰한다. 수신측에서
            // 자기 로컬 partial/slide 상태로 재계산하면 음정이 어긋나기 때문에 ComputeEffectiveMidi 사용 금지.
            int effectiveMidi = midiEvent.Note;
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

        int ComputeSlideSemitones()
            => slideController != null ? -slideController.SlideIndex : 0;

        float ComputeEffectiveMidi()
            => baseToneMidiNote
             + (partialController != null ? partialController.PartialOffsetSemitones : 0)
             + ComputeSlideSemitones();

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
