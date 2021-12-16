using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace NCoreUtils.IO
{
    public class FailWhileTransformTransformation : IStreamTransformation
    {
#pragma warning disable CA1816
        public ValueTask DisposeAsync() => default;
#pragma warning restore CA1816

        public async ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
        {
            var buffer = new byte[20];
            var read = await input.ReadAsync(buffer.AsMemory(0, 20), cancellationToken);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            await output.FlushAsync(cancellationToken);
            throw new ExpectedException();
        }
    }
}