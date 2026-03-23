using System.Collections.Generic;
using UnityEngine;

public sealed class PianoSoundEngine
{
    readonly Dictionary<string, AudioClip> m_SampleClips = new Dictionary<string, AudioClip>();
    readonly PianoAudioOutput m_AudioOutput;

    public PianoSoundEngine(PianoAudioOutput audioOutput)
    {
        m_AudioOutput = audioOutput;
        LoadSamples();
    }

    public void Handle(PianoMidiEvent midiEvent)
    {
        if (m_AudioOutput == null)
            return;

        if (midiEvent.IsNoteOn)
        {
            var (clip, pitch) = CalculatePitchAndSample(midiEvent.Note);
            if (clip == null)
                return;

            m_AudioOutput.PlayNote(midiEvent.Note, clip, pitch, midiEvent.Velocity);
            return;
        }

        m_AudioOutput.ReleaseNote(midiEvent.Note);
    }

    void LoadSamples()
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/Piano");
        m_SampleClips.Clear();

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip != null)
                m_SampleClips[clip.name] = clip;
        }

        if (m_SampleClips.Count == 0)
            Debug.LogWarning("[PianoSoundEngine] No piano samples were found under Resources/Audio/Piano.");
    }

    (AudioClip clip, float pitch) CalculatePitchAndSample(int midiNote)
    {
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        string targetBase;
        int sourceIndex;

        if (noteIndex >= 1 && noteIndex <= 6)
        {
            targetBase = "Ds" + octave;
            sourceIndex = 3;
        }
        else
        {
            int targetOctave = noteIndex <= 1 ? octave - 1 : octave;
            targetBase = "A" + targetOctave;
            sourceIndex = 9;
        }

        if (!m_SampleClips.TryGetValue(targetBase, out AudioClip clip))
            return (null, 1f);

        int diff = noteIndex - sourceIndex;
        if (noteIndex <= 1 && sourceIndex == 9)
            diff += 12;

        float pitch = Mathf.Pow(1.059463f, diff);
        return (clip, pitch);
    }
}
