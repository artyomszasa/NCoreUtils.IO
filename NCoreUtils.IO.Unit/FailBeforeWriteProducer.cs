using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class FailBeforeWriteProducer : IStreamProducer
    {
        public ValueTask DisposeAsync() => default;

        public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        {
            throw new ExpectedException();
        }
    }
}