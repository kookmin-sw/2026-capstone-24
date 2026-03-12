using UnityEngine;
using VRMusicStudio.Audio;

public class PianoMidiBridge : MonoBehaviour
{
    [SerializeField] Transform pianoRoot;
    [SerializeField] UniversalAudioEngine audioEngine;
    [SerializeField] int instrumentId = 0;
    [SerializeField] int channel = 1;

    PianoKeySensor[] m_Sensors;

    void OnEnable()
    {
        if (pianoRoot == null)
        {
            Debug.LogError("[PianoMidiBridge] pianoRoot이 할당되지 않았습니다.");
            return;
        }

        if (audioEngine == null)
        {
            audioEngine = GetComponent<UniversalAudioEngine>();
            if (audioEngine == null)
            {
                Debug.LogError("[PianoMidiBridge] UniversalAudioEngine을 찾을 수 없습니다.");
                return;
            }
        }

        m_Sensors = pianoRoot.GetComponentsInChildren<PianoKeySensor>();
        for (int i = 0; i < m_Sensors.Length; i++)
        {
            m_Sensors[i].OnNoteOn += HandleNoteOn;
            m_Sensors[i].OnNoteOff += HandleNoteOff;
        }

        Debug.Log($"[PianoMidiBridge] {m_Sensors.Length}개의 PianoKeySensor를 연결했습니다.");
    }

    void OnDisable()
    {
        if (m_Sensors == null)
            return;

        for (int i = 0; i < m_Sensors.Length; i++)
        {
            if (m_Sensors[i] != null)
            {
                m_Sensors[i].OnNoteOn -= HandleNoteOn;
                m_Sensors[i].OnNoteOff -= HandleNoteOff;
            }
        }
    }

    void HandleNoteOn(PianoKeySensor sensor)
    {
        audioEngine.OnReceiveMidi(new MidiData
        {
            instrumentId = instrumentId,
            channel = channel,
            note = sensor.MidiNote,
            velocity = Mathf.Clamp(sensor.CurrentPressNormalized, 0.1f, 1.0f),
            isOn = true
        });
    }

    void HandleNoteOff(PianoKeySensor sensor)
    {
        audioEngine.OnReceiveMidi(new MidiData
        {
            instrumentId = instrumentId,
            channel = channel,
            note = sensor.MidiNote,
            velocity = 0f,
            isOn = false
        });
    }
}
