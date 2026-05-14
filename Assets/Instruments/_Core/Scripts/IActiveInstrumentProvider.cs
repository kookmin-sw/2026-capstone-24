using UnityEngine;

namespace Instruments
{
    public interface IActiveInstrumentProvider
    {
        event System.Action<IActiveInstrument> ActiveInstrumentChanged;
        IActiveInstrument Current { get; }
    }

    public interface IActiveInstrument
    {
        Transform PanelAnchor { get; }
        string InstrumentId { get; }
        Transform InstrumentRoot { get; }
        float InstanceVolume { get; set; }
    }
}
