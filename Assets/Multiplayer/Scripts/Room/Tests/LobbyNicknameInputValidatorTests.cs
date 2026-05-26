using Murang.Multiplayer.Presence;
using NUnit.Framework;

public class LobbyNicknameInputValidatorTests
{
    [Test]
    public void Validate_ValidAlnum_Succeeds()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate("Murang01");

        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorField, Is.Empty);
    }

    [Test]
    public void Validate_UnderscoreAndHyphen_Succeeds()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate("user_01-A");

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void Validate_Empty_Fails()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate("");

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyNicknameInputValidator.FieldNickname));
        Assert.That(result.ErrorMessage, Does.Contain("required"));
    }

    [Test]
    public void Validate_Null_Fails()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate(null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyNicknameInputValidator.FieldNickname));
    }

    [Test]
    public void Validate_WhitespaceOnly_Fails()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate("   ");

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyNicknameInputValidator.FieldNickname));
    }

    [Test]
    public void Validate_Length17_Fails()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate(new string('A', 17));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyNicknameInputValidator.FieldNickname));
        Assert.That(result.ErrorMessage, Does.Contain("16"));
    }

    [Test]
    public void Validate_KoreanChar_Fails()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate("무랑01");

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyNicknameInputValidator.FieldNickname));
    }

    [Test]
    public void Validate_Space_Fails()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate("Murang 01");

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyNicknameInputValidator.FieldNickname));
    }

    [Test]
    public void Validate_Asterisk_Fails()
    {
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate("Bad*Nickname");

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyNicknameInputValidator.FieldNickname));
    }

    [Test]
    public void Validate_TrimsAndPasses()
    {
        // trim 후 "Murang" — 6자, 정규식 통과
        LobbyValidationResult result = LobbyNicknameInputValidator.Validate("  Murang  ");

        Assert.That(result.Success, Is.True);
    }
}
