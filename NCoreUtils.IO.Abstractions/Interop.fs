namespace NCoreUtils.IO

open System
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open NCoreUtils

/// Provides base class for implementing asynchronous stream transformation.
[<AbstractClass>]
type AsyncStreamTransformation =
  /// <summary>
  /// Initializes asynchronous stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream transformation.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (transformation, [<Optional; DefaultParameterValue(null: Action<bool>)>] dispose) =
    if isNull transformation then nullArg "transformation"
    new AsyncExplicitStreamTransformation (transformation, dispose) :> AsyncStreamTransformation

  /// <summary>
  /// Initializes asynchronous stream transformation.
  /// </summary>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  new () = { }

  /// <summary>
  /// Invoked on instance disposal.
  /// </summary>
  /// <param name="disposing">Whether the object is being disposed or finalized.</param>
  abstract Dispose : disposing:bool -> unit

  /// <summary>
  /// Performs the transformation defined by the actual instance.
  /// </summary>
  /// <param name="source">Input stream.</param>
  /// <param name="target">Output stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  abstract PerformAsync : source:Stream * target:Stream * [<Optional>] cancellationToken:CancellationToken -> Task

  member inline internal this.PerformDirect (source, target, cancellationToken) =
    this.PerformAsync (source, target, cancellationToken)

  /// <summary>
  /// Invoked on instance disposal.
  /// </summary>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.Dispose () =
    GC.SuppressFinalize this
    this.Dispose true

  default __.Dispose _ = ()
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member internal this.AsyncPerform (input, output) =
    Async.Adapt (fun cancellationToken -> this.PerformAsync (input, output, cancellationToken))

  interface IDisposable with
    member this.Dispose () = this.Dispose ()
  interface IStreamTransformation with
    member this.AsyncPerform (input, output) = this.AsyncPerform (input, output)

and
  [<Sealed>]
  private AsyncExplicitStreamTransformation =
    inherit AsyncStreamTransformation
    val private transformation : Func<Stream, Stream, CancellationToken, Task>
    val private dispose        : Action<bool>
    new (transformation, dispose) =
      { inherit AsyncStreamTransformation ()
        transformation = transformation
        dispose        = dispose }
    override this.PerformAsync (source, target, cancellationToken) =
      if isNull source then nullArg "source"
      if isNull target then nullArg "target"
      this.transformation.Invoke (source, target, cancellationToken)
    override this.Dispose disposing =
      if not (isNull this.dispose) then
        this.dispose.Invoke disposing

/// Provides base class for implementing dependent asynchronous stream transformation.
[<AbstractClass>]
type AsyncDependentStreamTransformation<'TState> =
  /// <summary>
  /// Initializes asynchronous stream transformation.
  /// </summary>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  new () = { }

  /// <summary>
  /// Invoked on instance disposal.
  /// </summary>
  /// <param name="disposing">Whether the object is being disposed or finalized.</param>
  abstract Dispose : disposing:bool -> unit

  /// <summary>
  /// Performs the transformation defined by the actual instance.
  /// </summary>
  /// <param name="source">Input stream.</param>
  /// <param name="dependentTarget">Function to populate output stream depending on the intermediate state.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  abstract PerformAsync
    :  source:Stream
    *  dependentTarget:Func<'TState, Stream>
    *  [<Optional>] cancellationToken:CancellationToken
    -> Task

  member inline internal this.PerformDirect (source, target, cancellationToken) =
    this.PerformAsync (source, target, cancellationToken)

  /// <summary>
  /// Invoked on instance disposal.
  /// </summary>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.Dispose () =
    GC.SuppressFinalize this
    this.Dispose true
  default __.Dispose _ = ()
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member internal this.AsyncPerform (input, dependentOutput) =
    Async.Adapt (fun cancellationToken -> this.PerformAsync (input, dependentOutput, cancellationToken))
  interface IDisposable with
    member this.Dispose () = this.Dispose ()
  interface IDependentStreamTransformation<'TState> with
    member this.AsyncPerform (input, dependentOutput) = this.AsyncPerform (input, Func<_, _> dependentOutput)

