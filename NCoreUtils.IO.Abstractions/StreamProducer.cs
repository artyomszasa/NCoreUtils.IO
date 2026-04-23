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

        public Stream Source { get; } = source.ThrowIfNull();

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
        private Func<Stream, CancellationToken, ValueTask> ProducerFun { get; } = produce.ThrowIfNull();

        private Func<ValueTask>? DisposeFun { get; } = dispose;

        public ValueTask DisposeAsync()
            => DisposeFun?.Invoke() ?? default;

        public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
            => ProducerFun(output, cancellationToken);
    }

    private sealed class FromStringProducer(Encoding encoding, string source, int bufferSize) : IStreamProducer
    {
        public string Source { get; } = source.ThrowIfNull();

        public Encoding Encoding { get; } = encoding.ThrowIfNull();

        public int BufferSize { get; } = bufferSize;

        public async ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        {
            using var writer = new StreamWriter(output, Encoding, BufferSize, true);
            await writer.WriteAsync(Source).ConfigureAwait(false);
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
        private Func<CancellationToken, ValueTask<IStreamProducer>> Factory { get; } = factory.ThrowIfNull();

        public ValueTask DisposeAsync()
            => default;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Az await using itt zajos.")]
        public async ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        {
            await using var producer = await Factory(cancellationToken).ConfigureAwait(false);
            await producer.ProduceAsync(output, cancellationToken).ConfigureAwait(false);
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
        data.ThrowIfNull();
        return FromArray(data, 0, data.Length, copyBufferSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamProducer FromArray(byte[] data, int index, int count, int copyBufferSize = DefaultBufferSize)
    {
        return FromStream(new MemoryStream(data.ThrowIfNull(), index, count, false, true), copyBufferSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamProducer FromString(string input, Encoding? encoding = default, int copyBufferSize = DefaultBufferSize)
        => new FromStringProducer(encoding ?? DefaultEncoding, input.ThrowIfNull(), copyBufferSize);
}