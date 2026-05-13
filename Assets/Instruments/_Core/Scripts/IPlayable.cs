namespace Instruments
{
    public interface IPlayable
    {
        void TriggerMidi(MidiEvent midiEvent);
    }
}
