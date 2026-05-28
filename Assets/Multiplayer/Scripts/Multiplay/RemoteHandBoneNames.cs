namespace Murang.Multiplayer.Multiplay
{
    public enum HandSide
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// PlayHand prefab 의 wrist root 아래 10개 손가락 본 이름.
    /// decision 04 hand-pose-payload-reduction 박제 — Proximal + Intermediate × 4fingers + Thumb Proximal/Distal.
    /// LocalHandPoseSource (RPC 송신측) 와 NetworkedWristPose.Render (수신 적용측) 가
    /// 동일한 인덱스 순서를 사용해야 본 10개가 일관되게 매핑된다.
    /// 본 이름 정렬 (index 0-9): Index Proximal/Intermediate → Middle → Ring → Little → Thumb Proximal/Distal.
    /// </summary>
    public static class RemoteHandBoneNames
    {
        // decision 04 §C — Capacity 25 → 10 (Proximal + Intermediate × 4 fingers + Thumb Proximal/Distal).
        public const int FingerBoneCount = 10;

        public static readonly string[] RightSide = new string[FingerBoneCount]
        {
            "R_IndexProximal",   "R_IndexIntermediate",
            "R_MiddleProximal",  "R_MiddleIntermediate",
            "R_RingProximal",    "R_RingIntermediate",
            "R_LittleProximal",  "R_LittleIntermediate",
            "R_ThumbProximal",   "R_ThumbDistal"
        };

        public static readonly string[] LeftSide = new string[FingerBoneCount]
        {
            "L_IndexProximal",   "L_IndexIntermediate",
            "L_MiddleProximal",  "L_MiddleIntermediate",
            "L_RingProximal",    "L_RingIntermediate",
            "L_LittleProximal",  "L_LittleIntermediate",
            "L_ThumbProximal",   "L_ThumbDistal"
        };

        public static string[] For(HandSide side) => side == HandSide.Left ? LeftSide : RightSide;
    }
}