and
  [<Sealed>]
  private AsyncExplicitDependentStreamTransformation<'TState> =
    inherit AsyncDependentStreamTransformation<'TState>
    val private transformation : Func<Stream, Func<'TState, Stream>, CancellationToken, Task>
    val private dispose : Action<bool>
    new (transformation, dispose) =
      { inherit AsyncDependentStreamTransformation<'TState> ()
        transformation = transformation
        dispose        = dispose }
    override this.PerformAsync (source, dependentTarget, cancellationToken) =
      if isNull source then nullArg "source"
      if isNull dependentTarget then nullArg "dependentTarget"
      this.transformation.Invoke (source, dependentTarget, cancellationToken)
    override this.Dispose disposing =
      if not (isNull this.dispose) then
        this.dispose.Invoke disposing

/// Provides helper functions to create dependent asynchronous stream transformations.
[<Sealed; AbstractClass>]
type AsyncDependentStreamTransformation =
  /// <summary>
  /// Initializes asynchronous dependent stream transformation from the specified parameters.
  /// </summary>
  /// <param name="transformation">Transformation to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous dependent stream transformation.</returns>

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (transformation, [<Optional; DefaultParameterValue(null: Action<bool>)>] dispose) =
    if isNull transformation then nullArg "transformation"
    new AsyncExplicitDependentStreamTransformation<'TState> (transformation, dispose)
    :> AsyncDependentStreamTransformation<'TState>

/// provides base class for implementing asynchronous stream consumer.
[<AbstractClass>]
type AsyncStreamConsumer =
  /// <summary>
  /// Initializes asynchronous stream consumer from the specified parameters.
  /// </summary>
  /// <param name="consume">Consume function to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (consume,  [<Optional; DefaultParameterValue(null: Action<bool>)>] dispose) =
    if isNull consume then nullArg "consume"
    new AsyncExplicitStreamConsumer (consume, dispose) :> AsyncStreamConsumer

  /// <summary>
  /// Initializes asynchronous stream consumer.
  /// </summary>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  new () = { }
  /// <summary>
  /// Consumes the input stream.
  /// </summary>
  /// <param name="source">Input stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  abstract ConsumeAsync : source:Stream * [<Optional>] cancellationToken:CancellationToken -> Task

  /// <summary>
  /// Invoked on instance disposal.
  /// </summary>
  /// <param name="disposing">Whether the object is being disposed or finalized.</param>
  abstract Dispose : disposing:bool -> unit

  member inline internal this.ConsumeDirect (source, cancellationToken) = this.ConsumeAsync (source, cancellationToken)

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.Dispose () =
    GC.SuppressFinalize this
    this.Dispose true

  default __.Dispose _ = ()

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member internal this.AsyncConsume source =
    Async.Adapt (fun cancellationToken -> this.ConsumeAsync (source, cancellationToken))

  interface IDisposable with
    member this.Dispose () = this.Dispose ()
  interface IStreamConsumer with
    member this.AsyncConsume source = this.AsyncConsume source

and
  [<Sealed>]
  private AsyncExplicitStreamConsumer (consume : Func<Stream, CancellationToken, Task>, dispose : Action<bool>) =
    inherit AsyncStreamConsumer ()
    override __.ConsumeAsync (source, cancellationToken) =
      if isNull source then nullArg "source"
      consume.Invoke (source, cancellationToken)
    override __.Dispose disposing =
      if not (isNull dispose) then
        dispose.Invoke disposing

