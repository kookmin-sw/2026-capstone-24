namespace Instruments
{
    public interface IInstanceVolumeStore
    {
        float Load(string id, float fallback);
        void Persist(string id, float value);
    }

    public static class InstanceVolumeStore
    {
        static IInstanceVolumeStore s_active = NullStore.Instance;

        public static IInstanceVolumeStore Active => s_active;

        public static void SetActive(IInstanceVolumeStore store)
        {
            s_active = store ?? NullStore.Instance;
        }

        sealed class NullStore : IInstanceVolumeStore
        {
            public static readonly NullStore Instance = new NullStore();
            public float Load(string id, float fallback) => fallback;
            public void Persist(string id, float value) { }
        }
    }
}
