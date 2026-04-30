using System;
using System.Threading;
using System.Threading.Tasks;

namespace Murang.Multiplayer.Auth
{
    public sealed class RealMetaTokenProvider : IMetaTokenProvider
    {
        public Task<string> GetMetaIdTokenAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException("TODO: integrate Meta Platform SDK token acquisition.");
        }
    }
}
