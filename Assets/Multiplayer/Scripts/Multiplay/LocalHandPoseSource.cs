using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// TestSceneSanyo 의 LocalHandPoseSource GameObject 에 부착.
    /// leftWristSource / rightWristSource 를 Inspector 에서
    /// 씬 안의 LeftPlayHand/L_Wrist / RightPlayHand/R_Wrist 본 transform 에 직접 wiring.
    /// LateUpdate 에서 자기 PlayerHandRig 의 RPC_PushPose 로 wrist + 25 손가락 본 pose 송신.
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

        private Transform[] _cachedLeftFingerBones;
        private Transform[] _cachedRightFingerBones;
        private readonly Quaternion[] _leftFingerBuffer = new Quaternion[RemoteHandBoneNames.FingerBoneCount];
        private readonly Quaternion[] _rightFingerBuffer = new Quaternion[RemoteHandBoneNames.FingerBoneCount];

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
                FillFingerBuffer(leftWristSource, HandSide.Left, _leftFingerBuffer, ref _cachedLeftFingerBones);
                _cachedRig.LeftHand.RPC_PushPose(leftWristSource.position, leftWristSource.rotation, _leftFingerBuffer);
            }

            if (rightWristSource != null && _cachedRig.RightHand != null)
            {
                FillFingerBuffer(rightWristSource, HandSide.Right, _rightFingerBuffer, ref _cachedRightFingerBones);
                _cachedRig.RightHand.RPC_PushPose(rightWristSource.position, rightWristSource.rotation, _rightFingerBuffer);
            }
        }

        private static void FillFingerBuffer(Transform wristRoot, HandSide side, Quaternion[] buffer, ref Transform[] cache)
        {
            if (cache == null || cache.Length != RemoteHandBoneNames.FingerBoneCount)
            {
                cache = new Transform[RemoteHandBoneNames.FingerBoneCount];
                string[] boneNames = RemoteHandBoneNames.For(side);
                Transform[] all = wristRoot.GetComponentsInChildren<Transform>(includeInactive: true);
                for (int i = 0; i < boneNames.Length; i++)
                {
                    string targetName = boneNames[i];
                    for (int j = 0; j < all.Length; j++)
                    {
                        if (all[j].name == targetName)
                        {
                            cache[i] = all[j];
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < cache.Length; i++)
            {
                buffer[i] = cache[i] != null ? cache[i].localRotation : Quaternion.identity;
            }
        }
    }
}
