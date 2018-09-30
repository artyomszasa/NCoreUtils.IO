namespace NCoreUtils.IO

open System
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open NCoreUtils

/// Defines functionality for implementing asynchronous stream transformation.
type IStreamTransformation =
  inherit IDisposable
  /// <summary>
  /// Performs the transformation defined by the actual instance.
  /// </summary>
  /// <param name="input">Input stream.</param>
  /// <param name="output">Output stream.</param>
  abstract AsyncPerform : input:Stream * output:Stream -> Async<unit>

/// Defines functionality for implementing asynchronous stream consumer.
type IStreamConsumer =
  inherit IDisposable
  /// <summary>
  /// Consumes the input stream.
  /// </summary>
  /// <param name="input">Input stream.</param>
  abstract AsyncConsume : input:Stream -> Async<unit>

/// Defines functionality for implementing dependent asynchronous stream transformation.
type IDependentStreamTransformation<'TState> =
  inherit IDisposable
  /// <summary>
  /// Performs the transformation defined by the actual instance.
  /// </summary>
  /// <param name="input">Input stream.</param>
  /// <param name="dependentOutput">Function to populate output stream depending on the intermediate state.</param>
  abstract AsyncPerform : input:Stream * dependentOutput:('TState -> Stream) -> Async<unit>

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