/// provides base class for implementing asynchronous stream consumer that produces some result.
[<AbstractClass>]
type AsyncStreamConsumer<'T> =
  /// <summary>
  /// Initializes asynchronous stream consumer from the specified parameters.
  /// </summary>
  /// <param name="consume">Consume function to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream consumer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (consume,  [<Optional; DefaultParameterValue(null: Action<bool>)>] dispose) =
    if isNull consume then nullArg "consume"
    new AsyncExplicitStreamConsumer<'T> (consume, dispose) :> AsyncStreamConsumer<'T>

  /// <summary>
  /// Initializes asynchronous stream consumer.
  /// </summary>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  new () = { }

  /// <summary>
  /// Consumes the input stream.
  /// </summary>
  /// <param name="source">Input stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  abstract ConsumeAsync : source:Stream * [<Optional>] cancellationToken:CancellationToken -> Task<'T>

  /// <summary>
  /// Invoked on instance disposal.
  /// </summary>
  /// <param name="disposing">Whether the object is being disposed or finalized.</param>
  abstract Dispose : disposing:bool -> unit

  member inline internal this.ConsumeDirect (source, cancellationToken) = this.ConsumeAsync (source, cancellationToken)

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.Dispose () =
    GC.SuppressFinalize this
    this.Dispose true

  default __.Dispose _ = ()

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member internal this.AsyncConsume source =
    Async.Adapt (fun cancellationToken -> this.ConsumeAsync (source, cancellationToken))

  interface IDisposable with
    member this.Dispose () = this.Dispose ()
  interface IStreamConsumer<'T> with
    member this.AsyncConsume source = this.AsyncConsume source

and
  [<Sealed>]
  private AsyncExplicitStreamConsumer<'T> (consume : Func<Stream, CancellationToken, Task<'T>>, dispose : Action<bool>) =
    inherit AsyncStreamConsumer<'T> ()
    override __.ConsumeAsync (source, cancellationToken) =
      if isNull source then nullArg "source"
      consume.Invoke (source, cancellationToken)
    override __.Dispose disposing =
      if not (isNull dispose) then
        dispose.Invoke disposing


/// provides base class for implementing asynchronous stream producer.
[<AbstractClass>]
type AsyncStreamProducer =
  /// <summary>
  /// Initializes asynchronous stream producer from the specified parameters.
  /// </summary>
  /// <param name="produce">Producer function to wrap.</param>
  /// <param name="action">Optional action to invoke on disposal.</param>
  /// <returns>Asynchronous stream producer.</returns>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member From (produce,  [<Optional; DefaultParameterValue(null: Action<bool>)>] dispose) =
    if isNull produce then nullArg "produce"
    new AsyncExplicitStreamProducer (produce, dispose) :> AsyncStreamProducer

  /// <summary>
  /// Initializes asynchronous stream producer.
  /// </summary>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  new () = { }
  /// <summary>
  /// Populates the output stream.
  /// </summary>
  /// <param name="source">Input stream.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  abstract ProduceAsync : target:Stream * [<Optional>] cancellationToken:CancellationToken -> Task

  /// <summary>
  /// Invoked on instance disposal.
  /// </summary>
  /// <param name="disposing">Whether the object is being disposed or finalized.</param>
  abstract Dispose : disposing:bool -> unit

  member inline internal this.ProduceDirect (target, cancellationToken) = this.ProduceAsync (target, cancellationToken)

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.Dispose () =
    GC.SuppressFinalize this
    this.Dispose true

  default __.Dispose _ = ()

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member internal this.AsyncProduce target =
    Async.Adapt (fun cancellationToken -> this.ProduceAsync (target, cancellationToken))

  interface IDisposable with
    member this.Dispose () = this.Dispose ()
  interface IStreamProducer with
    member this.AsyncProduce source = this.AsyncProduce source

and
  [<Sealed>]
  private AsyncExplicitStreamProducer (produce : Func<Stream, CancellationToken, Task>, dispose : Action<bool>) =
    inherit AsyncStreamProducer ()
    override __.ProduceAsync (source, cancellationToken) =
      if isNull source then nullArg "source"
      produce.Invoke (source, cancellationToken)
    override __.Dispose disposing =
      if not (isNull dispose) then
        dispose.Invoke disposing
