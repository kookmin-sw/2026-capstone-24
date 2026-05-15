using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Backend.Dto;
using Murang.Multiplayer.Backend.Http;

namespace Murang.Multiplayer.Room.Client
{
    /// <summary>
    /// Production 어댑터: <see cref="IRoomBackendApi"/> 를 실제 HTTP 호출하는
    /// <see cref="BackendApiClient"/> 위로 매핑한다.
    /// </summary>
    public sealed class BackendApiClientRoomAdapter : IRoomBackendApi
    {
        private readonly BackendApiClient _client;

        public BackendApiClientRoomAdapter(BackendApiClient client)
        {
            _client = client;
        }

        public Task<RoomResponse> CreateRoomAsync(
            string accessToken,
            RoomCreateRequest request,
            CancellationToken cancellationToken)
        {
            return _client.CreateRoomAsync(accessToken, request, cancellationToken);
        }

        public Task<RoomResponse> GetRoomAsync(
            string accessToken,
            long roomId,
            CancellationToken cancellationToken)
        {
            return _client.GetRoomAsync(accessToken, roomId, cancellationToken);
        }
    }
}
