using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace VRMusicStudio.Audio
{
    /// <summary>
    /// 오디오 믹서 그룹을 동적으로 할당하고 회수하는 풀 매니저입니다.
    /// </summary>
    public class MixerPoolManager : MonoBehaviour
    {
        private static MixerPoolManager _instance;
        public static MixerPoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MixerPoolManager");
                    _instance = go.AddComponent<MixerPoolManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Pool Settings")]
        [SerializeField] private string mixerAssetName = "MasterMixer";
        [SerializeField] private string groupPrefix = "Voice_";
        
        private List<AudioMixerGroup> _availableGroups = new List<AudioMixerGroup>();
        private HashSet<AudioMixerGroup> _inUseGroups = new HashSet<AudioMixerGroup>();

        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePool();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitializePool()
        {
            // Resources.Load는 확장자를 제외한 이름을 사용합니다.
            var mixer = Resources.Load<AudioMixer>(mixerAssetName);
            if (mixer == null)
            {
                Debug.LogError($"[MixerPoolManager] AudioMixer '{mixerAssetName}'을 Resources 폴더에서 찾을 수 없습니다.");
                return;
            }

            _availableGroups.Clear();
            _inUseGroups.Clear();

            // 모든 믹서 그룹을 탐색합니다. (FindMatchingGroups("")는 루트부터 모든 그룹을 반환)
            var allGroups = mixer.FindMatchingGroups(string.Empty); 
            string prefixLower = groupPrefix.ToLower();

            foreach (var group in allGroups)
            {
                if (group.name.ToLower().StartsWith(prefixLower))
                {
                    _availableGroups.Add(group);
                }
            }

            if (_availableGroups.Count > 0)
            {
                Debug.Log($"[MixerPoolManager] Initialization Successful. Found {_availableGroups.Count} groups starting with '{groupPrefix}'.", this);
                foreach(var g in _availableGroups) Debug.Log($" - Found Group: {g.name}");
            }
            else
            {
                Debug.LogWarning($"[MixerPoolManager] No groups found starting with '{groupPrefix}' in {mixerAssetName}. Please check your Mixer setup.");
            }
        }

        public AudioMixerGroup RequestGroup()
        {
            if (_availableGroups.Count > 0)
            {
                var group = _availableGroups[0];
                _availableGroups.RemoveAt(0);
                _inUseGroups.Add(group);
                return group;
            }

            Debug.LogWarning("[MixerPoolManager] No available mixer groups in pool! Falling back to Master.");
            return null;
        }

        public void ReturnGroup(AudioMixerGroup group)
        {
            if (group == null) return;

            if (_inUseGroups.Remove(group))
            {
                _availableGroups.Add(group);
            }
        }
    }
}
