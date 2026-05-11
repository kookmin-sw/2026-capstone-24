using UnityEngine;
using UnityEngine.Audio;

namespace SessionPanel
{
    public static class SessionVolume
    {
        const string MasterKey = "SessionPanel.Volume.Master";

        static string InstanceKey(string id) => string.Format("SessionPanel.Volume.{0}", id);

        static AudioMixer s_Mixer;
        static float s_Master = 0.5f;

        public static void Bind(AudioMixer mixer)
        {
            s_Mixer = mixer;
            s_Master = PlayerPrefs.GetFloat(MasterKey, 0.5f);
            ApplyMasterToMixer(s_Master);
        }

        public static float Master
        {
            get => s_Master;
            set
            {
                s_Master = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterKey, s_Master);
                ApplyMasterToMixer(s_Master);
            }
        }

        public static float LoadInstance(string id, float defaultValue = 0.5f)
        {
            if (string.IsNullOrEmpty(id))
                return defaultValue;
            return PlayerPrefs.GetFloat(InstanceKey(id), defaultValue);
        }

        public static void PersistInstance(string id, float value)
        {
            if (string.IsNullOrEmpty(id))
                return;
            PlayerPrefs.SetFloat(InstanceKey(id), Mathf.Clamp01(value));
        }

        static void ApplyMasterToMixer(float v)
        {
            if (s_Mixer == null)
                return;
            s_Mixer.SetFloat("MasterVolume_dB", LinearToDb(v));
        }

        static float LinearToDb(float v) => Mathf.Log10(Mathf.Max(v, 0.0001f)) * 20f;
    }
}
