namespace NCoreUtils.IO

open System
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading.Tasks
open NCoreUtils

/// Constains methods to create asynchronous stream transformations.
[<Sealed; AbstractClass>]
type StreamTransformation =

  /// <summary>
  /// Initializes asynchronous stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream transformation.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (transformation, [<Optional; DefaultParameterValue(null:Action<bool>)>] dispose) =
    AsyncStreamTransformation.From (transformation, dispose) :> IStreamTransformation

  /// <summary>
  /// Initializes asynchronous stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
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
  /// <param name="dispose">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (consume, [<Optional; DefaultParameterValue(null:Action<bool>)>] dispose) =
    if isNull consume then nullArg "consume"
    AsyncStreamConsumer.From (consume, dispose) :> IStreamConsumer

  /// <summary>
  /// Initializes asynchronous stream consumer from the specified parameters.
  /// </summary>
  /// <param name="consume">Consume function to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member Of (consume : Stream -> Async<unit>, ?dispose : unit -> unit) =
    { new IStreamConsumer with
        member __.AsyncConsume source =
          if isNull source then nullArg "source"
          consume source
        member __.Dispose () =
          Option.iter invoke dispose
    }

/// Constains methods to create asynchronous stream consumers.
[<Sealed; AbstractClass>]
type StreamToResultConsumer =

  /// <summary>
  /// Initializes asynchronous stream consumer from the specified parameters.
  /// </summary>
  /// <param name="consume">Consume function to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (consume : Func<_, _, Task<'T>>, [<Optional; DefaultParameterValue(null:Action<bool>)>] dispose) =
    if isNull consume then nullArg "consume"
    AsyncStreamConsumer<'T>.From (consume, dispose) :> IStreamConsumer<'T>


  /// <summary>
  /// Initializes asynchronous stream consumer from the specified parameters.
  /// </summary>
  /// <param name="consume">Consume function to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member Of (consume : Stream -> Async<'T>, ?dispose : unit -> unit) =
    { new IStreamConsumer<'T> with
        member __.AsyncConsume source =
          if isNull source then nullArg "source"
          consume source
        member __.Dispose () =
          Option.iter invoke dispose
    }

  /// <summary>
  /// Binds result of the source consumer to the reference cell producing resultless consumer.
  /// </summary>
  /// <param name="ref">Reference cell to bind to.</param>
  /// <param name="consumer">Source consumer.</param>
  /// <returns>Resultless stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member BindTo (ref : _ ref) (consumer : IStreamConsumer<_>) =
    { new IStreamConsumer with
        member __.AsyncConsume source = async {
          let! result = consumer.AsyncConsume source
          ref := result }
        member __.Dispose () = consumer.Dispose ()
    }

/// Provides helper functions to create dependent asynchronous dependent stream transformations.
[<Sealed; AbstractClass>]
type DependentStreamTransformation =
  /// <summary>
  /// Initializes asynchronous dependent stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous dependent stream transformation.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (transformation, [<Optional; DefaultParameterValue(null: Action<bool>)>] dispose) =
    AsyncDependentStreamTransformation.From (transformation, dispose) :> IDependentStreamTransformation<'TState>

  /// <summary>
  /// Initializes asynchronous dependent stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
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

// Constains methods to create asynchronous stream producers.
[<Sealed; AbstractClass>]
type StreamProducer =

  /// <summary>
  /// Initializes asynchronous stream producer from the specified parameters.
  /// </summary>
  /// <param name="produce">Producer function to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream producer.</returns>
  static member From (produce, [<Optional; DefaultParameterValue(null:Action<bool>)>] dispose) =
    AsyncStreamProducer.From (produce, dispose) :> IStreamProducer

  /// <summary>
  /// Initializes asynchronous stream producer from the specified parameters.
  /// </summary>
  /// <param name="produce">Producer function to wrap.</param>
  /// <param name="dispose">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous producer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member Of (produce, ?dispose) =
    { new IStreamProducer with
        member __.AsyncProduce output =
          if isNull output then nullArg "output"
          produce output
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
  /// <param name="consumer">Asynchronous stream consumer.</param>
  /// <param name="source">Input stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member ConsumeAsync (consumer : IStreamConsumer, source, [<Optional>] cancellationToken) =
    match consumer with
    | :? AsyncStreamConsumer as inst -> inst.ConsumeDirect (source, cancellationToken)
    | _ ->
      Async.StartAsTask (consumer.AsyncConsume source, cancellationToken = cancellationToken) :> _

  /// <summary>
  /// Consumes the input stream producing some result.
  /// </summary>
  /// <param name="consumer">Asynchronous stream consumer.</param>
  /// <param name="source">Input stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member ConsumeAsync (consumer : IStreamConsumer<'T>, source, [<Optional>] cancellationToken) =
    match consumer with
    | :? AsyncStreamConsumer<'T> as inst ->
      inst.ConsumeDirect (source, cancellationToken)
    | _ ->
      Async.StartAsTask (consumer.AsyncConsume source, cancellationToken = cancellationToken)


/// Provides extensions for asynchronous stream producers.
[<Extension>]
[<Sealed; AbstractClass>]
type StreamProducerExtensions =

  /// <summary>
  /// Populates the output stream.
  /// </summary>
  /// <param name="producer">Asynchronous stream producer.</param>
  /// <param name="target">Output stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member ProduceAsync (producer : IStreamProducer, target, [<Optional>] cancellationToken) =
    match producer with
    | :? AsyncStreamProducer as inst -> inst.ProduceDirect (target, cancellationToken)
    | _ ->
      Async.StartAsTask (producer.AsyncProduce target, cancellationToken = cancellationToken) :> _

/// Provides extensions for asynchronous stream consumers.
[<Extension>]
[<Sealed; AbstractClass>]
type StreamToResultConsumerExtensions =

  /// <summary>
  /// Binds result of the source consumer to the reference cell producing resultless consumer.
  /// </summary>
  /// <param name="consumer">Source consumer.</param>
  /// <param name="ref">Reference cell to bind to.</param>
  /// <returns>Resultless stream consumer.</returns>
  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member BindTo (consumer, ref) =
    StreamToResultConsumer.BindTo ref consumer
