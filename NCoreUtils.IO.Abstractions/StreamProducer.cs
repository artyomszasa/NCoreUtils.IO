using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public static class StreamProducer
{
    private sealed class StreamCopyProducer(Stream source, int bufferSize, bool leaveOpen) : IStreamProducer
    {
        public bool LeaveOpen { get; } = leaveOpen;

        public int BufferSize { get; } = bufferSize;

        public Stream Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

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

    private sealed class InlineStreamProducer(Func<Stream, CancellationToken, ValueTask> produce, Func<ValueTask>? dispose) : IStreamProducer
    {
        private Func<Stream, CancellationToken, ValueTask> ProducerFun { get; } = produce ?? throw new ArgumentNullException(nameof(produce));

        private Func<ValueTask>? DisposeFun { get; } = dispose;

        public ValueTask DisposeAsync()
            => DisposeFun?.Invoke() ?? default;

        public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
            => ProducerFun(output, cancellationToken);
    }

    private sealed class FromStringProducer(Encoding encoding, string source, int bufferSize) : IStreamProducer
    {
        public string Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

        public Encoding Encoding { get; } = encoding ?? throw new ArgumentNullException(nameof(encoding));

        public int BufferSize { get; } = bufferSize;

        public async ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        {
            using var writer = new StreamWriter(output, Encoding, BufferSize, true);
            await writer.WriteAsync(Source);
        }

        public ValueTask DisposeAsync()
            => default;
    }

    private sealed class FromReadOnlyMemoryProducer(ReadOnlyMemory<byte> buffer) : IStreamProducer
    {
        private ReadOnlyMemory<byte> Buffer { get; } = buffer;

        public ValueTask DisposeAsync()
            => default;

        public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
            => output.WriteAsync(Buffer, cancellationToken);
    }

    private sealed class DelayedStreamProducer(Func<CancellationToken, ValueTask<IStreamProducer>> factory) : IStreamProducer
    {
        private Func<CancellationToken, ValueTask<IStreamProducer>> Factory { get; } = factory ?? throw new ArgumentNullException(nameof(factory));

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