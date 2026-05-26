namespace Murang.Multiplayer.Multiplay
{
    public enum HandSide
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// PlayHand prefab 의 wrist root 아래 25개 손가락/Palm 본 이름.
    /// LocalHandPoseSource (RPC 송신측) 와 NetworkedWristPose.Render (수신 적용측) 가
    /// 동일한 인덱스 순서를 사용해야 본 25개가 일관되게 매핑된다.
    /// 본 이름 정렬: Index → Middle → Ring → Little → Thumb → Palm (Hands CLAUDE.md §3 박제 순서).
    /// </summary>
    public static class RemoteHandBoneNames
    {
        public const int FingerBoneCount = 25;

        public static readonly string[] RightSide = new string[FingerBoneCount]
        {
            "R_IndexMetacarpal", "R_IndexProximal", "R_IndexIntermediate", "R_IndexDistal", "R_IndexTip",
            "R_MiddleMetacarpal", "R_MiddleProximal", "R_MiddleIntermediate", "R_MiddleDistal", "R_MiddleTip",
            "R_RingMetacarpal", "R_RingProximal", "R_RingIntermediate", "R_RingDistal", "R_RingTip",
            "R_LittleMetacarpal", "R_LittleProximal", "R_LittleIntermediate", "R_LittleDistal", "R_LittleTip",
            "R_ThumbMetacarpal", "R_ThumbProximal", "R_ThumbDistal", "R_ThumbTip",
            "R_Palm"
        };

        public static readonly string[] LeftSide = new string[FingerBoneCount]
        {
            "L_IndexMetacarpal", "L_IndexProximal", "L_IndexIntermediate", "L_IndexDistal", "L_IndexTip",
            "L_MiddleMetacarpal", "L_MiddleProximal", "L_MiddleIntermediate", "L_MiddleDistal", "L_MiddleTip",
            "L_RingMetacarpal", "L_RingProximal", "L_RingIntermediate", "L_RingDistal", "L_RingTip",
            "L_LittleMetacarpal", "L_LittleProximal", "L_LittleIntermediate", "L_LittleDistal", "L_LittleTip",
            "L_ThumbMetacarpal", "L_ThumbProximal", "L_ThumbDistal", "L_ThumbTip",
            "L_Palm"
        };

        public static string[] For(HandSide side) => side == HandSide.Left ? LeftSide : RightSide;
    }
}
