using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// PlayerHandRig 의 자식 (Head) 에 부착.
    /// HMD global pose 를 [Networked] 로 보관하고 Render() 에서 head root 에 매 프레임 적용한다.
    /// inputAuthority 클라이언트는 Spawned() 에서 자기 머리 렌더러를 hide (자기 시점에서는 안 보이게).
    /// </summary>
    public sealed class NetworkedHeadPose : NetworkBehaviour
    {
        [Networked] public Vector3 HeadPosition { get; set; }
        [Networked] public Quaternion HeadRotation { get; set; }

        // dedicated-server 모델: client 는 InputAuthority 만 보유, [Networked] 쓰기는 StateAuthority (server) 만 가능.
        // 따라서 client 의 LocalHeadPoseSource 는 직접 set 대신 본 RPC 로 server 에 pose 전달, server 가 [Networked] 에 적용한다.
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
        public void RPC_PushPose(Vector3 pos, Quaternion rot)
        {
            HeadPosition = pos;
            HeadRotation = rot;
        }

        public override void Spawned()
        {
            if (Object.HasInputAuthority)
            {
                // 자기 머리 시각 hide. 레이어 컬링이 아니라 InputAuthority 기준이라 원격 머리는 그대로 보인다.
                foreach (Renderer r in GetComponentsInChildren<Renderer>(includeInactive: false))
                {
                    r.enabled = false;
                }
            }
        }

        public override void Render()
        {
            transform.SetPositionAndRotation(HeadPosition, HeadRotation);
        }
    }
}
