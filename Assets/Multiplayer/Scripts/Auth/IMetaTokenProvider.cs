using System.Threading;
using System.Threading.Tasks;

namespace Murang.Multiplayer.Auth
{
    public interface IMetaTokenProvider
    {
        Task<string> GetMetaIdTokenAsync(CancellationToken cancellationToken);
    }
}
