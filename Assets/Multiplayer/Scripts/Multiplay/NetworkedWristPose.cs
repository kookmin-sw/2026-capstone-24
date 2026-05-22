using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// PlayerHandRig 의 자식 (LeftHand / RightHand) 에 부착.
    /// wrist global pose + 25개 손가락/Palm 본 localRotation 을 [Networked] 로 보관하고
    /// Render() 에서 wrist root 와 자식 본 25개에 매 프레임 적용한다.
    /// inputAuthority 클라이언트는 Spawned() 에서 자기 SkinnedMeshRenderer 를 hide.
    /// </summary>
    public sealed class NetworkedWristPose : NetworkBehaviour
    {
        [SerializeField] private HandSide handSide = HandSide.Right;

        [Networked] public Vector3 WristPosition { get; set; }
        [Networked] public Quaternion WristRotation { get; set; }

        // 25개 손가락/Palm 본 localRotation. capacity = RemoteHandBoneNames.FingerBoneCount.
        [Networked, Capacity(RemoteHandBoneNames.FingerBoneCount)]
        public NetworkArray<Quaternion> FingerRotations { get; }

        private Transform[] _cachedFingerBones;

        // dedicated-server 모델: client 는 InputAuthority 만 보유, [Networked] 쓰기는 StateAuthority (server) 만 가능.
        // 따라서 client 의 LocalHandPoseSource 는 직접 set 대신 본 RPC 로 server 에 pose 전달, server 가 [Networked] 에 적용한다.
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
        public void RPC_PushPose(Vector3 pos, Quaternion rot, Quaternion[] fingerRots)
        {
            WristPosition = pos;
            WristRotation = rot;

            if (fingerRots == null)
                return;

            int count = Mathf.Min(fingerRots.Length, RemoteHandBoneNames.FingerBoneCount);
            for (int i = 0; i < count; i++)
            {
                FingerRotations.Set(i, fingerRots[i]);
            }
        }

        public override void Spawned()
        {
            if (Object.HasInputAuthority)
            {
                // 자기 RemoteHand 의 시각 (SkinnedMesh + 기존 cube placeholder fallback) hide.
                foreach (Renderer r in GetComponentsInChildren<Renderer>(includeInactive: false))
                {
                    r.enabled = false;
                }
            }

            CacheFingerBones();
        }

        public override void Render()
        {
            transform.SetPositionAndRotation(WristPosition, WristRotation);

            if (_cachedFingerBones == null)
                return;

            for (int i = 0; i < _cachedFingerBones.Length; i++)
            {
                Transform bone = _cachedFingerBones[i];
                if (bone != null)
                {
                    bone.localRotation = FingerRotations[i];
                }
            }
        }

        private void CacheFingerBones()
        {
            string[] boneNames = RemoteHandBoneNames.For(handSide);
            _cachedFingerBones = new Transform[RemoteHandBoneNames.FingerBoneCount];

            Transform[] all = GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < boneNames.Length; i++)
            {
                string targetName = boneNames[i];
                for (int j = 0; j < all.Length; j++)
                {
                    if (all[j].name == targetName)
                    {
                        _cachedFingerBones[i] = all[j];
                        break;
                    }
                }
            }
        }
    }
}
