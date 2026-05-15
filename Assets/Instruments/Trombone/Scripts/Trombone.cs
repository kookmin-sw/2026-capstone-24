using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 트럼본 악기 컴포넌트.
    /// InstrumentBase를 상속해 InstrumentTeleportLink.linkedInstrument 등록 기반을 제공한다.
    /// 발음 로직은 02-slide-midi plan에서 채운다.
    /// </summary>
    [AddComponentMenu("Instruments/Trombone")]
    public class Trombone : InstrumentBase
    {
        protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)
        {
            // 02-slide-midi stub — 슬라이드 MIDI 발음은 다음 plan에서 구현.
            playback = default;
            return false;
        }

        protected override void Initialize()
        {
            // audioOutput이 없어도 컴포넌트 동작 유지 (02-slide-midi 전까지 발음 불필요).
            // 부모의 Initialize()는 audioOutput null 시 enabled=false 처리를 하므로
            // 트럼본은 오디오 없이도 앵커·그립 기능이 동작해야 해 base 호출을 생략한다.
        }
    }
}
