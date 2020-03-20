using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class FailBeforeTransformTransformation : IStreamTransformation
    {
        public ValueTask DisposeAsync() => default;

        public ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
        {
            throw new ExpectedException();
        }
    }
}