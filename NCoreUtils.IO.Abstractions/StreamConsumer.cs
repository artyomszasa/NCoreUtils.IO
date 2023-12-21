using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public static class StreamConsumer
{
    private sealed class StreamCopyConsumer(Stream target, int bufferSize, bool leaveOpen = false) : IStreamConsumer
    {
        public int BufferSize { get; } = bufferSize;

        public bool LeaveOpen { get; } = leaveOpen;

        public Stream Target { get; } = target ?? throw new ArgumentNullException(nameof(target));

        public async ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
        {
            await input.CopyToAsync(Target, BufferSize, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (!LeaveOpen)
            {
#if NETFRAMEWORK
                Target.Dispose();
                return default;
#else
                return Target.DisposeAsync();
#endif
            }
            return default;
        }
    }

    private sealed class StreamToArrayConsumer(int bufferSize) : IStreamConsumer<byte[]>
    {
        public int BufferSize { get; } = bufferSize;

        public async ValueTask<byte[]> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, BufferSize, cancellationToken);
            return buffer.ToArray();
        }

        public ValueTask DisposeAsync() => default;
    }

    private sealed class StreamToStringConsumer(Encoding encoding, int bufferSize) : IStreamConsumer<string>
    {
        public int BufferSize { get; } = bufferSize;

        public Encoding Encoding { get; } = encoding ?? throw new ArgumentNullException(nameof(encoding));

        public async ValueTask<string> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(input, Encoding, false, BufferSize, true);
            return await reader.ReadToEndAsync(
#if NET7_0_OR_GREATER
                cancellationToken
#endif
            );
        }

        public ValueTask DisposeAsync() => default;
    }

    private sealed class InlineStreamConsumer(Func<Stream, CancellationToken, ValueTask> consume, Func<ValueTask>? dispose) : IStreamConsumer
    {
        private Func<Stream, CancellationToken, ValueTask> ConsumerFun { get; } = consume ?? throw new ArgumentNullException(nameof(consume));

        private Func<ValueTask>? DisposeFun { get; } = dispose;

        public ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            => ConsumerFun(input, cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (DisposeFun is not null)
            {
                return DisposeFun.Invoke();
            }
            return default;
        }
    }

    private sealed class InlineStreamConsumer<T>(Func<Stream, CancellationToken, ValueTask<T>> consume, Func<ValueTask>? dispose) : IStreamConsumer<T>
    {
        private Func<Stream, CancellationToken, ValueTask<T>> ConsumerFun { get; } = consume ?? throw new ArgumentNullException(nameof(consume));

        private Func<ValueTask>? DisposeFun { get; } = dispose;

        public ValueTask<T> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            => ConsumerFun(input, cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (DisposeFun is not null)
            {
                return DisposeFun.Invoke();
            }
            return default;
        }
    }

    private sealed class DelayedStreamConsumer(Func<CancellationToken, ValueTask<IStreamConsumer>> factory) : IStreamConsumer
    {
        private Func<CancellationToken, ValueTask<IStreamConsumer>> Factory { get; } = factory ?? throw new ArgumentNullException(nameof(factory));

        private IStreamConsumer? Consumer { get; set; }

        public async ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
        {
            Consumer = await Factory(cancellationToken);
            await Consumer.ConsumeAsync(input, cancellationToken);
        }

        public ValueTask DisposeAsync()
            => Consumer?.DisposeAsync() ?? default;
    }

    public const int DefaultBufferSize = 16 * 1024;

    public static Encoding DefaultEncoding { get; } = new UTF8Encoding(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamConsumer Create(Func<Stream, CancellationToken, ValueTask> consume, Func<ValueTask>? dispose = default)
        => new InlineStreamConsumer(consume, dispose);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamConsumer<T> Create<T>(Func<Stream, CancellationToken, ValueTask<T>> consume, Func<ValueTask>? dispose = default)
        => new InlineStreamConsumer<T>(consume, dispose);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamConsumer Delay(Func<CancellationToken, ValueTask<IStreamConsumer>> factory)
        => new DelayedStreamConsumer(factory);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamConsumer ToStream(Stream target, int copyBufferSize, bool leaveOpen = false)
        => new StreamCopyConsumer(target, copyBufferSize, leaveOpen);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamConsumer ToStream(Stream target, int copyBufferSize = DefaultBufferSize)
        => ToStream(target, copyBufferSize, false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamConsumer<byte[]> ToArray(int copyBufferSize = DefaultBufferSize)
        => new StreamToArrayConsumer(copyBufferSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IStreamConsumer<string> ToString(Encoding? encoding = default, int copyBufferSize = DefaultBufferSize)
        => new StreamToStringConsumer(encoding ?? DefaultEncoding, copyBufferSize);
}