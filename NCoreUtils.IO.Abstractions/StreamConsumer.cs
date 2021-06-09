using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class StreamConsumer
    {
        sealed class StreamCopyConsumer : IStreamConsumer
        {
            readonly int _bufferSize;

            readonly bool _leaveOpen;

            public Stream Target { get; }

            public StreamCopyConsumer(Stream target, int bufferSize, bool leaveOpen = false)
            {
                Target = target ?? throw new ArgumentNullException(nameof(target));
                _bufferSize = bufferSize;
                _leaveOpen = leaveOpen;
            }

            public async ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            {
                await input.CopyToAsync(Target, _bufferSize, cancellationToken);
                await Target.FlushAsync(CancellationToken.None);
            }

            public ValueTask DisposeAsync()
            {
                #if NETSTANDARD2_1
                return Target.DisposeAsync();
                #else
                Target.Dispose();
                return default;
                #endif
            }
        }

        sealed class StreamToArrayConsumer : IStreamConsumer<byte[]>
        {
            readonly int _bufferSize;

            public StreamToArrayConsumer(int bufferSize)
                => _bufferSize = bufferSize;

            public async ValueTask<byte[]> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            {
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, _bufferSize, cancellationToken);
                return buffer.ToArray();
            }

            public ValueTask DisposeAsync() => default;
        }

        sealed class StreamToStringConsumer : IStreamConsumer<string>
        {
            readonly int _bufferSize;

            readonly Encoding _encoding;

            public StreamToStringConsumer(Encoding encoding, int bufferSize)
            {
                _encoding = encoding;
                _bufferSize = bufferSize;
            }

            public async ValueTask<string> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            {
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, _bufferSize, cancellationToken);
                return _encoding.GetString(buffer.ToArray());
            }

            public ValueTask DisposeAsync() => default;
        }

        sealed class InlineStreamConsumer : IStreamConsumer
        {
            readonly Func<Stream, CancellationToken, ValueTask> _consume;

            readonly Func<ValueTask>? _dispose;

            public InlineStreamConsumer(Func<Stream, CancellationToken, ValueTask> consume, Func<ValueTask>? dispose)
            {
                _consume = consume ?? throw new ArgumentNullException(nameof(consume));
                _dispose = dispose;
            }

            public ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
                => _consume(input, cancellationToken);

            public ValueTask DisposeAsync()
                => _dispose?.Invoke() ?? default;
        }

        sealed class InlineStreamConsumer<T> : IStreamConsumer<T>
        {
            readonly Func<Stream, CancellationToken, ValueTask<T>> _consume;

            readonly Func<ValueTask>? _dispose;

            public InlineStreamConsumer(Func<Stream, CancellationToken, ValueTask<T>> consume, Func<ValueTask>? dispose)
            {
                _consume = consume ?? throw new ArgumentNullException(nameof(consume));
                _dispose = dispose;
            }

            public ValueTask<T> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
                => _consume(input, cancellationToken);

            public ValueTask DisposeAsync()
                => _dispose?.Invoke() ?? default;
        }

        sealed class DelayedStreamConsumer : IStreamConsumer
        {
            readonly Func<CancellationToken, ValueTask<IStreamConsumer>> _factory;

            IStreamConsumer? _consumer;

            public DelayedStreamConsumer(Func<CancellationToken, ValueTask<IStreamConsumer>> factory)
            {
                _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            public async ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            {
                _consumer = await _factory(cancellationToken);
                await _consumer.ConsumeAsync(input, cancellationToken);
            }

            public ValueTask DisposeAsync()
                => _consumer?.DisposeAsync() ?? default;
        }

        public const int DefaultBufferSize = 16 * 1024;

        public static Encoding DefaultEncoding { get; } = new UTF8Encoding(false);

        public static IStreamConsumer Create(Func<Stream, CancellationToken, ValueTask> consume, Func<ValueTask>? dispose = default)
            => new InlineStreamConsumer(consume, dispose);

        public static IStreamConsumer<T> Create<T>(Func<Stream, CancellationToken, ValueTask<T>> consume, Func<ValueTask>? dispose = default)
            => new InlineStreamConsumer<T>(consume, dispose);

        public static IStreamConsumer Delay(Func<CancellationToken, ValueTask<IStreamConsumer>> factory)
            => new DelayedStreamConsumer(factory);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamConsumer ToStream(Stream target, int copyBufferSize, bool leaveOpen = false)
            => new StreamCopyConsumer(target, copyBufferSize, leaveOpen);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamConsumer ToStream(Stream target, int copyBufferSize = DefaultBufferSize)
            => ToStream(target, copyBufferSize, false);

        public static IStreamConsumer<byte[]> ToArray(int copyBufferSize = DefaultBufferSize)
            => new StreamToArrayConsumer(copyBufferSize);

        public static IStreamConsumer<string> ToString(Encoding? encoding = default, int copyBufferSize = DefaultBufferSize)
            => new StreamToStringConsumer(encoding ?? DefaultEncoding, copyBufferSize);
    }
}