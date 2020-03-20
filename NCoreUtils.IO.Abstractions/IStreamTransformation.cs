using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public interface IStreamTransformation : IAsyncDisposable
    {
        ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default);
    }
}