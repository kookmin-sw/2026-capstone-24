using System.Text.RegularExpressions;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// 닉네임 입력값을 backend 검증과 동일한 룰로 점검하는 순수 정적 클래스.
    /// MonoBehaviour 의존성이 없으므로 EditMode 단위 테스트로 회귀 가드한다.
    ///
    /// <para>규칙: trim 후 1~16자, 정규식 <c>^[A-Za-z0-9_\-]{1,16}$</c></para>
    /// </summary>
    public static class LobbyNicknameInputValidator
    {
        public const int MinLength = 1;
        public const int MaxLength = 16;
        public const string FieldNickname = "nickname";

        private static readonly Regex Pattern = new Regex(@"^[A-Za-z0-9_\-]{1,16}$", RegexOptions.Compiled);

        public static LobbyValidationResult Validate(string raw)
        {
            string trimmed = raw == null ? string.Empty : raw.Trim();
            if (trimmed.Length < MinLength)
            {
                return LobbyValidationResult.Fail(FieldNickname, "Nickname is required.");
            }

            if (trimmed.Length > MaxLength)
            {
                return LobbyValidationResult.Fail(FieldNickname, $"Nickname must be at most {MaxLength} characters.");
            }

            if (!Pattern.IsMatch(trimmed))
            {
                return LobbyValidationResult.Fail(FieldNickname, "Nickname allows letters, digits, _ and - only.");
            }

            return LobbyValidationResult.Ok();
        }
    }
}
