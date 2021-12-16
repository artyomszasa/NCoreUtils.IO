using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO.Internal
{
    public sealed class PipeWriterWrapper<T> : PipeWriter
    {
        private int _callbackFired;

        private int WrittenBeforeTrigger { get; set; }

        private int TriggerSize { get; }

        private Action<T> Callback { get; }

        private T State { get; }

        private Action<T> OnComplete { get; }

        public PipeWriter Writer { get; }

        public PipeWriterWrapper(PipeWriter writer, int triggerSize, Action<T> callback, Action<T> onComplete, T state)
        {
            Writer = writer;
            TriggerSize = triggerSize;
            Callback = callback;
            OnComplete = onComplete;
            State = state;
        }

        private void FireCallback()
        {
            if (0 == Interlocked.CompareExchange(ref _callbackFired, 1, 0))
            {
                Callback(State);
            }
        }

        public override void Advance(int bytes)
            => Writer.Advance(bytes);

        public override void CancelPendingFlush()
            => Writer.CancelPendingFlush();

        public override void Complete(Exception? exception = default)
        {
            Writer.Complete(exception);
            if (exception is null)
            {
                FireCallback();
            }
            OnComplete(State);
        }

        public override async ValueTask CompleteAsync(Exception? exception = default)
        {
            await Writer.CompleteAsync(exception);
            if (exception is null)
            {
                FireCallback();
            }
            OnComplete(State);
        }

        public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            var flushResult = await Writer.FlushAsync(cancellationToken);
            FireCallback();
            return flushResult;
        }

        public override Memory<byte> GetMemory(int sizeHint = 0)
            => Writer.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0)
            => Writer.GetSpan(sizeHint);

        public override async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            if (0 == Interlocked.CompareExchange(ref _callbackFired, 0, 0))
            {
                FlushResult flushResult;
                if (TriggerSize >= source.Length + WrittenBeforeTrigger)
                {
                    flushResult = await Writer.WriteAsync(source, cancellationToken);
                    WrittenBeforeTrigger += source.Length;
                }
                else
                {
#if NET6_0_OR_GREATER
                    await Writer.WriteAsync(source[..TriggerSize], cancellationToken);
                    FireCallback();
                    flushResult = await Writer.WriteAsync(source[TriggerSize..], cancellationToken);
#else
                    await Writer.WriteAsync(source.Slice(0, TriggerSize), cancellationToken);
                    FireCallback();
                    flushResult = await Writer.WriteAsync(source.Slice(TriggerSize), cancellationToken);
#endif
                }
                return flushResult;
            }
            return await Writer.WriteAsync(source, cancellationToken);
        }
    }
}