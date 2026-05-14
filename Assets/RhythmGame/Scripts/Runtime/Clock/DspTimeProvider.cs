namespace RhythmGame.Runtime.Clock
{
    public sealed class DspTimeProvider : ITimeProvider
    {
        public double Now => UnityEngine.AudioSettings.dspTime;
    }
}
