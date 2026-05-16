namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// 인-룸 패널의 참가자 행에 표시할 문자열을 만든다. 1차 출시는 nickname
    /// 채널이 없어 playerId(ULID) prefix 또는 Photon PlayerRef 번호만 사용.
    /// nickname 채널은 후속 plan 책임.
    /// </summary>
    public static class ParticipantDisplayFormatter
    {
        public const int PlayerIdPrefixLength = 6;
        public const string LocalSuffix = " (You)";
        public const string UnknownLabel = "Unknown";

        /// <summary>본인(LocalPlayer)용 라벨.</summary>
        public static string FormatLocal(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return UnknownLabel + LocalSuffix;
            }
            return TakePrefix(playerId) + LocalSuffix;
        }

        /// <summary>다른 유저용 라벨. <paramref name="playerIdOrNull"/> 가 null/빈값이면
        /// Photon PlayerRef 번호로 fallback.</summary>
        public static string FormatRemote(int playerNumber, string playerIdOrNull)
        {
            if (!string.IsNullOrWhiteSpace(playerIdOrNull))
            {
                return TakePrefix(playerIdOrNull);
            }
            return "Player " + playerNumber;
        }

        private static string TakePrefix(string playerId)
        {
            string trimmed = playerId.Trim();
            return trimmed.Length <= PlayerIdPrefixLength
                ? trimmed
                : trimmed.Substring(0, PlayerIdPrefixLength);
        }
    }
}
