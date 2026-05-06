using UnityEngine;
using UnityEngine.Audio;

namespace SessionPanel
{
    public class SessionVolumeBootstrap : MonoBehaviour
    {
        [SerializeField] private AudioMixer sessionMixer;

        private void Awake()
        {
            if (sessionMixer == null)
            {
                Debug.LogError("[SessionVolumeBootstrap] sessionMixer가 Inspector에서 할당되지 않았습니다.", this);
                return;
            }
            SessionVolume.Bind(sessionMixer);
        }
    }
}
