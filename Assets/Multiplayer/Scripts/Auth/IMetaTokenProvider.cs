using System;
using System.Threading;
using System.Threading.Tasks;

namespace Murang.Multiplayer.Auth
{
    public interface IMetaTokenProvider
    {
        Task<MetaAuthenticationResult> GetAuthenticationResultAsync(CancellationToken cancellationToken);
    }

    public sealed class MetaAuthenticationResult
    {
        public MetaAuthenticationResult(string metaIdToken, string metaAccountId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(metaIdToken))
            {
                throw new ArgumentException("Meta ID token is required.", nameof(metaIdToken));
            }

            if (string.IsNullOrWhiteSpace(metaAccountId))
            {
                throw new ArgumentException("Meta account ID is required.", nameof(metaAccountId));
            }

            MetaIdToken = metaIdToken;
            MetaAccountId = metaAccountId;
            DisplayName = displayName;
        }

        public string MetaIdToken { get; }

        public string MetaAccountId { get; }

        public string DisplayName { get; }
    }
}
