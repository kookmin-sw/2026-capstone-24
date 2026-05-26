using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerHandRig : NetworkBehaviour
    {
        private const string LeftHandChildName = "LeftHand";
        private const string RightHandChildName = "RightHand";

        private NetworkedWristPose _leftHand;
        private NetworkedWristPose _rightHand;

        public NetworkedWristPose LeftHand => _leftHand != null ? _leftHand : (_leftHand = FindWristPose(LeftHandChildName));
        public NetworkedWristPose RightHand => _rightHand != null ? _rightHand : (_rightHand = FindWristPose(RightHandChildName));

        private NetworkedWristPose FindWristPose(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<NetworkedWristPose>() : null;
        }
    }
}
