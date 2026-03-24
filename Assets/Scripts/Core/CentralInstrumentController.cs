using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 악기의 MIDI 이벤트를 수신하고, 알맞은 오디오를 찾아 개별 악기의 AudioOutput으로 전달하는 중앙 제어 시스템입니다.
/// </summary>
public class CentralInstrumentController : MonoBehaviour
{
    private static CentralInstrumentController _instance;
    
    /// <summary>싱글톤 패턴을 사용하여 전역에서 쉽게 접근 가능하도록 합니다.</summary>
    public static CentralInstrumentController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CentralInstrumentController>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CentralInstrumentController");
                    _instance = go.AddComponent<CentralInstrumentController>();
                }
            }
            return _instance;
        }
    }

    // 악기별 로드된 오디오 샘플 캐시 (경로 -> (샘플명 -> AudioClip))
    // 메모리에 한 번만 올리기 위해 캐싱합니다. 예: "Audio/Piano" -> ("Ds4" -> AudioClip)
    private readonly Dictionary<string, Dictionary<string, AudioClip>> _audioBanks = new Dictionary<string, Dictionary<string, AudioClip>>();

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject); // 중복 방지
        }
    }

    /// <summary>
    /// 악기에서 발생한 MIDI 이벤트를 처리하여 해당 악기의 오디오 출력(InstrumentAudioOutput)으로 보냅니다.
    /// </summary>
    /// <param name="midiEvent">발생한 MIDI 이벤트 상세 정보</param>
    /// <param name="instrumentType">음계악기(Melodic)인지 타악기(Percussion)인지 구분</param>
    /// <param name="resourcePath">해당 악기의 오디오 리소스 폴더 경로 (예: "Audio/Piano")</param>
    /// <param name="audioOutput">소리를 재생할 해당 악기의 스피커(오디오 출력 컴포넌트)</param>
    public void ProcessMidiEvent(MidiEvent midiEvent, InstrumentType instrumentType, string resourcePath, InstrumentAudioOutput audioOutput)
    {
        if (audioOutput == null) return;

        // NoteOff 이벤트 처리: 누르고 있던 건반/버튼을 뗀 경우 해당 음을 끕니다.
        if (!midiEvent.IsNoteOn)
        {
            audioOutput.ReleaseNote(midiEvent.Note);
            return;
        }

        // 1. 해당 악기의 오디오 뱅크를 불러옵니다. (없으면 로드)
        if (!_audioBanks.ContainsKey(resourcePath))
        {
            LoadAudioBank(resourcePath);
        }

        Dictionary<string, AudioClip> bank = _audioBanks[resourcePath];
        if (bank.Count == 0) return;

        AudioClip clipToPlay = null;
        float pitch = 1f;

        // 2. 악기군(Type)에 따라 알맞은 샘플 찾기 알고리즘 분기
        if (instrumentType == InstrumentType.Percussion)
        {
            // 타악기 처리: 파일명이 "36_Kick", "38-Snare" 형태이므로 노트 번호가 포함되어 있는지 검사합니다.
            string notePrefix = midiEvent.Note.ToString();
            foreach (var kvp in bank)
            {
                // 이름이 36_ 이나 36- 으로 시작하는 파일 찾기
                if (kvp.Key.StartsWith(notePrefix + "_") || kvp.Key.StartsWith(notePrefix + "-") || kvp.Key == notePrefix)
                {
                    clipToPlay = kvp.Value;
                    pitch = 1f;
                    break;
                }
            }
        }
        else if (instrumentType == InstrumentType.Melodic)
        {
            // 음계악기 처리: 수학적 계산을 통해 기준음과의 차이를 구해서 Pitch 조절
            (clipToPlay, pitch) = CalculateMelodicPitchAndSample(midiEvent.Note, bank);
        }

        // 3. 최종적으로 선택된 소리를 개별 악기의 스피커(AudioOutput)에 재생 명령
        if (clipToPlay != null)
        {
            audioOutput.PlayNote(midiEvent.Note, clipToPlay, pitch, midiEvent.Velocity);
        }
    }

    /// <summary>
    /// Resources 폴더에서 지정된 경로의 오디오 클립들을 딕셔너리로 캐싱합니다.
    /// </summary>
    private void LoadAudioBank(string path)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(path);
        Dictionary<string, AudioClip> bank = new Dictionary<string, AudioClip>();

        foreach (AudioClip clip in clips)
        {
            if (clip != null)
            {
                bank[clip.name] = clip;
            }
        }

        _audioBanks[path] = bank;

        if (clips.Length == 0)
        {
            Debug.LogWarning($"[CentralInstrumentController] 경로에 오디오가 존재하지 않습니다: Resources/{path}");
        }
    }

    /// <summary>
    /// 음계악기 처리 로직: 가장 가까운 기준음(A, Ds) 샘플을 찾아 반음 차이를 Pitch 수치로 변환합니다.
    /// (기존 PianoSoundEngine 구조를 범용화)
    /// </summary>
    private (AudioClip clip, float pitch) CalculateMelodicPitchAndSample(int midiNote, Dictionary<string, AudioClip> bank)
    {
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        
        string targetBase;
        int sourceIndex;

        // C ~ F 구간은 D# (Ds) 샘플을 기준으로 변경
        if (noteIndex >= 1 && noteIndex <= 6)
        {
            targetBase = "Ds" + octave;
            sourceIndex = 3; 
        }
        // F# ~ B 구간은 A 샘플을 기준으로 변경
        else
        {
            int targetOctave = noteIndex <= 1 ? octave - 1 : octave;
            targetBase = "A" + targetOctave;
            sourceIndex = 9; 
        }

        if (!bank.TryGetValue(targetBase, out AudioClip clip))
        {
            return (null, 1f); // 샘플이 없을 경우 기본값 반환
        }

        // 기준음과 요청된 MIDI Note 간의 반음 차이 계산
        int diff = noteIndex - sourceIndex;
        if (noteIndex <= 1 && sourceIndex == 9)
        {
            diff += 12;
        }

        // 1.059463은 2의 (1/12)승. 즉 반음(Semitone) 차이당 음정을 올리고 내리는 비율입니다.
        float pitch = Mathf.Pow(1.059463f, diff);
        return (clip, pitch);
    }
}
