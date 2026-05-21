using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// TestSceneSanyo 의 LocalHandPoseSource GameObject 에 부착.
    /// leftWristSource / rightWristSource 를 Inspector 에서
    /// 씬 안의 LeftPlayHand/L_Wrist / RightPlayHand/R_Wrist 본 transform 에 직접 wiring.
    /// LateUpdate 에서 자기 PlayerHandRig 의 [Networked] 필드에 wrist pose 를 write.
    /// 다른 플레이어의 PlayerHandRig 에는 쓰지 않는다 (InputAuthority 가드).
    /// PlayerHandRig 미발견 (룸 미합류 또는 spawn 직전) 시 silent skip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalHandPoseSource : MonoBehaviour
    {
        private const string LeftPlayHandName = "LeftPlayHand";
        private const string RightPlayHandName = "RightPlayHand";

        [SerializeField] private Transform leftWristSource;
        [SerializeField] private Transform rightWristSource;

        private PlayerHandRig _cachedRig;

        private void LateUpdate()
        {
            EnsureWristSources();

            if (!TryGetOrRefreshRig())
            {
                return;
            }

            WriteWristPose();
        }

        private void EnsureWristSources()
        {
            if (leftWristSource == null)
            {
                GameObject left = GameObject.Find(LeftPlayHandName);
                if (left != null) leftWristSource = left.transform;
            }

            if (rightWristSource == null)
            {
                GameObject right = GameObject.Find(RightPlayHandName);
                if (right != null) rightWristSource = right.transform;
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

        private void WriteWristPose()
        {
            // dedicated-server 모델: client 는 InputAuthority 만 보유. [Networked] 직접 쓰기 대신 RPC 로 server 에 pose 전달.
            if (leftWristSource != null && _cachedRig.LeftHand != null)
            {
                _cachedRig.LeftHand.RPC_PushPose(leftWristSource.position, leftWristSource.rotation);
            }

            if (rightWristSource != null && _cachedRig.RightHand != null)
            {
                _cachedRig.RightHand.RPC_PushPose(rightWristSource.position, rightWristSource.rotation);
            }
        }
    }
}
