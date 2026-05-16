using System;
using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Backend.Dto;

namespace Murang.Multiplayer.Room.Client
{
    /// <summary>
    /// 클라이언트가 backend 를 경유해 룸을 생성하고 READY 상태가 될 때까지
    /// 기다리는 흐름을 캡슐화한다.
    ///
    /// <list type="number">
    ///   <item><c>POST /api/v1/rooms</c> 호출로 <c>rooms</c>/<c>room_server_instances</c>
    ///   row 와 ECS RunTask 를 발화시킨다.</item>
    ///   <item><c>GET /api/v1/rooms/{id}</c> 로 status 를 폴링한다.
    ///   <see cref="PollIntervalSeconds"/> 마다 1회.</item>
    ///   <item>status 가 <c>READY</c> 가 되면 응답을 반환.</item>
    /// </list>
    ///
    /// <para>오류 처리:</para>
    /// <list type="bullet">
    ///   <item>응답 status 가 <c>UNHEALTHY</c>/<c>TERMINATING</c>/<c>TERMINATED</c>/<c>FAILED</c>
    ///   가 되면 즉시 <see cref="RoomProvisioningFailedException"/> 으로 종료.</item>
    ///   <item><see cref="ReadyTimeout"/> 초과 시 <see cref="TimeoutException"/>.</item>
    ///   <item>HTTP 오류는 backend 클라이언트에서 그대로 전파된다.</item>
    /// </list>
    /// </summary>
    public sealed class RoomProvisioningService
    {
        public const double DefaultPollIntervalSeconds = 1.5;
        public const double DefaultReadyTimeoutSeconds = 120.0;

        private readonly IRoomBackendApi _api;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly Func<DateTime> _nowProvider;

        public TimeSpan PollInterval { get; }
        public TimeSpan ReadyTimeout { get; }

        public RoomProvisioningService(
            IRoomBackendApi api,
            TimeSpan? pollInterval = null,
            TimeSpan? readyTimeout = null,
            Func<TimeSpan, CancellationToken, Task> delayAsync = null,
            Func<DateTime> nowProvider = null)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            PollInterval = pollInterval ?? TimeSpan.FromSeconds(DefaultPollIntervalSeconds);
            ReadyTimeout = readyTimeout ?? TimeSpan.FromSeconds(DefaultReadyTimeoutSeconds);
            _delayAsync = delayAsync ?? Task.Delay;
            _nowProvider = nowProvider ?? (() => DateTime.UtcNow);
        }

        public async Task<RoomResponse> ProvisionAndWaitReadyAsync(
            string accessToken,
            RoomCreateRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new ArgumentException("Backend access token 이 필요합니다.", nameof(accessToken));
            }
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            RoomResponse created = await _api.CreateRoomAsync(accessToken, request, cancellationToken);

            if (created.IsReady)
            {
                return created;
            }

            ThrowIfTerminal(created);

            return await PollUntilReadyAsync(accessToken, created.roomId, cancellationToken);
        }

        private async Task<RoomResponse> PollUntilReadyAsync(
            string accessToken,
            long roomId,
            CancellationToken cancellationToken)
        {
            DateTime deadline = _nowProvider() + ReadyTimeout;
            string lastStatus = "(no poll)";

            while (true)
            {
                await _delayAsync(PollInterval, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (_nowProvider() >= deadline)
                {
                    throw new TimeoutException(
                        $"룸 {roomId} 가 {ReadyTimeout.TotalSeconds:F0} 초 안에 READY 상태로 진입하지 못했습니다. 마지막 status={lastStatus}.");
                }

                RoomResponse snapshot = await _api.GetRoomAsync(accessToken, roomId, cancellationToken);
                lastStatus = snapshot.status;
                if (snapshot.IsReady)
                {
                    return snapshot;
                }

                ThrowIfTerminal(snapshot);
            }
        }

        private static void ThrowIfTerminal(RoomResponse response)
        {
            if (!response.IsTerminal)
            {
                return;
            }
            throw new RoomProvisioningFailedException(
                response.roomId,
                response.status,
                $"룸 {response.roomId} 가 READY 도달 전 {response.status} 로 종료되었습니다.");
        }
    }

    /// <summary>
    /// Backend 가 보고한 룸 상태가 READY 진입 전 종료 계열(UNHEALTHY/TERMINATING/
    /// TERMINATED/FAILED)이 되었을 때 발생.
    /// </summary>
    public sealed class RoomProvisioningFailedException : Exception
    {
        public long RoomId { get; }
        public string LastStatus { get; }

        public RoomProvisioningFailedException(long roomId, string lastStatus, string message)
            : base(message)
        {
            RoomId = roomId;
            LastStatus = lastStatus;
        }
    }
}
