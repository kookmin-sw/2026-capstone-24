public sealed class DspTimeProvider : ITimeProvider
{
    public double Now => UnityEngine.AudioSettings.dspTime;
}
