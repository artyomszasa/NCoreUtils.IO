using System;

namespace NCoreUtils.IO
{
    public interface ITraceStreamTransformation : IStreamTransformation
    {
        bool HasStarted { get; }

        bool HasCompleted { get; }

        Exception? Error { get; }

        bool HasBeenDisposed { get; }
    }
}