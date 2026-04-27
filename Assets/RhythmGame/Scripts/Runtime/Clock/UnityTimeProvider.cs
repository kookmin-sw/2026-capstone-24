public sealed class UnityTimeProvider : ITimeProvider
{
    public double Now => UnityEngine.Time.timeAsDouble;
}
