using System.Threading;
using System.Threading.Tasks;

namespace Murang.Multiplayer.Auth
{
    public sealed class MockMetaTokenProvider : IMetaTokenProvider
    {
        private readonly MultiplayerAuthConfig _config;

        public MockMetaTokenProvider(MultiplayerAuthConfig config)
        {
            _config = config;
        }

        public Task<string> GetMetaIdTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_config.MockMetaTokenPrefix + _config.MockAccountId);
        }
    }
}
