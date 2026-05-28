using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// TestSceneSanyo 의 LocalHeadPoseSource GameObject 에 부착.
    /// headSource (VR Player 의 Main Camera = HMD world pose) 를 Inspector 에서 wiring,
    /// 미지정 시 Camera.main 으로 폴백.
    /// LateUpdate 에서 자기 PlayerHandRig 의 Head.RPC_PushPose 로 HMD pose 송신.
    /// 다른 플레이어의 PlayerHandRig 에는 쓰지 않는다 (InputAuthority 가드).
    /// PlayerHandRig 미발견 (룸 미합류 또는 spawn 직전) 시 silent skip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalHeadPoseSource : MonoBehaviour
    {
        [SerializeField] private Transform headSource;

        private PlayerHandRig _cachedRig;

        private void LateUpdate()
        {
            EnsureHeadSource();

            if (headSource == null)
            {
                return;
            }

            if (!TryGetOrRefreshRig())
            {
                return;
            }

            WriteHeadPose();
        }

        private void EnsureHeadSource()
        {
            if (headSource == null && Camera.main != null)
            {
                headSource = Camera.main.transform;
            }
        }

        private bool TryGetOrRefreshRig()
        {
            if (_cachedRig != null)
            {
                return true;
            }

            // Fusion spawn 직후 씬에 PlayerHandRig 가 등장하면 자동 발견.
            PlayerHandRig[] rigs = FindObjectsByType<PlayerHandRig>(FindObjectsSortMode.None);
            foreach (PlayerHandRig rig in rigs)
            {
                // HasInputAuthority == 자기 PlayerHandRig
                if (rig.Object != null && rig.Object.HasInputAuthority)
                {
                    _cachedRig = rig;
                    return true;
                }
            }

            return false;
        }

        private void WriteHeadPose()
        {
            // dedicated-server 모델: client 는 InputAuthority 만 보유. [Networked] 직접 쓰기 대신 RPC 로 server 에 pose 전달.
            if (_cachedRig.Head != null)
            {
                _cachedRig.Head.RPC_PushPose(headSource.position, headSource.rotation);
            }
        }
    }
}
