using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// PlayerHandRig 의 자식 (LeftHand / RightHand) 에 부착.
    /// wrist global pose (position + rotation) 를 [Networked] 로 보관하고
    /// Render() 에서 자기 transform 에 매 프레임 적용한다.
    /// inputAuthority 클라이언트는 Spawned() 에서 자기 MeshRenderer 를 hide.
    /// </summary>
    public sealed class NetworkedWristPose : NetworkBehaviour
    {
        [Networked] public Vector3 WristPosition { get; set; }
        [Networked] public Quaternion WristRotation { get; set; }

        // dedicated-server 모델: client 는 InputAuthority 만 보유, [Networked] 쓰기는 StateAuthority (server) 만 가능.
        // 따라서 client 의 LocalHandPoseSource 는 직접 set 대신 본 RPC 로 server 에 pose 전달, server 가 [Networked] 에 적용한다.
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
        public void RPC_PushPose(Vector3 pos, Quaternion rot)
        {
            WristPosition = pos;
            WristRotation = rot;
        }

        public override void Spawned()
        {
            if (Object.HasInputAuthority)
            {
                // 자기 PlayerHandRig 의 시각 큐브를 hide (transform 은 활성 유지).
                MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>(includeInactive: false);
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }
            }
        }

        public override void Render()
        {
            transform.SetPositionAndRotation(WristPosition, WristRotation);
        }
    }
}
