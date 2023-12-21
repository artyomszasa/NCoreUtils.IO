using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public interface IStreamConsumer : IAsyncDisposable
{
    ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default);
}

public interface IStreamConsumer<T> : IAsyncDisposable
{
    ValueTask<T> ConsumeAsync(Stream input, CancellationToken cancellationToken = default);
}