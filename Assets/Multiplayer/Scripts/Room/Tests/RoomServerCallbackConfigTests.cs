using System;
using System.Collections.Generic;
using Murang.Multiplayer.Room.Server;
using NUnit.Framework;

public class RoomServerCallbackConfigTests
{
    [Test]
    public void TryParse_WithAllRequiredVars_ReturnsConfig()
    {
        var env = new Dictionary<string, string>
        {
            { RoomServerCallbackConfig.EnvRoomId, "42" },
            { RoomServerCallbackConfig.EnvReadyCallbackUrl, "http://ec2.example:8080/internal/rooms/42/ready" },
            { RoomServerCallbackConfig.EnvHeartbeatCallbackUrl, "http://ec2.example:8080/internal/rooms/42/heartbeat" },
            { RoomServerCallbackConfig.EnvRoomRuntimeVersion, "v0.1.0" },
            { RoomServerCallbackConfig.EnvSharedSecret, "secret123" }
        };

        RoomServerCallbackConfig config = RoomServerCallbackConfig.TryParse(env);

        Assert.That(config, Is.Not.Null);
        Assert.That(config.RoomId, Is.EqualTo(42L));
        Assert.That(config.ReadyCallbackUrl, Is.EqualTo("http://ec2.example:8080/internal/rooms/42/ready"));
        Assert.That(config.HeartbeatCallbackUrl, Is.EqualTo("http://ec2.example:8080/internal/rooms/42/heartbeat"));
        Assert.That(config.RoomRuntimeVersion, Is.EqualTo("v0.1.0"));
        Assert.That(config.SharedSecret, Is.EqualTo("secret123"));
    }

    [Test]
    public void TryParse_MissingRoomId_ReturnsNull()
    {
        var env = new Dictionary<string, string>
        {
            { RoomServerCallbackConfig.EnvReadyCallbackUrl, "http://ec2.example:8080/internal/rooms/42/ready" },
            { RoomServerCallbackConfig.EnvHeartbeatCallbackUrl, "http://ec2.example:8080/internal/rooms/42/heartbeat" }
        };

        Assert.That(RoomServerCallbackConfig.TryParse(env), Is.Null);
    }

    [Test]
    public void TryParse_MissingReadyUrl_ReturnsNull()
    {
        var env = new Dictionary<string, string>
        {
            { RoomServerCallbackConfig.EnvRoomId, "42" },
            { RoomServerCallbackConfig.EnvHeartbeatCallbackUrl, "http://ec2.example:8080/internal/rooms/42/heartbeat" }
        };

        Assert.That(RoomServerCallbackConfig.TryParse(env), Is.Null);
    }

    [Test]
    public void TryParse_BlankUrls_ReturnsNullAsIfMissing()
    {
        var env = new Dictionary<string, string>
        {
            { RoomServerCallbackConfig.EnvRoomId, "42" },
            { RoomServerCallbackConfig.EnvReadyCallbackUrl, "   " },
            { RoomServerCallbackConfig.EnvHeartbeatCallbackUrl, "   " }
        };

        Assert.That(RoomServerCallbackConfig.TryParse(env), Is.Null);
    }

    [Test]
    public void TryParse_InvalidRoomId_Throws()
    {
        var env = new Dictionary<string, string>
        {
            { RoomServerCallbackConfig.EnvRoomId, "not-a-number" },
            { RoomServerCallbackConfig.EnvReadyCallbackUrl, "http://ec2.example/ready" },
            { RoomServerCallbackConfig.EnvHeartbeatCallbackUrl, "http://ec2.example/heartbeat" }
        };

        Assert.Throws<InvalidOperationException>(() => RoomServerCallbackConfig.TryParse(env));
    }

    [Test]
    public void TryParse_NegativeRoomId_Throws()
    {
        var env = new Dictionary<string, string>
        {
            { RoomServerCallbackConfig.EnvRoomId, "-5" },
            { RoomServerCallbackConfig.EnvReadyCallbackUrl, "http://ec2.example/ready" },
            { RoomServerCallbackConfig.EnvHeartbeatCallbackUrl, "http://ec2.example/heartbeat" }
        };

        Assert.Throws<InvalidOperationException>(() => RoomServerCallbackConfig.TryParse(env));
    }

    [Test]
    public void TryParse_NonHttpUrl_Throws()
    {
        var env = new Dictionary<string, string>
        {
            { RoomServerCallbackConfig.EnvRoomId, "42" },
            { RoomServerCallbackConfig.EnvReadyCallbackUrl, "ftp://ec2.example/ready" },
            { RoomServerCallbackConfig.EnvHeartbeatCallbackUrl, "http://ec2.example/heartbeat" }
        };

        Assert.Throws<InvalidOperationException>(() => RoomServerCallbackConfig.TryParse(env));
    }

    [Test]
    public void TryParse_OmittedRuntimeVersion_UsesUnknownFallback()
    {
        var env = new Dictionary<string, string>
        {
            { RoomServerCallbackConfig.EnvRoomId, "42" },
            { RoomServerCallbackConfig.EnvReadyCallbackUrl, "http://ec2.example/ready" },
            { RoomServerCallbackConfig.EnvHeartbeatCallbackUrl, "http://ec2.example/heartbeat" }
        };

        RoomServerCallbackConfig config = RoomServerCallbackConfig.TryParse(env);

        Assert.That(config, Is.Not.Null);
        Assert.That(config.RoomRuntimeVersion, Is.EqualTo("unknown"));
        Assert.That(config.SharedSecret, Is.Null);
    }

    [Test]
    public void TryParse_NullDictionary_ReturnsNull()
    {
        Assert.That(RoomServerCallbackConfig.TryParse(null), Is.Null);
    }
}
