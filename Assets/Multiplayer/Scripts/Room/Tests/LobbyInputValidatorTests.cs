using Murang.Multiplayer.Presence;
using NUnit.Framework;

public class LobbyInputValidatorTests
{
    [Test]
    public void ValidateCreate_ValidInputs_NoPassword_Succeeds()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "murang-room-1",
            maxPlayers: 8,
            passwordEnabled: false,
            passwordRaw: null);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorField, Is.Empty);
    }

    [Test]
    public void ValidateCreate_ValidInputs_WithPassword_Succeeds()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "murang-room-1",
            maxPlayers: 4,
            passwordEnabled: true,
            passwordRaw: "secret");

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void ValidateCreate_EmptyRoomName_FailsOnNameField()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "",
            maxPlayers: 4,
            passwordEnabled: false,
            passwordRaw: null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldPhotonSessionName));
    }

    [Test]
    public void ValidateCreate_RoomNameWithSpaces_FailsPattern()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "room with spaces",
            maxPlayers: 4,
            passwordEnabled: false,
            passwordRaw: null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldPhotonSessionName));
        Assert.That(result.ErrorMessage, Does.Contain("letters"));
    }

    [Test]
    public void ValidateCreate_RoomNameWithKoreanChars_FailsPattern()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "내방",
            maxPlayers: 4,
            passwordEnabled: false,
            passwordRaw: null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldPhotonSessionName));
    }

    [Test]
    public void ValidateCreate_RoomNameExactly128Chars_Succeeds()
    {
        string name = new string('a', 128);
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: name,
            maxPlayers: 4,
            passwordEnabled: false,
            passwordRaw: null);

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void ValidateCreate_RoomName129Chars_FailsLength()
    {
        string name = new string('a', 129);
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: name,
            maxPlayers: 4,
            passwordEnabled: false,
            passwordRaw: null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldPhotonSessionName));
    }

    [Test]
    public void ValidateCreate_MaxPlayersZero_Fails()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "room",
            maxPlayers: 0,
            passwordEnabled: false,
            passwordRaw: null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldMaxPlayers));
    }

    [Test]
    public void ValidateCreate_MaxPlayers33_Fails()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "room",
            maxPlayers: 33,
            passwordEnabled: false,
            passwordRaw: null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldMaxPlayers));
    }

    [Test]
    public void ValidateCreate_PasswordEnabledButBlank_Fails()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "room",
            maxPlayers: 4,
            passwordEnabled: true,
            passwordRaw: "");

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldPassword));
    }

    [Test]
    public void ValidateCreate_PasswordEnabledTooLong_Fails()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
            photonSessionName: "room",
            maxPlayers: 4,
            passwordEnabled: true,
            passwordRaw: new string('p', 129));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldPassword));
    }

    [Test]
    public void ValidateJoinPassword_LockedAndBlank_Fails()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateJoinPassword(locked: true, passwordRaw: "");

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorField, Is.EqualTo(LobbyInputValidator.FieldPassword));
    }

    [Test]
    public void ValidateJoinPassword_UnlockedAndBlank_Succeeds()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateJoinPassword(locked: false, passwordRaw: null);

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void ValidateJoinPassword_LockedWithPassword_Succeeds()
    {
        LobbyValidationResult result = LobbyInputValidator.ValidateJoinPassword(locked: true, passwordRaw: "secret");

        Assert.That(result.Success, Is.True);
    }
}
