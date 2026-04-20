using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피아노 악기의 MIDI 입력을 처리하고 피아노 샘플을 재생합니다.
/// </summary>
[DefaultExecutionOrder(10030)]
[DisallowMultipleComponent]
public class Piano : InstrumentBase
{
    const int KeyCount = 88;
    const int FirstMidiNote = 21;

    [SerializeField, Range(0f, 1f)] float fingertipSwitchMargin = 0.05f;

    readonly List<PianoKeyPressSensor> m_KeySensors = new List<PianoKeyPressSensor>();
    readonly List<PianoKeyPressSensor.PressCandidate> m_CandidateScratch = new List<PianoKeyPressSensor.PressCandidate>();
    readonly Dictionary<PianoKeyPressSensor, float> m_ResolvedPressBySensor = new Dictionary<PianoKeyPressSensor, float>();
    readonly Dictionary<Fingertip, FingertipClaim> m_CurrentClaims = new Dictionary<Fingertip, FingertipClaim>();
    readonly Dictionary<Fingertip, FingertipClaim> m_NextClaims = new Dictionary<Fingertip, FingertipClaim>();

    readonly struct FingertipClaim
    {
        public FingertipClaim(PianoKeyPressSensor.PressCandidate candidate)
        {
            Sensor = candidate.Sensor;
            Press = candidate.Press;
            LateralOverlap = candidate.LateralOverlap;
            KeyIndex = candidate.KeyIndex;
        }

        public PianoKeyPressSensor Sensor { get; }
        public float Press { get; }
        public float LateralOverlap { get; }
        public int KeyIndex { get; }
    }

    protected override void Awake()
    {
        CacheKeySensors();
        base.Awake();
    }

    void LateUpdate()
    {
        ResolveKeyPresses();
    }

    void OnTransformChildrenChanged()
    {
        CacheKeySensors();
    }

    // 물리 건반 센서 등에서 호출하는 기존 API 유지
    public void NoteOn(int keyIndex, float velocity)
    {
        if (!IsValidKeyIndex(keyIndex))
            return;

        TriggerMidi(new MidiEvent(FirstMidiNote + keyIndex, velocity, true));
    }

    public void NoteOff(int keyIndex)
    {
        if (!IsValidKeyIndex(keyIndex))
            return;

        TriggerMidi(new MidiEvent(FirstMidiNote + keyIndex, 0f, false));
    }

    protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)
    {
        playback = default;

        if (!TryGetAudioBank(out Dictionary<string, AudioClip> bank))
            return false;

        if (!TryResolveMelodicPlayback(midiEvent.Note, bank, out AudioClip clip, out float pitch))
            return false;

        playback = new NotePlayback(clip, pitch, midiEvent.Velocity);
        return true;
    }

    bool TryResolveMelodicPlayback(int midiNote, Dictionary<string, AudioClip> bank, out AudioClip clip, out float pitch)
    {
        clip = null;
        pitch = 1f;

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

        if (!bank.TryGetValue(targetBase, out clip))
            return false;

        int diff = noteIndex - sourceIndex;
        if (noteIndex <= 1 && sourceIndex == 9)
            diff += 12;

        pitch = Mathf.Pow(1.059463f, diff);
        return true;
    }

    void ResolveKeyPresses()
    {
        if (m_KeySensors.Count == 0)
            CacheKeySensors();

        m_CandidateScratch.Clear();
        m_ResolvedPressBySensor.Clear();
        m_NextClaims.Clear();

        for (int i = 0; i < m_KeySensors.Count; i++)
        {
            PianoKeyPressSensor sensor = m_KeySensors[i];
            if (sensor == null || !sensor.isActiveAndEnabled)
                continue;

            sensor.CollectPressCandidates(m_CandidateScratch);
            m_ResolvedPressBySensor[sensor] = 0f;
        }

        for (int i = 0; i < m_CandidateScratch.Count; i++)
        {
            PianoKeyPressSensor.PressCandidate candidate = m_CandidateScratch[i];
            if (candidate.Fingertip == null || candidate.Sensor == null)
                continue;

            m_CurrentClaims.TryGetValue(candidate.Fingertip, out FingertipClaim currentClaim);
            if (!m_NextClaims.TryGetValue(candidate.Fingertip, out FingertipClaim bestClaim))
            {
                m_NextClaims[candidate.Fingertip] = new FingertipClaim(candidate);
                continue;
            }

            if (IsCandidatePreferred(candidate, bestClaim, currentClaim.Sensor))
                m_NextClaims[candidate.Fingertip] = new FingertipClaim(candidate);
        }

        foreach (KeyValuePair<Fingertip, FingertipClaim> pair in m_NextClaims)
        {
            FingertipClaim claim = pair.Value;
            if (claim.Sensor == null)
                continue;

            if (!m_ResolvedPressBySensor.TryGetValue(claim.Sensor, out float resolvedPress) || claim.Press > resolvedPress)
                m_ResolvedPressBySensor[claim.Sensor] = claim.Press;
        }

        for (int i = 0; i < m_KeySensors.Count; i++)
        {
            PianoKeyPressSensor sensor = m_KeySensors[i];
            if (sensor == null || !sensor.isActiveAndEnabled)
                continue;

            float resolvedPress = 0f;
            m_ResolvedPressBySensor.TryGetValue(sensor, out resolvedPress);
            sensor.ApplyResolvedPress(resolvedPress);
        }

        m_CurrentClaims.Clear();
        foreach (KeyValuePair<Fingertip, FingertipClaim> pair in m_NextClaims)
            m_CurrentClaims[pair.Key] = pair.Value;
    }

    void CacheKeySensors()
    {
        m_KeySensors.Clear();
        m_KeySensors.AddRange(GetComponentsInChildren<PianoKeyPressSensor>(true));
    }

    bool IsCandidatePreferred(PianoKeyPressSensor.PressCandidate candidate, FingertipClaim currentBest, PianoKeyPressSensor currentOwner)
    {
        bool candidateIsOwner = candidate.Sensor == currentOwner;
        bool bestIsOwner = currentBest.Sensor == currentOwner;

        if (candidateIsOwner && candidate.Press + fingertipSwitchMargin >= currentBest.Press)
            return true;

        if (bestIsOwner && currentBest.Press + fingertipSwitchMargin >= candidate.Press)
            return false;

        float pressDelta = candidate.Press - currentBest.Press;
        if (!Mathf.Approximately(pressDelta, 0f))
            return pressDelta > 0f;

        float overlapDelta = candidate.LateralOverlap - currentBest.LateralOverlap;
        if (!Mathf.Approximately(overlapDelta, 0f))
            return overlapDelta > 0f;

        if (candidateIsOwner != bestIsOwner)
            return candidateIsOwner;

        return candidate.KeyIndex < currentBest.KeyIndex;
    }

    bool IsValidKeyIndex(int keyIndex)
    {
        if (keyIndex >= 0 && keyIndex < KeyCount)
            return true;

        Debug.LogWarning($"[Piano] 유효하지 않은 건반 인덱스가 전달되었습니다: {keyIndex}", this);
        return false;
    }
}
