using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class FailBeforeTransformTransformation : IStreamTransformation
    {
#pragma warning disable CA1816
        public ValueTask DisposeAsync() => default;
#pragma warning restore CA1816

        public ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
        {
            throw new ExpectedException();
        }
    }
}