using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Piano : MonoBehaviour
{
    [SerializeField] PianoAudioOutput audioOutput;

    readonly HashSet<int> m_ActiveKeys = new HashSet<int>();
    PianoMidiMapper m_MidiMapper;
    PianoSoundEngine m_SoundEngine;

    void Awake()
    {
        if (audioOutput == null)
            audioOutput = GetComponentInChildren<PianoAudioOutput>(true);

        if (audioOutput == null)
        {
            Debug.LogError("[Piano] PianoAudioOutput child is missing.", this);
            enabled = false;
            return;
        }

        m_MidiMapper = new PianoMidiMapper();
        m_SoundEngine = new PianoSoundEngine(audioOutput);
    }

    void OnDisable()
    {
        m_ActiveKeys.Clear();
        if (audioOutput != null)
            audioOutput.StopAllVoices();
    }

    public void NoteOn(int keyIndex, float velocity)
    {
        if (!IsValidKeyIndex(keyIndex))
            return;

        if (!m_ActiveKeys.Add(keyIndex))
            return;

        m_SoundEngine?.Handle(m_MidiMapper.CreateNoteOn(keyIndex, velocity));
        // Send Data to Server to BroadCast Sounds Here
    }

    public void NoteOff(int keyIndex)
    {
        if (!IsValidKeyIndex(keyIndex))
            return;

        if (!m_ActiveKeys.Remove(keyIndex))
            return;

        m_SoundEngine?.Handle(m_MidiMapper.CreateNoteOff(keyIndex));
        // Send Data to Server to BroadCast Sounds Here
    }

    bool IsValidKeyIndex(int keyIndex)
    {
        if (keyIndex >= 0 && keyIndex < PianoMidiMapper.KeyCount)
            return true;

        Debug.LogWarning($"[Piano] Ignoring invalid key index {keyIndex}.", this);
        return false;
    }
}
