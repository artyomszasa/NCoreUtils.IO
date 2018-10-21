namespace NCoreUtils.IO

open System
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// Constains methods to create asynchronous stream transformations.
[<Sealed; AbstractClass>]
type StreamTransformation =

  /// <summary>
  /// Initializes asynchronous stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream transformation.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (transformation, [<Optional; DefaultParameterValue(null:Action<bool>)>] dispose) =
    AsyncStreamTransformation.From (transformation, dispose) :> IStreamTransformation

  /// <summary>
  /// Initializes asynchronous stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream transformation.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member Of (transformation : Stream -> Stream -> Async<unit>, ?dispose : unit -> unit) =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          if isNull input  then nullArg "input"
          if isNull output then nullArg "output"
          transformation input output
        member __.Dispose () =
          match dispose with
          | None -> ()
          | Some dispose -> dispose ()
    }

/// Constains methods to create asynchronous stream consumers.
[<Sealed; AbstractClass>]
type StreamConsumer =

  /// <summary>
  /// Initializes asynchronous stream consumer from the specified parameters.
  /// </summary>
  /// <param name="consume">Consume function to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (consume, [<Optional; DefaultParameterValue(null:Action<bool>)>] dispose) =
    AsyncStreamConsumer.From (consume, dispose) :> IStreamConsumer

  /// <summary>
  /// Initializes asynchronous stream consumer from the specified parameters.
  /// </summary>
  /// <param name="consume">Consume function to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member Of (consume : Stream -> Async<unit>, ?dispose : unit -> unit) =
    { new IStreamConsumer with
        member __.AsyncConsume source =
          if isNull source then nullArg "source"
          consume source
        member __.Dispose () =
          match dispose with
          | None -> ()
          | Some dispose -> dispose ()
    }

/// Provides helper functions to create dependent asynchronous dependent stream transformations.
[<Sealed; AbstractClass>]
type DependentStreamTransformation =
  /// <summary>
  /// Initializes asynchronous dependent stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous dependent stream transformation.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (transformation, [<Optional; DefaultParameterValue(null: Action<bool>)>] dispose) =
    AsyncDependentStreamTransformation.From (transformation, dispose) :> IDependentStreamTransformation<'TState>

  /// <summary>
  /// Initializes asynchronous dependent stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous dependent stream transformation.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member Of (transformation, ?dispose) =
    { new IDependentStreamTransformation<'TState> with
        member __.AsyncPerform (input, dependentOutput) =
          if isNull input then nullArg "input"
          transformation input dependentOutput
        member __.Dispose () =
          match dispose with
          | None -> ()
          | Some dispose -> dispose ()
    }

/// Provides extensions for asynchronous stream transformations.
[<Extension>]
[<Sealed; AbstractClass>]
type StreamTransformationExtensions =

  /// <summary>
  /// Performs the transformation defined by the actual instance.
  /// </summary>
  /// <param name="transformation">Asynchronous stream transformation.</param>
  /// <param name="source">Input stream.</param>
  /// <param name="target">Output stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member PerformAsync (transformation : IStreamTransformation, source, target, [<Optional>] cancellationToken) =
    match transformation with
    | :? AsyncStreamTransformation as inst -> inst.PerformDirect (source, target, cancellationToken)
    | _ ->
      Async.StartAsTask (transformation.AsyncPerform (source, target), cancellationToken = cancellationToken) :> _

/// Provides extensions for asynchronous dependent stream transformations.
[<Extension>]
[<Sealed; AbstractClass>]
type DependentStreamTransformationExtensions =

  /// <summary>
  /// Performs the transformation defined by the actual instance.
  /// </summary>
  /// <param name="transformation">Asynchronous dependent stream transformation.</param>
  /// <param name="source">Input stream.</param>
  /// <param name="dependentTarget">Function to populate output stream depending on the intermediate state.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member PerformAsync (transformation : IDependentStreamTransformation<'TState>, source, dependentTarget, [<Optional>] cancellationToken) =
    match transformation with
    | :? AsyncDependentStreamTransformation<'TState> as inst ->
      inst.PerformDirect (source, dependentTarget, cancellationToken)
    | _ ->
      Async.StartAsTask (
        transformation.AsyncPerform (source, dependentTarget.Invoke),
        cancellationToken = cancellationToken)
      :> _

/// Provides extensions for asynchronous stream consumers.
[<Extension>]
[<Sealed; AbstractClass>]
type StreamConsumerExtensions =

  /// <summary>
  /// Consumes the input stream.
  /// </summary>
  /// <param name="consumer">Asynchronous dependent stream transformation.</param>
  /// <param name="source">Input stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member ConsumeAsync (consumer : IStreamConsumer, source, [<Optional>] cancellationToken) =
    match consumer with
    | :? AsyncStreamConsumer as inst -> inst.ConsumeDirect (source, cancellationToken)
    | _ ->
      Async.StartAsTask (consumer.AsyncConsume source, cancellationToken = cancellationToken) :> _