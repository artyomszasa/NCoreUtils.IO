using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public static class StreamTransformation
{
    sealed class InlineStreamTransformation(Func<Stream, Stream, CancellationToken, ValueTask> transform, Func<ValueTask>? dispose) : IStreamTransformation
    {
        private Func<Stream, Stream, CancellationToken, ValueTask> TransformFun { get; } = transform ?? throw new ArgumentNullException(nameof(transform));

        private Func<ValueTask>? DisposeFun { get; } = dispose;

        public ValueTask DisposeAsync()
        {
            if (DisposeFun is not null)
            {
                return DisposeFun.Invoke();
            }
            return default;
        }

        public ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
            => TransformFun(input, output, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamTransformation Create(Func<Stream, Stream, CancellationToken, ValueTask> transform, Func<ValueTask>? dispose = default)
        => new InlineStreamTransformation(transform, dispose);
}