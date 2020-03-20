using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public interface IStreamProducer : IAsyncDisposable
    {
        ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default);
    }
}