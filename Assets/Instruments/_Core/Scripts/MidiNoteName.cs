namespace Instruments
{
    /// <summary>
    /// MIDI 노트 번호를 화면 표기용 문자열로 변환. MIDI 60 = C4 컨벤션.
    /// 트럼본은 B♭ key 악기이므로 enharmonic 은 플랫 우선, 폰트 호환성 위해 ASCII("Bb" 등).
    /// </summary>
    public static class MidiNoteName
    {
        static readonly string[] s_Flats =
            { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };

        public static string ToDisplay(int midi)
        {
            int n = ((midi % 12) + 12) % 12;
            int octave = (midi - n) / 12 - 1;
            return s_Flats[n] + octave.ToString();
        }
    }
}
