using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab root에 부착. InstrumentBase 계약 통과 (NoteOn/NoteOff + MidiTriggered).
    /// 왼손 Grip rising/falling edge → baseToneMidiNote 기반 NoteOn(sustain loop + fadeIn) / NoteOff(fadeOut).
    /// 4개 sample을 음역대 분할 멀티샘플로 두고, NoteOn 시점 effective MIDI에 가장 가까운 root sample을 선택해
    /// pitch shift 폭을 최소화한다. Partial/Slide 인덱스 변경 시에는 Choke + NoteOn 으로 재트리거해 sample 재선택을 보장.
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
        int m_LastSlideIndex;
        TromboneSample m_SelectedSample;

        public int CurrentMidiNote => Mathf.RoundToInt(ComputeEffectiveMidi());
        public bool IsBlowing => m_IsBlowing;

        protected override void Awake()
        {
            // base.Awake() 안에서 Initialize() → EnsureVoicePool() → VoiceGameObjectCreated 이벤트 발화.
            // 구독을 base 호출 전에 해야 모든 voice GameObject의 이벤트를 수신할 수 있다.
            if (audioOutput == null)
                audioOutput = GetComponentInChildren<InstrumentAudioOutput>(true);
            if (audioOutput != null)
                audioOutput.VoiceGameObjectCreated += OnVoiceGameObjectCreated;
            base.Awake();
        }

        void OnDestroy()
        {
            if (audioOutput != null)
                audioOutput.VoiceGameObjectCreated -= OnVoiceGameObjectCreated;
        }

        void OnVoiceGameObjectCreated(GameObject voiceGo)
        {
            if (voiceGo.GetComponent<TrombonePitchDsp>() == null)
                voiceGo.AddComponent<TrombonePitchDsp>();
        }

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
                m_LastSlideIndex = slideController != null ? slideController.SlideIndex : 0;
            }
            else if (!grip && m_IsBlowing)
            {
                if (audioOutput != null)
                    audioOutput.RequestGripReleaseFadeOut(baseToneMidiNote);
                m_IsBlowing = false;
            }

            // Partial 또는 Slide 인덱스 변경 시 끊김 없이 pitch만 갱신 (retrigger 제거).
            bool partialChanged = partialController != null && partialController.PartialIndex != m_LastPartialIndex;
            bool slideChanged = slideController != null && slideController.SlideIndex != m_LastSlideIndex;
            if (m_IsBlowing && (partialChanged || slideChanged))
            {
                float newEffectiveMidi = ComputeEffectiveMidi();
                float newPitch = ComputePitchForSelectedSample(newEffectiveMidi);
                if (audioOutput != null)
                    audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, newPitch);
                if (partialController != null) m_LastPartialIndex = partialController.PartialIndex;
                if (slideController != null) m_LastSlideIndex = slideController.SlideIndex;
            }
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
