using System.Text.RegularExpressions;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// 로비 패널의 룸 생성/입장 입력값을 backend 검증과 동일한 룰로 점검하는
    /// 순수 정적 클래스. MonoBehaviour 의존성이 없으므로 EditMode 단위 테스트로
    /// 회귀 가드한다.
    ///
    /// <para>backend 측 룰 출처: <c>RoomCreateRequest.java</c>
    /// (photonSessionName 패턴/길이, maxPlayers 범위).</para>
    /// </summary>
    public static class LobbyInputValidator
    {
        public const int MinPhotonSessionNameLength = 1;
        public const int MaxPhotonSessionNameLength = 128;
        public const int MinMaxPlayers = 1;
        public const int MaxMaxPlayers = 32;
        public const int MaxPasswordRawLength = 128;

        public const string FieldPhotonSessionName = "photonSessionName";
        public const string FieldMaxPlayers = "maxPlayers";
        public const string FieldPassword = "password";

        private static readonly Regex PhotonSessionNamePattern =
            new Regex("^[A-Za-z0-9_\\-]+$", RegexOptions.Compiled);

        public static LobbyValidationResult ValidateCreate(
            string photonSessionName,
            int maxPlayers,
            bool passwordEnabled,
            string passwordRaw)
        {
            LobbyValidationResult nameResult = ValidatePhotonSessionName(photonSessionName);
            if (!nameResult.Success)
            {
                return nameResult;
            }

            LobbyValidationResult playersResult = ValidateMaxPlayers(maxPlayers);
            if (!playersResult.Success)
            {
                return playersResult;
            }

            if (passwordEnabled)
            {
                LobbyValidationResult passwordResult = ValidatePassword(passwordRaw, required: true);
                if (!passwordResult.Success)
                {
                    return passwordResult;
                }
            }

            return LobbyValidationResult.Ok();
        }

        public static LobbyValidationResult ValidateJoinPassword(bool locked, string passwordRaw)
        {
            return ValidatePassword(passwordRaw, required: locked);
        }

        public static LobbyValidationResult ValidatePhotonSessionName(string value)
        {
            string trimmed = value == null ? string.Empty : value.Trim();
            if (trimmed.Length < MinPhotonSessionNameLength)
            {
                return LobbyValidationResult.Fail(FieldPhotonSessionName, "룸 이름을 입력해주세요.");
            }
            if (trimmed.Length > MaxPhotonSessionNameLength)
            {
                return LobbyValidationResult.Fail(
                    FieldPhotonSessionName,
                    $"룸 이름은 최대 {MaxPhotonSessionNameLength}자까지 허용됩니다.");
            }
            if (!PhotonSessionNamePattern.IsMatch(trimmed))
            {
                return LobbyValidationResult.Fail(
                    FieldPhotonSessionName,
                    "룸 이름은 영문/숫자/_/- 만 허용됩니다.");
            }
            return LobbyValidationResult.Ok();
        }

        public static LobbyValidationResult ValidateMaxPlayers(int value)
        {
            if (value < MinMaxPlayers || value > MaxMaxPlayers)
            {
                return LobbyValidationResult.Fail(
                    FieldMaxPlayers,
                    $"정원은 {MinMaxPlayers}~{MaxMaxPlayers} 사이여야 합니다.");
            }
            return LobbyValidationResult.Ok();
        }

        public static LobbyValidationResult ValidatePassword(string raw, bool required)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return required
                    ? LobbyValidationResult.Fail(FieldPassword, "비밀번호를 입력해주세요.")
                    : LobbyValidationResult.Ok();
            }
            if (raw.Length > MaxPasswordRawLength)
            {
                return LobbyValidationResult.Fail(
                    FieldPassword,
                    $"비밀번호는 최대 {MaxPasswordRawLength}자까지 허용됩니다.");
            }
            return LobbyValidationResult.Ok();
        }
    }

    /// <summary>검증 결과. 실패 시 <see cref="ErrorField"/> + <see cref="ErrorMessage"/> 노출.</summary>
    public readonly struct LobbyValidationResult
    {
        public bool Success { get; }
        public string ErrorField { get; }
        public string ErrorMessage { get; }

        private LobbyValidationResult(bool success, string field, string message)
        {
            Success = success;
            ErrorField = field ?? string.Empty;
            ErrorMessage = message ?? string.Empty;
        }

        public static LobbyValidationResult Ok()
        {
            return new LobbyValidationResult(true, string.Empty, string.Empty);
        }

        public static LobbyValidationResult Fail(string field, string message)
        {
            return new LobbyValidationResult(false, field, message);
        }
    }
}
