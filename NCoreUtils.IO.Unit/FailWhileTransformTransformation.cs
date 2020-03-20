using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class FailWhileTransformTransformation : IStreamTransformation
    {
        public ValueTask DisposeAsync() => default;

        public async ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
        {
            var buffer = new byte[20];
            var read = await input.ReadAsync(buffer, 0, 20, cancellationToken);
            await output.WriteAsync(buffer, 0, read, cancellationToken);
            await output.FlushAsync(cancellationToken);
            throw new ExpectedException();
        }
    }
}