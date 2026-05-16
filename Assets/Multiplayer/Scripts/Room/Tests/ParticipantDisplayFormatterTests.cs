using Murang.Multiplayer.Presence;
using NUnit.Framework;

public class ParticipantDisplayFormatterTests
{
    [Test]
    public void FormatLocal_WithFullUlidPlayerId_ReturnsPrefixedWithYouSuffix()
    {
        // 26자 ULID 가정
        string playerId = "01HXMNT0PQRSTUVWXYZ123456";

        string result = ParticipantDisplayFormatter.FormatLocal(playerId);

        Assert.That(result, Is.EqualTo("01HXMN (You)"));
    }

    [Test]
    public void FormatLocal_WithShortPlayerId_DoesNotTrim()
    {
        string result = ParticipantDisplayFormatter.FormatLocal("abc");

        Assert.That(result, Is.EqualTo("abc (You)"));
    }

    [Test]
    public void FormatLocal_WithNullPlayerId_ReturnsUnknownYou()
    {
        string result = ParticipantDisplayFormatter.FormatLocal(null);

        Assert.That(result, Is.EqualTo("Unknown (You)"));
    }

    [Test]
    public void FormatLocal_WithWhitespacePlayerId_ReturnsUnknownYou()
    {
        string result = ParticipantDisplayFormatter.FormatLocal("   ");

        Assert.That(result, Is.EqualTo("Unknown (You)"));
    }

    [Test]
    public void FormatRemote_WithPlayerId_ReturnsPrefixOnly()
    {
        string result = ParticipantDisplayFormatter.FormatRemote(playerNumber: 3, playerIdOrNull: "01HXMNT0PQRSTUVWXYZ123456");

        Assert.That(result, Is.EqualTo("01HXMN"));
    }

    [Test]
    public void FormatRemote_WithoutPlayerId_FallsBackToPlayerNumber()
    {
        string result = ParticipantDisplayFormatter.FormatRemote(playerNumber: 3, playerIdOrNull: null);

        Assert.That(result, Is.EqualTo("Player 3"));
    }

    [Test]
    public void FormatRemote_WithBlankPlayerId_FallsBackToPlayerNumber()
    {
        string result = ParticipantDisplayFormatter.FormatRemote(playerNumber: 7, playerIdOrNull: "  ");

        Assert.That(result, Is.EqualTo("Player 7"));
    }

    [Test]
    public void FormatRemote_WithShortPlayerId_DoesNotTrim()
    {
        string result = ParticipantDisplayFormatter.FormatRemote(playerNumber: 9, playerIdOrNull: "abc");

        Assert.That(result, Is.EqualTo("abc"));
    }
}
