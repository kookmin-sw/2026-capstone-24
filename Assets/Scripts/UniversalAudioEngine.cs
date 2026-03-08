using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace VRMusicStudio.Audio
{
    // MIDI 데이터 구조체
    public struct MidiData
    {
        public int instrumentId;
        public int channel;
        public int note;
        public float velocity;
        public bool isOn;
    }

    // 1. 유니티가 파일을 인식할 수 있도록 파일 이름과 동일한 메인 클래스를 상단에 배치합니다.
    public class UniversalAudioEngine : MonoBehaviour
    {
        public AudioMixerGroup defaultMixerGroup;
        private Dictionary<int, InstrumentEngine> _activeEngines = new Dictionary<int, InstrumentEngine>();

        // 외부 호출 진입점
        public void OnReceiveMidi(MidiData midi)
        {
            if (!_activeEngines.ContainsKey(midi.instrumentId))
            {
                CreateEngine(midi.instrumentId);
            }
            _activeEngines[midi.instrumentId].ProcessMidi(midi);
        }

        private void CreateEngine(int id)
        {
            InstrumentEngine engine;
            if (id >= 113)
                engine = gameObject.AddComponent<PercussionInstrument>();
            else
                engine = gameObject.AddComponent<MelodicInstrument>();

            engine.instrumentName = GetInstrumentNameFromId(id);
            engine.mixerGroup = defaultMixerGroup;
            engine.Initialize(32);
            _activeEngines[id] = engine;
        }

        private string GetInstrumentNameFromId(int id) => id switch
        {
            0 => "Piano",
            113 => "Drums",
            _ => "Default"
        };
    }

    // --- 아래는 보조 추상 클래스 및 구현 클래스들입니다 ---

    public abstract class InstrumentEngine : MonoBehaviour
    {
        public string instrumentName;
        public AudioMixerGroup mixerGroup;
        protected Dictionary<string, AudioClip> sampleClips = new Dictionary<string, AudioClip>();
        protected List<AudioSource> voicePool = new List<AudioSource>();
        protected Dictionary<int, AudioSource> activeVoices = new Dictionary<int, AudioSource>();

        public virtual void Initialize(int maxVoices)
        {
            LoadSamples();
            CreateVoicePool(maxVoices);
        }

        protected abstract void LoadSamples();
        public abstract void ProcessMidi(MidiData midi);

        protected void CreateVoicePool(int maxVoices)
        {
            GameObject container = new GameObject($"{instrumentName}_VoicePool");
            container.transform.SetParent(this.transform);
            for (int i = 0; i < maxVoices; i++)
            {
                AudioSource source = container.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1.0f;
                source.outputAudioMixerGroup = mixerGroup;
                voicePool.Add(source);
            }
        }

        protected AudioSource GetFreeVoice()
        {
            foreach (var v in voicePool) if (!v.isPlaying) return v;
            return voicePool[0];
        }
    }

    public class MelodicInstrument : InstrumentEngine
    {
        protected override void LoadSamples()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>($"Audio/{instrumentName}");
            foreach (var clip in clips) sampleClips[clip.name] = clip;
        }

        public override void ProcessMidi(MidiData midi)
        {
            if (midi.isOn)
            {
                AudioSource source = GetFreeVoice();
                var (clip, pitch) = CalculatePitchAndSample(midi.note);
                if (clip != null)
                {
                    source.clip = clip;
                    source.pitch = pitch;
                    source.volume = midi.velocity;
                    source.Play();
                    activeVoices[midi.note] = source;
                }
            }
            else if (activeVoices.TryGetValue(midi.note, out AudioSource source))
            {
                source.Stop();
                activeVoices.Remove(midi.note);
            }
        }

        private (AudioClip, float) CalculatePitchAndSample(int midiNote)
        {
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            string targetBase;
            int sourceIdx;

            if (noteIndex >= 1 && noteIndex <= 6) { targetBase = "Ds" + octave; sourceIdx = 3; }
            else { int tOct = (noteIndex <= 1) ? octave - 1 : octave; targetBase = "A" + tOct; sourceIdx = 9; }

            if (sampleClips.TryGetValue(targetBase, out AudioClip clip))
            {
                int diff = noteIndex - sourceIdx;
                if (noteIndex <= 1 && sourceIdx == 9) diff += 12;
                return (clip, Mathf.Pow(1.059463f, diff));
            }
            return (null, 1.0f);
        }
    }

    public class PercussionInstrument : InstrumentEngine
    {
        protected override void LoadSamples()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>($"Audio/{instrumentName}");
            foreach (var clip in clips)
            {
                string[] parts = clip.name.Split('_');
                if (parts.Length > 0) sampleClips[parts[0]] = clip;
            }
        }

        public override void ProcessMidi(MidiData midi)
        {
            if (midi.isOn && sampleClips.TryGetValue(midi.note.ToString(), out AudioClip clip))
            {
                AudioSource source = GetFreeVoice();
                source.clip = clip;
                source.pitch = 1.0f;
                source.volume = midi.velocity;
                source.Play();
            }
        }
    }
}