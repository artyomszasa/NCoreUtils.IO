using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class StreamProducer
    {
        sealed class StreamCopyProducer : IStreamProducer
        {
            readonly bool _leaveOpen;

            readonly int _bufferSize;

            public Stream Source { get; }

            public StreamCopyProducer(Stream source, int bufferSize, bool leaveOpen)
            {
                Source = source ?? throw new ArgumentNullException(nameof(source));
                _bufferSize = bufferSize;
                _leaveOpen = leaveOpen;
            }

            public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
                => new ValueTask(Source.CopyToAsync(output, _bufferSize, cancellationToken));

            public ValueTask DisposeAsync()
            {
                if (!_leaveOpen)
                {
                    Source.Dispose();
                }
                return default;
            }
        }

        sealed class InlineStreamProducer : IStreamProducer
        {
            readonly Func<Stream, CancellationToken, ValueTask> _produce;

            readonly Func<ValueTask>? _dispose;

            public InlineStreamProducer(Func<Stream, CancellationToken, ValueTask> produce, Func<ValueTask>? dispose)
            {
                _produce = produce ?? throw new ArgumentNullException(nameof(produce));
                _dispose = dispose;
            }

            public ValueTask DisposeAsync()
                => _dispose?.Invoke() ?? default;

            public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
                => _produce(output, cancellationToken);
        }

        public const int DefaultBufferSize = 16 * 1024;

        public static Encoding DefaultEncoding { get; } = new UTF8Encoding(false);

        public static IStreamProducer Create(Func<Stream, CancellationToken, ValueTask> produce, Func<ValueTask>? dispose = default)
            => new InlineStreamProducer(produce, dispose);

        public static IStreamProducer FromStream(Stream source, int copyBufferSize = DefaultBufferSize, bool leaveOpen = false)
            => new StreamCopyProducer(source, copyBufferSize, leaveOpen);

        public static IStreamProducer FromArray(byte[] data, int copyBufferSize = DefaultBufferSize)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            return FromStream(new MemoryStream(data, 0, data.Length, false, true), copyBufferSize);
        }

        public static IStreamProducer FromString(string input, Encoding? encoding = default, int copyBufferSize = DefaultBufferSize)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            return FromArray((encoding ?? DefaultEncoding).GetBytes(input), copyBufferSize);
        }
    }
}