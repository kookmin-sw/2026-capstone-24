using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Backend.Dto;

namespace Murang.Multiplayer.Room.Client
{
    /// <summary>
    /// Backend 의 룸 endpoint 두 개 (<c>POST /api/v1/rooms</c>, <c>GET /api/v1/rooms/{id}</c>)
    /// 만 추상화한 좁은 인터페이스. <see cref="RoomProvisioningService"/> 가 의존성
    /// 주입 받아 EditMode 단위 테스트에서 mock 으로 교체할 수 있다.
    /// </summary>
    public interface IRoomBackendApi
    {
        Task<RoomResponse> CreateRoomAsync(
            string accessToken,
            RoomCreateRequest request,
            CancellationToken cancellationToken);

        Task<RoomResponse> GetRoomAsync(
            string accessToken,
            long roomId,
            CancellationToken cancellationToken);
    }
}
