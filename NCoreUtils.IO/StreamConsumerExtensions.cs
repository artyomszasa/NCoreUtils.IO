using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class StreamConsumerExtensions
    {
        public static async Task<string> ToStringAsync(
            this IStreamProducer producer,
            Encoding? encoding = default,
            int copyBufferSize = StreamConsumer.DefaultBufferSize,
            CancellationToken cancellationToken = default)
        {
            await using var consumer = StreamConsumer.ToString(encoding, copyBufferSize);
            return await producer.ConsumeAsync(consumer).ConfigureAwait(false);
        }

        public static async Task<byte[]> ToArrayAsync(
            this IStreamProducer producer,
            int copyBufferSize = StreamConsumer.DefaultBufferSize,
            CancellationToken cancellationToken = default)
        {
            await using var consumer = StreamConsumer.ToArray(copyBufferSize);
            return await producer.ConsumeAsync(consumer).ConfigureAwait(false);
        }
    }
}