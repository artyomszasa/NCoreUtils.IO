using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class PipeStreamer
    {
        sealed class PipeWriterWrapper : PipeWriter
        {
            readonly Action _callback;

            public PipeWriter Writer { get; }

            int _callbackFired;

            public PipeWriterWrapper(PipeWriter writer, Action callback)
            {
                Writer = writer;
                _callback = callback;
            }

            void FireCallback()
            {
                if (0 == Interlocked.CompareExchange(ref _callbackFired, 1, 0))
                {
                    _callback();
                }
            }

            public override void Advance(int bytes)
                => Writer.Advance(bytes);

            // public override Stream AsStream(bool leaveOpen = false)
            //     => Writer.AsStream(leaveOpen);

            public override void CancelPendingFlush()
                => Writer.CancelPendingFlush();

            public override void Complete(Exception? exception = default)
            {
                if (exception is null)
                {
                    FireCallback();
                }
                Writer.Complete(exception);
            }

            public override ValueTask CompleteAsync(Exception? exception = default)
            {
                if (exception is null)
                {
                    FireCallback();
                }
                return Writer.CompleteAsync(exception);
            }

            public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
            {
                FireCallback();
                return Writer.FlushAsync(cancellationToken);
            }

            public override Memory<byte> GetMemory(int sizeHint = 0)
                => Writer.GetMemory(sizeHint);

            public override Span<byte> GetSpan(int sizeHint = 0)
                => Writer.GetSpan(sizeHint);

            public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default(CancellationToken))
            {
                FireCallback();
                return Writer.WriteAsync(source, cancellationToken);
            }
        }

        sealed class BoundConsumer<T> : IStreamConsumer
        {
            readonly IStreamConsumer<T> _consumer;

            readonly Action<T> _store;

            public BoundConsumer(IStreamConsumer<T> consumer, Action<T> store)
            {
                _consumer = consumer;
                _store = store;
            }

            public async ValueTask ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
                => _store(await _consumer.ConsumeAsync(input, cancellationToken));

            public ValueTask DisposeAsync()
                => _consumer.DisposeAsync();
        }

        sealed class ChainedProducer : IStreamProducer
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
                => new ValueTask(StreamAsync(
                    producer: _producer.ProduceAsync,
                    consumer: (input, cancellationToken) => _transformation.PerformAsync(input, output, cancellationToken),
                    cancellationToken: cancellationToken
                ));
        }

        sealed class ChainedTransformation : IStreamTransformation
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
                => new ValueTask(StreamAsync(
                    producer: (output, cancellationToken) => _first.PerformAsync(input, output, cancellationToken),
                    consumer: (input, cancellationToken) => _second.PerformAsync(input, output, cancellationToken),
                    cancellationToken: cancellationToken
                ));
        }

        [Flags]
        enum StreamerState
        {
            Idle = 0x00,
            ProducerStarted = 0x01,
            ConsumerStarted = 0x02,
            ProducerCompleted = 0x04,
            ConsumerCompleted = 0x08,
            Failed = 0x10
        }

        sealed class Streamer : IAsyncDisposable
        {
            // int _sync;
            readonly object _sync = new object();

            readonly PipeWriterWrapper _writer;

            readonly Func<Stream, CancellationToken, ValueTask> _producer;

            readonly Func<Stream, CancellationToken, ValueTask> _consumer;

            CancellationTokenSource? _cancellation;

            TaskCompletionSource<int>? _completion;

            bool ShouldCompleteReader
            {
                get
                {
                    // CasLock.Lock(ref _sync);
                    // try
                    lock (_sync)
                    {
                        return State.HasFlag(StreamerState.ConsumerStarted);
                    }
                    // finally
                    // {
                    //     CasLock.Release(ref _sync);
                    // }
                }
            }

            public PipeWriter Writer => _writer;

            public PipeReader Reader { get; }

            public Exception? Error { get; private set; }

            public Task? ConsumerTask { get; private set; }

            public Task? ProducerTask { get; private set; }

            public StreamerState State { get; private set; }

            public Streamer(Func<Stream, CancellationToken, ValueTask> producer, Func<Stream, CancellationToken, ValueTask> consumer)
            {
                _producer = producer;
                _consumer = consumer;
                var pipe = new Pipe();
                _writer = new PipeWriterWrapper(pipe.Writer, OnProducerStarted);
                Reader = pipe.Reader;
            }

            void UncheckedOnProducerOrConsumerCompleted()
            {
                if (State.HasFlag(StreamerState.ConsumerCompleted) && State.HasFlag(StreamerState.ProducerCompleted))
                {
                    _completion!.TrySetResult(0);
                }
            }

            void OnFailed()
            {
                // CasLock.Lock(ref _sync);
                // try
                lock (_sync)
                {
                    if (!State.HasFlag(StreamerState.Failed))
                    {
                        State |= StreamerState.Failed;
                        _cancellation!.Cancel();
                    }
                }
                // finally
                // {
                //     CasLock.Release(ref _sync);
                // }
            }

            void OnProducerStarted()
            {
                // CasLock.Lock(ref _sync);
                // try
                lock (_sync)
                {
                    if (!State.HasFlag(StreamerState.ConsumerStarted))
                    {
                        ConsumerTask = Consume(_cancellation!.Token);
                        State |= StreamerState.ConsumerStarted;
                    }
                }
                // finally
                // {
                //     CasLock.Release(ref _sync);
                // }
            }

            void OnConsumerCompleted()
            {
                // CasLock.Lock(ref _sync);
                // try
                lock (_sync)
                {
                    State |= StreamerState.ConsumerCompleted;
                    UncheckedOnProducerOrConsumerCompleted();
                }
                // finally
                // {
                //     CasLock.Release(ref _sync);
                // }
            }

            void OnProducerCompleted()
            {
                // CasLock.Lock(ref _sync);
                // try
                lock (_sync)
                {
                    State |= StreamerState.ProducerCompleted;
                    UncheckedOnProducerOrConsumerCompleted();
                }
                // finally
                // {
                //     CasLock.Release(ref _sync);
                // }
            }

            void TrySetCanceled(OperationCanceledException exn)
            {
                Error ??= exn;
                _completion!.TrySetCanceled();
                OnFailed();
            }

            void TrySetError(Exception exn)
            {
                Error ??= exn;
                _completion!.TrySetException(exn);
                OnFailed();
            }

            async Task Consume(CancellationToken cancellationToken)
            {
                try
                {
                    // force async --> task must be returned to the caller
                    await Task.Yield();
                    await _consumer(Reader.AsStream(false), cancellationToken);
                }
                catch (OperationCanceledException exn)
                {
                    TrySetCanceled(exn);
                }
                catch (Exception exn)
                {
                    TrySetError(exn);
                }
                finally
                {
                    // reader is ALWAYS completed!
                    await Reader.CompleteAsync(Error);
                    OnConsumerCompleted();
                }
            }

            async Task Produce(CancellationToken cancellationToken)
            {
                try
                {
                    // force async --> task must be returned to the caller
                    await Task.Yield();
                    await _producer(Writer.AsStream(false), cancellationToken).ConfigureAwait(false);
                    await Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException exn)
                {
                    TrySetCanceled(exn);
                }
                catch (Exception exn)
                {
                    TrySetError(exn);
                }
                finally
                {
                    // writer is ALWAYS completed!
                    await Writer.CompleteAsync(Error);
                    OnProducerCompleted();
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (ShouldCompleteReader)
                {
                    await Reader.CompleteAsync(Error);
                }
                _cancellation?.Dispose();

            }

            public Task Run(CancellationToken cancellationToken)
            {
                // CasLock.Lock(ref _sync);
                // try
                lock (_sync)
                {
                    if (!State.HasFlag(StreamerState.ProducerStarted))
                    {
                        _completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                        // _completion = new TaskCompletionSource<int>();
                        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        ProducerTask = Produce(_cancellation.Token);
                        State |= StreamerState.ProducerStarted;
                        return _completion.Task;
                    }
                    return _completion!.Task;
                }
                // finally
                // {
                //     CasLock.Release(ref _sync);
                // }
            }
        }

        public static async Task StreamAsync(
            Func<Stream, CancellationToken, ValueTask> producer,
            Func<Stream, CancellationToken, ValueTask> consumer,
            CancellationToken cancellationToken = default)
        {
            await using var streamer = new Streamer(producer, consumer);
            await streamer.Run(cancellationToken);
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
        public static async Task ConsumeAsync(this IStreamProducer producer, IStreamConsumer consumer, CancellationToken cancellationToken = default)
        {
            try
            {
                await StreamAsync(producer.ProduceAsync, consumer.ConsumeAsync, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await producer.ConfigureAwait(false).DisposeAsync();
            }
        }

        public static async Task<T> ConsumeAsync<T>(this IStreamProducer producer, IStreamConsumer<T> consumer, CancellationToken cancellationToken = default)
        {
            try
            {
                T result = default;
                await StreamAsync(producer.ProduceAsync, consumer.Bind(v => result = v).ConsumeAsync, cancellationToken).ConfigureAwait(false);
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

    }
}