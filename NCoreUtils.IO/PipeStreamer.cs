using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public static class PipeStreamer
{
    private sealed class Box<T>
    {
        public T Value { get; set; } = default!;
    }

    private sealed class BoundConsumer<T>(IStreamConsumer<T> consumer, Action<T> store) : IStreamConsumer
    {
        private readonly IStreamConsumer<T> _consumer = consumer.ThrowIfNull();

        private readonly Action<T> _store = store.ThrowIfNull();

        public async ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            => _store(await _consumer.ConsumeAsync(input, cancellationToken).ConfigureAwait(false));

        public ValueTask DisposeAsync()
            => _consumer.DisposeAsync();
    }

    private sealed class ChainedProducer(IStreamProducer producer, IStreamTransformation transformation) : IStreamProducer
    {
        private readonly IStreamProducer _producer = producer.ThrowIfNull();

        private readonly IStreamTransformation _transformation = transformation.ThrowIfNull();

        public async ValueTask DisposeAsync()
        {
            await _producer.ConfigureAwait(false).DisposeAsync();
            await _transformation.ConfigureAwait(false).DisposeAsync();
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The StreamAsync handles disposal")]
        public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
            => StreamAsync(
                producer: _producer,
                consumer: StreamConsumer.Create((input, cancellationToken) => _transformation.PerformAsync(input, output, cancellationToken)),
                cancellationToken: cancellationToken
            );
    }

    private sealed class ChainedTransformation(IStreamTransformation first, IStreamTransformation second) : IStreamTransformation
    {
        private readonly IStreamTransformation _first = first.ThrowIfNull();

        private readonly IStreamTransformation _second = second.ThrowIfNull();

        public async ValueTask DisposeAsync()
        {
            await _first.ConfigureAwait(false).DisposeAsync();
            await _second.ConfigureAwait(false).DisposeAsync();
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "StreamAsync handles disposal")]
        public ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
            => StreamAsync(
                producer: StreamProducer.Create((output, cancellationToken) => _first.PerformAsync(input, output, cancellationToken)),
                consumer: StreamConsumer.Create((input, cancellationToken) => _second.PerformAsync(input, output, cancellationToken)),
                cancellationToken: cancellationToken
            );
    }

    private sealed class ChainedConsumer(IStreamTransformation transformation, IStreamConsumer consumer) : IStreamConsumer
    {
        private readonly IStreamTransformation _transformation = transformation.ThrowIfNull();

        private readonly IStreamConsumer _consumer = consumer.ThrowIfNull();

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "StreamAsync handles disposal")]
        public ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            => StreamAsync(
                producer: StreamProducer.Create((output, cancellationToken) => _transformation.PerformAsync(input, output, cancellationToken)),
                consumer: _consumer,
                cancellationToken: cancellationToken
            );

        public async ValueTask DisposeAsync()
        {
            await _transformation.ConfigureAwait(false).DisposeAsync();
            await _consumer.ConfigureAwait(false).DisposeAsync();
        }
    }

    private sealed class ChainedConsumer<T>(IStreamTransformation transformation, IStreamConsumer<T> consumer) : IStreamConsumer<T>
    {
        private readonly IStreamTransformation _transformation = transformation.ThrowIfNull();

        private readonly IStreamConsumer<T> _consumer = consumer.ThrowIfNull();

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "StreamAsync handles disposal")]
        public ValueTask<T> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
        {
            var result = new Box<T>();
            var consumer = _consumer.Bind(value => result.Value = value);
            var task = StreamAsync(
                producer: StreamProducer.Create((output, cancellationToken) => _transformation.PerformAsync(input, output, cancellationToken)),
                consumer: consumer,
                cancellationToken: cancellationToken
            );
            if (task.IsCompletedSuccessfully)
            {
                return new ValueTask<T>(result.Value);
            }
            return FinishConsumeAsync(task, result);

            static async ValueTask<T> FinishConsumeAsync(ValueTask task, Box<T> result)
            {
                await task.ConfigureAwait(false);
                return result.Value;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _transformation.ConfigureAwait(false).DisposeAsync();
            await _consumer.ConfigureAwait(false).DisposeAsync();
        }
    }

    public static async ValueTask StreamAsync(
        IStreamProducer producer,
        IStreamConsumer consumer,
        CancellationToken cancellationToken = default)
    {
        var streamer = new Streamer(producer.ThrowIfNull(), consumer.ThrowIfNull(), cancellationToken);
        await using (streamer.ConfigureAwait(false))
        {
            await streamer.RunAsync().ConfigureAwait(false);
        }
    }

    public static IStreamConsumer Bind<T>(this IStreamConsumer<T> consumer, Action<T> store)
        => new BoundConsumer<T>(consumer, store);

    /// <summary>
    /// Consumes specified producer instance by pipeing its output to the specified consumer. Both producer and
    /// consumer are disposed if operation has completed, failed or has been cancelled.
    /// </summary>
    /// <param name="consumer">Consumer.</param>
    /// <param name="producer">Producer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask ConsumeAsync(this IStreamProducer producer, IStreamConsumer consumer, CancellationToken cancellationToken = default)
    {
        producer.ThrowIfNull();
        consumer.ThrowIfNull();
        try
        {
            await StreamAsync(producer, consumer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await producer.ConfigureAwait(false).DisposeAsync();
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "StreamAsync handles disposal")]
    public static async ValueTask<T> ConsumeAsync<T>(this IStreamProducer producer, IStreamConsumer<T> consumer, CancellationToken cancellationToken = default)
    {
        producer.ThrowIfNull();
        consumer.ThrowIfNull();
        try
        {
            T result = default!;
            await StreamAsync(producer, consumer.Bind(v => result = v), cancellationToken).ConfigureAwait(false);
            return result!;
        }
        finally
        {
            await producer.ConfigureAwait(false).DisposeAsync();
        }
    }

    public static IStreamProducer Chain(this IStreamProducer producer, IStreamTransformation transformation)
        => new ChainedProducer(producer, transformation);

    public static IStreamTransformation Chain(this IStreamTransformation first, IStreamTransformation second)
        => new ChainedTransformation(first, second);

    public static IStreamConsumer Chain(this IStreamConsumer consumer, IStreamTransformation transformation)
        => new ChainedConsumer(transformation, consumer);

    public static IStreamConsumer<T> Chain<T>(this IStreamConsumer<T> consumer, IStreamTransformation transformation)
        => new ChainedConsumer<T>(transformation, consumer);
}