using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class StreamTransformation
    {
        sealed class InlineStreamTransformation : IStreamTransformation
        {
            readonly Func<Stream, Stream, CancellationToken, ValueTask> _transformation;

            readonly Func<ValueTask>? _dispose;

            public InlineStreamTransformation(Func<Stream, Stream, CancellationToken, ValueTask> transform, Func<ValueTask>? dispose)
            {
                _transformation = transform ?? throw new ArgumentNullException(nameof(transform));
                _dispose = dispose;
            }

            public ValueTask DisposeAsync()
                => _dispose?.Invoke() ?? default;

            public ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
                => _transformation(input, output, cancellationToken);
        }

        public static IStreamTransformation Create(Func<Stream, Stream, CancellationToken, ValueTask> transform, Func<ValueTask>? dispose = default)
            => new InlineStreamTransformation(transform, dispose);
    }
}