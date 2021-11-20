using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class PipeStreamer
    {
        private sealed class Box<T>
        {
            public T Value { get; set; } = default!;
        }

        private sealed class BoundConsumer<T> : IStreamConsumer
        {
            readonly IStreamConsumer<T> _consumer;

            readonly Action<T> _store;

            public BoundConsumer(IStreamConsumer<T> consumer, Action<T> store)
            {
                _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
                _store = store ?? throw new ArgumentNullException(nameof(store));
            }

            public async ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
                => _store(await _consumer.ConsumeAsync(input, cancellationToken));

            public ValueTask DisposeAsync()
                => _consumer.DisposeAsync();
        }

        private sealed class ChainedProducer : IStreamProducer
        {
            readonly IStreamProducer _producer;

            readonly IStreamTransformation _transformation;

            public ChainedProducer(IStreamProducer producer, IStreamTransformation transformation)
            {
                _producer = producer ?? throw new ArgumentNullException(nameof(producer));
                _transformation = transformation ?? throw new ArgumentNullException(nameof(transformation));
            }

            public async ValueTask DisposeAsync()
            {
                await _producer.ConfigureAwait(false).DisposeAsync();
                await _transformation.ConfigureAwait(false).DisposeAsync();
            }

            public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
                => StreamAsync(
                    producer: _producer,
                    consumer: StreamConsumer.Create((input, cancellationToken) => _transformation.PerformAsync(input, output, cancellationToken)),
                    cancellationToken: cancellationToken
                );
        }

        private sealed class ChainedTransformation : IStreamTransformation
        {
            readonly IStreamTransformation _first;

            readonly IStreamTransformation _second;

            public ChainedTransformation(IStreamTransformation first, IStreamTransformation second)
            {
                _first = first ?? throw new ArgumentNullException(nameof(first));
                _second = second ?? throw new ArgumentNullException(nameof(second));
            }

            public async ValueTask DisposeAsync()
            {
                await _first.ConfigureAwait(false).DisposeAsync();
                await _second.ConfigureAwait(false).DisposeAsync();
            }

            public ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
                => StreamAsync(
                    producer: StreamProducer.Create((output, cancellationToken) => _first.PerformAsync(input, output, cancellationToken)),
                    consumer: StreamConsumer.Create((input, cancellationToken) => _second.PerformAsync(input, output, cancellationToken)),
                    cancellationToken: cancellationToken
                );
        }

        private sealed class ChainedConsumer : IStreamConsumer
        {
            readonly IStreamTransformation _transformation;

            readonly IStreamConsumer _consumer;

            public ChainedConsumer(IStreamTransformation transformation, IStreamConsumer consumer)
            {
                _transformation = transformation ?? throw new ArgumentNullException(nameof(transformation));
                _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            }

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

        private sealed class ChainedConsumer<T> : IStreamConsumer<T>
        {
            readonly IStreamTransformation _transformation;

            readonly IStreamConsumer<T> _consumer;

            public ChainedConsumer(IStreamTransformation transformation, IStreamConsumer<T> consumer)
            {
                _transformation = transformation ?? throw new ArgumentNullException(nameof(transformation));
                _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            }

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
            await using var streamer = new Streamer(producer, consumer, cancellationToken);
            await streamer.RunAsync();
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
            try
            {
                await StreamAsync(producer, consumer, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await producer.ConfigureAwait(false).DisposeAsync();
            }
        }

        public static async ValueTask<T> ConsumeAsync<T>(this IStreamProducer producer, IStreamConsumer<T> consumer, CancellationToken cancellationToken = default)
        {
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
}