using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public static class StreamConsumerExtensions
{
    public static async ValueTask<string> ToStringAsync(
        this IStreamProducer producer,
        Encoding? encoding = default,
        int copyBufferSize = StreamConsumer.DefaultBufferSize,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
        await using var consumer = StreamConsumer.ToString(encoding, copyBufferSize);
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
        return await producer.ConsumeAsync(consumer, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<byte[]> ToArrayAsync(
        this IStreamProducer producer,
        int copyBufferSize = StreamConsumer.DefaultBufferSize,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
        await using var consumer = StreamConsumer.ToArray(copyBufferSize);
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
        return await producer.ConsumeAsync(consumer, cancellationToken).ConfigureAwait(false);
    }
}