using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class StreamTransformation
    {
        sealed class InlineStreamTransformation : IStreamTransformation
        {
            private Func<Stream, Stream, CancellationToken, ValueTask> TransformFun { get; }

            private Func<ValueTask>? DisposeFun { get; }

            public InlineStreamTransformation(Func<Stream, Stream, CancellationToken, ValueTask> transform, Func<ValueTask>? dispose)
            {
                TransformFun = transform ?? throw new ArgumentNullException(nameof(transform));
                DisposeFun = dispose;
            }

            public ValueTask DisposeAsync()
                => DisposeFun?.Invoke() ?? default;

            public ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
                => TransformFun(input, output, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamTransformation Create(Func<Stream, Stream, CancellationToken, ValueTask> transform, Func<ValueTask>? dispose = default)
            => new InlineStreamTransformation(transform, dispose);
    }
}