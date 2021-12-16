using System;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NCoreUtils.IO.Internal;

namespace NCoreUtils.IO
{
    public sealed class Streamer : IAsyncDisposable
    {
        private static Action<Streamer> ProductionStartedCallback { get; } = streamer =>
        {
            // do not start consumption if any errors have already been reported within production.
            if (streamer.ConsumerCancellationTokenSource.IsCancellationRequested)
            {
                return;
            }
            if (0 == Interlocked.CompareExchange(ref streamer._consumptionStarted, 1, 0))
            {
#pragma warning disable CA2012
                streamer.ConsumptionTask = streamer.ConsumeAsync(streamer.ConsumerCancellationTokenSource.Token);
#pragma warning restore CA2012
            }
        };

        private static Action<Streamer> WriterCompletionCallback { get; } = streamer =>
        {
            streamer.TryMarkWriterCompleted();
        };

        private int _disposed;

        private int _productionStarted;

        private int _consumptionStarted;

        private int _writerCompleted;

        private int _readerCompleted;

        private ValueTask? ConsumptionTask;

        private TaskCompletionSource<int>? ConsumerCompletionSource { get; set; }

        private CancellationTokenSource ConsumerCancellationTokenSource { get; }

        private IStreamProducer Producer { get; }

        private IStreamConsumer Consumer { get; }

        public PipeWriter Writer { get; }

        public PipeReader Reader { get; }

        public Streamer(IStreamProducer producer, IStreamConsumer consumer, CancellationToken cancellationToken)
        {
            var options = new PipeOptions(minimumSegmentSize: 8 * 1024);
            var triggerSize = Math.Min(options.Pool.MaxBufferSize, options.MinimumSegmentSize);
            var pipe = new Pipe(options);
            ConsumerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Producer = producer ?? throw new ArgumentNullException(nameof(producer));
            Consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            Writer = new PipeWriterWrapper<Streamer>(pipe.Writer, triggerSize, ProductionStartedCallback, WriterCompletionCallback, this);
            Reader = pipe.Reader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryMarkWriterCompleted()
        {
            if (0 == Interlocked.CompareExchange(ref _writerCompleted, 1, 0))
            {
                return true;
            }
            return false;
        }

        private async ValueTask ConsumeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Consumer.ConsumeAsync(Reader.AsStream(true), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // pass cancellation to completion source
                ConsumerCompletionSource?.TrySetCanceled(CancellationToken.None);
                // pass exception to ValueTask
                throw;
            }
            catch (Exception exn)
            {
                if (0 == Interlocked.CompareExchange(ref _readerCompleted, 1, 0))
                {
                    await Reader.CompleteAsync(exn).ConfigureAwait(false);
                }
                // pass exception to completion source
                ConsumerCompletionSource?.TrySetException(exn);
                // pass exception to ValueTask
                throw;
            }
            finally
            {
                if (0 == Interlocked.CompareExchange(ref _readerCompleted, 1, 0))
                {
                    await Reader.CompleteAsync(default).ConfigureAwait(false);
                }
                // notify completion source if any
                ConsumerCompletionSource?.TrySetResult(default);
            }
        }

        private async ValueTask Produce(CancellationToken cancellationToken)
        {
            try
            {
                await Producer.ProduceAsync(Writer.AsStream(true), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // cancel whole operation if not cancelled already
                if (!ConsumerCancellationTokenSource.IsCancellationRequested)
                {
                    ConsumerCancellationTokenSource.Cancel();
                }
                // if consumption has not been started but the completion source has been created --> set as cancelled
                if (0 == Interlocked.CompareExchange(ref _consumptionStarted, 0, 0))
                {
                    ConsumerCompletionSource?.TrySetCanceled(CancellationToken.None);
                    throw; // sync case
                }
            }
            catch (Exception exn)
            {
                // complete writer with exception
                if (TryMarkWriterCompleted())
                {
                    await Writer.CompleteAsync(exn).ConfigureAwait(false);
                }
                // if consumption has not been started but the completion source has been created --> set as failed
                if (0 == Interlocked.CompareExchange(ref _consumptionStarted, 0, 0))
                {
                    ConsumerCompletionSource?.TrySetException(exn);
                    throw; // sync case
                }
                // cancel whole operation i.e. cancel consumption if it has been started
                // ConsumerCancellationTokenSource.Cancel();
            }
            finally
            {
                if (TryMarkWriterCompleted())
                {
                    await Writer.CompleteAsync(default).ConfigureAwait(false);
                }
            }
        }

        private ValueTask GetCompletionTask()
        {
            // if consumption task has been initialized --> return it
            if (ConsumptionTask.HasValue)
            {
                return ConsumptionTask.Value;
            }
            // otherwise fallback to async completion
            ConsumerCompletionSource ??= new TaskCompletionSource<int>();
            return new ValueTask(ConsumerCompletionSource.Task);
        }

        public ValueTask RunAsync()
        {
            if (0 == Interlocked.CompareExchange(ref _productionStarted, 1, 0))
            {
                // start production
                var productionTask = Produce(ConsumerCancellationTokenSource.Token);
                if (productionTask.IsCompleted)
                {
                    if (productionTask.IsCompletedSuccessfully)
                    {
                        // consumer must have started --> consumptionTask must have been initialized
                        return ConsumptionTask!.Value;
                    }
                    return productionTask;
                }
            }
            return GetCompletionTask();
        }

        #region disposable

        public async ValueTask DisposeAsync()
        {
            if (0 == Interlocked.CompareExchange(ref _disposed, 1, 0))
            {
                // of completion source has been created try send cancellation
                ConsumerCompletionSource?.TrySetCanceled();
                // cancel operation if still relevant
                if (!ConsumerCancellationTokenSource.IsCancellationRequested)
                {
                    ConsumerCancellationTokenSource.Cancel();
                }
                // await task is it has been created --> it may throw OperationCanceledException but this is intended
                if (ConsumptionTask.HasValue && !ConsumptionTask.Value.IsCompleted)
                {
                    await Task.WhenAny(ConsumptionTask.Value.AsTask()).ConfigureAwait(false);
                }
                // dispose resources
                await Producer.DisposeAsync().ConfigureAwait(false);
                await Consumer.DisposeAsync().ConfigureAwait(false);
                ConsumerCancellationTokenSource.Dispose();
            }
        }

        #endregion
    }
}