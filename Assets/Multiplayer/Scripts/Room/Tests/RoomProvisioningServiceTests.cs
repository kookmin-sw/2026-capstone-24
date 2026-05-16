using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Backend.Dto;
using Murang.Multiplayer.Room.Client;
using NUnit.Framework;

public class RoomProvisioningServiceTests
{
    [Test]
    public async Task ProvisionAndWaitReady_CreateImmediatelyReady_ReturnsWithoutPolling()
    {
        StubRoomBackendApi api = new StubRoomBackendApi();
        api.CreateResponse = new RoomResponse
        {
            roomId = 42,
            photonSessionName = "room-a",
            maxPlayers = 8,
            status = "READY"
        };
        RoomProvisioningService service = NewService(api);

        RoomResponse result = await service.ProvisionAndWaitReadyAsync(
            "token-x",
            NewRequest("room-a"),
            CancellationToken.None);

        Assert.That(result.roomId, Is.EqualTo(42));
        Assert.That(api.CreateCallCount, Is.EqualTo(1));
        Assert.That(api.GetCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ProvisionAndWaitReady_PollsUntilReady()
    {
        StubRoomBackendApi api = new StubRoomBackendApi();
        api.CreateResponse = MakeResponse(42, "SERVER_STARTING");
        api.GetResponses.Enqueue(MakeResponse(42, "SERVER_STARTING"));
        api.GetResponses.Enqueue(MakeResponse(42, "SERVER_STARTING"));
        api.GetResponses.Enqueue(MakeResponse(42, "READY"));
        RoomProvisioningService service = NewService(api);

        RoomResponse result = await service.ProvisionAndWaitReadyAsync(
            "token-x",
            NewRequest("room-a"),
            CancellationToken.None);

        Assert.That(result.IsReady, Is.True);
        Assert.That(api.CreateCallCount, Is.EqualTo(1));
        Assert.That(api.GetCallCount, Is.EqualTo(3));
    }

    [Test]
    public void ProvisionAndWaitReady_TerminalStatusOnCreate_ThrowsProvisioningFailed()
    {
        StubRoomBackendApi api = new StubRoomBackendApi();
        api.CreateResponse = MakeResponse(42, "FAILED");
        RoomProvisioningService service = NewService(api);

        RoomProvisioningFailedException ex = Assert.ThrowsAsync<RoomProvisioningFailedException>(
            async () => await service.ProvisionAndWaitReadyAsync(
                "token-x",
                NewRequest("room-a"),
                CancellationToken.None));

        Assert.That(ex.RoomId, Is.EqualTo(42L));
        Assert.That(ex.LastStatus, Is.EqualTo("FAILED"));
        Assert.That(api.GetCallCount, Is.EqualTo(0));
    }

    [Test]
    public void ProvisionAndWaitReady_TerminalStatusDuringPoll_ThrowsProvisioningFailed()
    {
        StubRoomBackendApi api = new StubRoomBackendApi();
        api.CreateResponse = MakeResponse(42, "SERVER_STARTING");
        api.GetResponses.Enqueue(MakeResponse(42, "SERVER_STARTING"));
        api.GetResponses.Enqueue(MakeResponse(42, "UNHEALTHY"));
        RoomProvisioningService service = NewService(api);

        RoomProvisioningFailedException ex = Assert.ThrowsAsync<RoomProvisioningFailedException>(
            async () => await service.ProvisionAndWaitReadyAsync(
                "token-x",
                NewRequest("room-a"),
                CancellationToken.None));

        Assert.That(ex.LastStatus, Is.EqualTo("UNHEALTHY"));
    }

    [Test]
    public void ProvisionAndWaitReady_ExceedsTimeout_ThrowsTimeoutException()
    {
        StubRoomBackendApi api = new StubRoomBackendApi();
        api.CreateResponse = MakeResponse(42, "SERVER_STARTING");
        // pollInterval=10ms × readyTimeout=50ms 면 최대 ~5회 polling 후 timeout.
        // 머신 속도에 따른 race 를 없애기 위해 fake clock 을 주입한다 — delayAsync 가
        // fakeNow 를 정확히 pollInterval 만큼 전진시키고, nowProvider 가 그 값을 노출.
        DateTime fakeNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 100; i++)
        {
            api.GetResponses.Enqueue(MakeResponse(42, "SERVER_STARTING"));
        }
        RoomProvisioningService service = new RoomProvisioningService(
            api,
            pollInterval: TimeSpan.FromMilliseconds(10),
            readyTimeout: TimeSpan.FromMilliseconds(50),
            delayAsync: (span, ct) => { fakeNow = fakeNow.Add(span); return Task.CompletedTask; },
            nowProvider: () => fakeNow);

        Assert.ThrowsAsync<TimeoutException>(
            async () => await service.ProvisionAndWaitReadyAsync(
                "token-x",
                NewRequest("room-a"),
                CancellationToken.None));
    }

    [Test]
    public void ProvisionAndWaitReady_BlankAccessToken_ThrowsArgumentException()
    {
        RoomProvisioningService service = NewService(new StubRoomBackendApi());

        Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ProvisionAndWaitReadyAsync(
                accessToken: "",
                NewRequest("room-a"),
                CancellationToken.None));
    }

    [Test]
    public void ProvisionAndWaitReady_NullRequest_ThrowsArgumentNullException()
    {
        RoomProvisioningService service = NewService(new StubRoomBackendApi());

        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await service.ProvisionAndWaitReadyAsync(
                "token-x",
                request: null,
                CancellationToken.None));
    }

    [Test]
    public void Constructor_NullApi_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RoomProvisioningService(null));
    }

    private static RoomProvisioningService NewService(IRoomBackendApi api)
    {
        return new RoomProvisioningService(
            api,
            pollInterval: TimeSpan.FromMilliseconds(1),
            readyTimeout: TimeSpan.FromSeconds(5),
            delayAsync: (span, ct) => Task.CompletedTask);
    }

    private static RoomCreateRequest NewRequest(string photonSessionName)
    {
        return new RoomCreateRequest
        {
            photonSessionName = photonSessionName,
            maxPlayers = 8,
            roomRuntimeVersion = "v0.1.0"
        };
    }

    private static RoomResponse MakeResponse(long roomId, string status)
    {
        return new RoomResponse
        {
            roomId = roomId,
            photonSessionName = "room-" + roomId,
            maxPlayers = 8,
            status = status
        };
    }

    private sealed class StubRoomBackendApi : IRoomBackendApi
    {
        public RoomResponse CreateResponse { get; set; }
        public Queue<RoomResponse> GetResponses { get; } = new Queue<RoomResponse>();
        public int CreateCallCount { get; private set; }
        public int GetCallCount { get; private set; }

        public Task<RoomResponse> CreateRoomAsync(
            string accessToken,
            RoomCreateRequest request,
            CancellationToken cancellationToken)
        {
            CreateCallCount++;
            if (CreateResponse == null)
            {
                throw new InvalidOperationException("Stub CreateResponse 가 설정되지 않았습니다.");
            }
            return Task.FromResult(CreateResponse);
        }

        public Task<RoomResponse> GetRoomAsync(
            string accessToken,
            long roomId,
            CancellationToken cancellationToken)
        {
            GetCallCount++;
            if (GetResponses.Count == 0)
            {
                throw new InvalidOperationException("Stub GetResponses 가 부족합니다.");
            }
            return Task.FromResult(GetResponses.Dequeue());
        }
    }
}
