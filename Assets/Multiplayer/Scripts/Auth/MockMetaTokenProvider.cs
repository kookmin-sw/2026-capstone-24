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

        public Task<MetaAuthenticationResult> GetAuthenticationResultAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MetaAuthenticationResult result = new MetaAuthenticationResult(
                _config.MockMetaTokenPrefix + _config.MockAccountId,
                _config.MockAccountId,
                null);

            return Task.FromResult(result);
        }
    }
}
