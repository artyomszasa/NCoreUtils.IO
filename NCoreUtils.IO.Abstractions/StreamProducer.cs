using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class StreamProducer
    {
        private sealed class StreamCopyProducer : IStreamProducer
        {
            public bool LeaveOpen { get; }

            public int BufferSize { get; }

            public Stream Source { get; }

            public StreamCopyProducer(Stream source, int bufferSize, bool leaveOpen)
            {
                Source = source ?? throw new ArgumentNullException(nameof(source));
                BufferSize = bufferSize;
                LeaveOpen = leaveOpen;
            }

            public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
                => new(Source.CopyToAsync(output, BufferSize, cancellationToken));

            public ValueTask DisposeAsync()
            {
                if (!LeaveOpen)
                {
                    #if NETFRAMEWORK
                    Source.Dispose();
                    return default;
                    #else
                    return Source.DisposeAsync();
                    #endif
                }
                return default;
            }
        }

        private sealed class InlineStreamProducer : IStreamProducer
        {
            private Func<Stream, CancellationToken, ValueTask> ProducerFun { get; }

            private Func<ValueTask>? DisposeFun { get; }

            public InlineStreamProducer(Func<Stream, CancellationToken, ValueTask> produce, Func<ValueTask>? dispose)
            {
                ProducerFun = produce ?? throw new ArgumentNullException(nameof(produce));
                DisposeFun = dispose;
            }

            public ValueTask DisposeAsync()
                => DisposeFun?.Invoke() ?? default;

            public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
                => ProducerFun(output, cancellationToken);
        }

        private sealed class FromStringProducer : IStreamProducer
        {
            public string Source { get; }

            public Encoding Encoding { get; }

            public int BufferSize { get; }

            public FromStringProducer(Encoding encoding, string source, int bufferSize)
            {
                Source = source ?? throw new ArgumentNullException(nameof(source));
                Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
                BufferSize = bufferSize;
            }

            public async ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
            {
                using var writer = new StreamWriter(output, Encoding, BufferSize, true);
                await writer.WriteAsync(Source);
            }

            public ValueTask DisposeAsync()
                => default;
        }

        private sealed class FromReadOnlyMemoryProducer : IStreamProducer
        {
            private ReadOnlyMemory<byte> Buffer { get; }

            public FromReadOnlyMemoryProducer(ReadOnlyMemory<byte> buffer)
                => Buffer = buffer;

            public ValueTask DisposeAsync()
                => default;

            public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
                => output.WriteAsync(Buffer, cancellationToken);
        }

        private sealed class DelayedStreamProducer : IStreamProducer
        {
            private Func<CancellationToken, ValueTask<IStreamProducer>> Factory { get; }

            public DelayedStreamProducer(Func<CancellationToken, ValueTask<IStreamProducer>> factory)
                => Factory = factory ?? throw new ArgumentNullException(nameof(factory));

            public ValueTask DisposeAsync()
                => default;

            public async ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
            {
                await using var producer = await Factory(cancellationToken);
                await producer.ProduceAsync(output, cancellationToken);
            }
        }

        public const int DefaultBufferSize = 16 * 1024;

        public static Encoding DefaultEncoding { get; } = new UTF8Encoding(false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamProducer Create(Func<Stream, CancellationToken, ValueTask> produce, Func<ValueTask>? dispose = default)
            => new InlineStreamProducer(produce, dispose);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamProducer Delay(Func<CancellationToken, ValueTask<IStreamProducer>> factory)
            => new DelayedStreamProducer(factory);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamProducer FromStream(Stream source, int copyBufferSize = DefaultBufferSize, bool leaveOpen = false)
            => new StreamCopyProducer(source, copyBufferSize, leaveOpen);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamProducer FromMemory(ReadOnlyMemory<byte> buffer)
            => new FromReadOnlyMemoryProducer(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamProducer FromArray(byte[] data, int copyBufferSize = DefaultBufferSize)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            return FromArray(data, 0, data.Length, copyBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamProducer FromArray(byte[] data, int index, int count, int copyBufferSize = DefaultBufferSize)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            return FromStream(new MemoryStream(data, index, count, false, true), copyBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamProducer FromString(string input, Encoding? encoding = default, int copyBufferSize = DefaultBufferSize)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            return new FromStringProducer(encoding ?? DefaultEncoding, input, copyBufferSize);
        }
    }
}