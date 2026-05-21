using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    // VR 입력 없이 04 sub-spec(DSP pitch crossfade) 동작을 청각 검증하기 위한 디버그 도구.
    // Trombone 과 같은 GameObject 에 부착 → Editor Play 모드에서 triggerKey 누르면
    // NoteOn(1회) → 일정 간격으로 TrySetActiveVoicePitch 만 호출 → C major scale 재생 → NoteOff.
    [RequireComponent(typeof(Trombone))]
    [DisallowMultipleComponent]
    public class TromboneCScaleDebugger : MonoBehaviour
    {
        [SerializeField] Key triggerKey = Key.P;
        [SerializeField, Min(0.05f)] float noteDurationSec = 0.4f;
        [SerializeField] int rootMidiNote = 33; // Trombone.baseToneMidiNote 와 동일하게 두면 sample root 와 정합
        [SerializeField] int[] semitoneOffsets = new[] { 0, 2, 4, 5, 7, 9, 11, 12 };
        [SerializeField] bool force2D = true; // VR 입력 없이 testing 시 spatial attenuation 우회

        Trombone m_Trombone;
        InstrumentAudioOutput m_AudioOutput;
        bool m_IsPlaying;

        void Awake()
        {
            m_Trombone = GetComponent<Trombone>();
            m_AudioOutput = GetComponent<InstrumentAudioOutput>();
            if (m_AudioOutput == null) m_AudioOutput = GetComponentInChildren<InstrumentAudioOutput>(true);
        }

        void Update()
        {
            if (m_IsPlaying) return;
            if (Keyboard.current == null) return;
            if (Keyboard.current[triggerKey].wasPressedThisFrame)
                StartCoroutine(PlayScale());
        }

        IEnumerator PlayScale()
        {
            m_IsPlaying = true;
            var listener = FindAnyObjectByType<AudioListener>();
            float dist = listener != null ? Vector3.Distance(listener.transform.position, transform.position) : -1f;
            float prevInstVol = m_Trombone.InstanceVolume;
            m_Trombone.InstanceVolume = 1f; // force max — 영속 저장소에서 작게 로드됐을 수 있음
            Debug.Log($"[CScaleDbg] Start: dist={dist:F2}m, instanceVolume {prevInstVol:F2}→1.00 (force)");

            m_Trombone.TriggerMidi(new MidiEvent(rootMidiNote, 1f, MidiEventType.NoteOn));
            yield return null;
            if (force2D) Force2DOnVoices();
            yield return new WaitForSeconds(0.1f); // fade-in 50ms 완료 대기
            LogVoiceState($"after NoteOn+fadeIn note={rootMidiNote}");

            yield return new WaitForSeconds(noteDurationSec);

            for (int i = 1; i < semitoneOffsets.Length; i++)
            {
                float pitch = Mathf.Pow(2f, semitoneOffsets[i] / 12f);
                bool ok = m_AudioOutput != null && m_AudioOutput.TrySetActiveVoicePitch(rootMidiNote, pitch);
                Debug.Log($"[CScaleDbg] step[{i}] offset={semitoneOffsets[i]} pitch={pitch:F3} trySet={ok}");
                yield return new WaitForSeconds(noteDurationSec);
            }

            m_Trombone.TriggerMidi(new MidiEvent(rootMidiNote, 0f, MidiEventType.NoteOff));
            Debug.Log("[CScaleDbg] NoteOff fired");
            m_IsPlaying = false;
        }

        void LogVoiceState(string label)
        {
            if (m_AudioOutput == null) { Debug.LogWarning($"[CScaleDbg] {label}: audioOutput null"); return; }
            var sources = m_AudioOutput.GetComponentsInChildren<AudioSource>(true);
            int playing = 0; float maxVol = 0f; float spatial = -1f; float maxDist = -1f;
            foreach (var s in sources)
            {
                if (s.isPlaying)
                {
                    playing++;
                    if (s.volume > maxVol) maxVol = s.volume;
                    spatial = s.spatialBlend;
                    maxDist = s.maxDistance;
                }
            }
            Debug.Log($"[CScaleDbg] {label}: voices={sources.Length} playing={playing} maxVol={maxVol:F2} spatialBlend={spatial:F2} maxDist={maxDist:F1}");
        }

        void Force2DOnVoices()
        {
            if (m_AudioOutput == null) return;
            var sources = m_AudioOutput.GetComponentsInChildren<AudioSource>(true);
            int n = 0;
            foreach (var s in sources)
            {
                s.spatialBlend = 0f;
                s.spatialize = false;       // OpenXR Spatializer 우회
                s.spatializePostEffects = false;
                s.bypassEffects = true;     // mixer effect 우회
                s.bypassListenerEffects = true;
                s.bypassReverbZones = true;
                s.outputAudioMixerGroup = null; // mixer 라우팅 우회 → 직접 listener로
                n++;
            }
            Debug.Log($"[CScaleDbg] Force2D applied to {n} sources (spatialize=false, mixer=bypassed)");
        }
    }
}
