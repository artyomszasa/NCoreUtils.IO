using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class FailBeforeWriteProducer : IStreamProducer
    {
#pragma warning disable CA1816
        public ValueTask DisposeAsync() => default;
#pragma warning restore CA1816

        public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        {
            throw new ExpectedException();
        }
    }
}