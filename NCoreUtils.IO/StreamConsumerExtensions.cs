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
        var consumer = StreamConsumer.ToString(encoding, copyBufferSize);
        await using (consumer.ConfigureAwait(false))
        {
            return await producer.ConsumeAsync(consumer, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async ValueTask<byte[]> ToArrayAsync(
        this IStreamProducer producer,
        int copyBufferSize = StreamConsumer.DefaultBufferSize,
        CancellationToken cancellationToken = default)
    {
        var consumer = StreamConsumer.ToArray(copyBufferSize);
        await using (consumer.ConfigureAwait(false))
        {
            return await producer.ConsumeAsync(consumer, cancellationToken).ConfigureAwait(false);
        }
    }
}