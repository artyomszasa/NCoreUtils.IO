namespace NCoreUtils.IO

open System
open System.Diagnostics.CodeAnalysis
open System.IO
open System.IO.Pipes
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open NCoreUtils
open System.Text

[<AutoOpen>]
module internal AsyncExtensions =

  type Async with
    static member StartAsyncWithContinuations (computation, cont, econt, ccont, cancellationToken : CancellationToken) =
      Task.Factory.StartNew (
        (fun () -> Async.StartWithContinuations (computation, cont, econt, ccont, cancellationToken)),
        CancellationToken.None,
        TaskCreationOptions.DenyChildAttach,
        TaskScheduler.Current
      ) |> ignore

    static member FromContinuationsOneShot (callback: (_ -> _) * (exn -> _) * (OperationCanceledException -> _) -> _) =
      Async.FromContinuations
        (fun (success, failure, interrupt) ->
          let dedup = ref 0
          let cont cont value =
            if 0 = Interlocked.CompareExchange (dedup, 1, 0) then
              cont value
          callback (cont success, cont failure, cont interrupt))

[<RequireQualifiedAccess>]
[<CompiledName("ObservableExtensions")>]
module internal Observable =

  type private ReentrantDisposable (source : IDisposable) =
    let mutable isDisposed = 0
    interface IDisposable with
      member __.Dispose () =
        if 0 = Interlocked.CompareExchange (&isDisposed, 1, 0) then
          source.Dispose ()

  let private reentrant source = new ReentrantDisposable (source) :> IDisposable

  let inline private dispose (disposable : IDisposable) = disposable.Dispose ()

  let once callback source =
    let rec callback' item =
      dispose subscription
      callback item
    and subscription = reentrant (Observable.subscribe callback' source)
    subscription

module private CommonIOHelpers =

  [<Sealed>]
  type private IOPipe () =
    let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
    let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
    let producerStream = new WriteNotifyStream (server)
    let cancellationTokenSource = new CancellationTokenSource ()
    let mutable consumeSubscription : IDisposable = null
    let mutable cancellationRegistration : IDisposable = null;
    let mutable isDisposed = 0
    member __.Server = producerStream
    member __.Client = client :> Stream
    member __.CancellationTokenSource = cancellationTokenSource
    member __.SetConsumeSubscription value =
      if 0 <> isDisposed then ObjectDisposedException "IOPipe" |> raise
      if Interlocked.CompareExchange (&consumeSubscription, value, null) |> isNull |> not then
        invalidOp "trying to set consume subscription multiple times"
    member __.SetCancellationRegistration value =
      if 0 <> isDisposed then ObjectDisposedException "IOPipe" |> raise
      if Interlocked.CompareExchange (&cancellationRegistration, value, null) |> isNull |> not then
        invalidOp "trying to set cancellation registration multiple times"
    interface IDisposable with
      member __.Dispose () =
        if 0 = Interlocked.CompareExchange (&isDisposed, 1, 0) then
          producerStream.Dispose ()
          client.Dispose ()
          cancellationTokenSource.Dispose ()
          match Interlocked.Exchange (&consumeSubscription, null) with
          | null -> ()
          | d    -> d.Dispose ()
          match Interlocked.Exchange (&cancellationRegistration, null) with
          | null -> ()
          | d    -> d.Dispose ()

  [<ExcludeFromCodeCoverage>]
  let inline save box value =
    box := value
    value

  let asyncPipe (producer : Stream -> _) (consumer: Stream -> _) = async {
    use pipe = new IOPipe ()
    let! cancellationToken = Async.CancellationToken
    return! Async.FromContinuationsOneShot
      (fun (success, error, cancelled) ->
        // -------------------------
        // triggered if produce operation has finished.
        let producerDone = Event<unit> ()
        // triggered if either stream has been written to or produce operation has finished.
        let startProcessing = Observable.map ignore pipe.Server.Started |> Observable.merge producerDone.Publish
        // -------------------------
        // on error --> terminate inner tasks and return error.
        let onFailure (exn : exn) =
          pipe.CancellationTokenSource.Cancel ()
          match exn with
          | :? OperationCanceledException as exn -> cancelled exn
          | _    -> error exn
        // PRODUCER OPERATION ************************************************************************************
        cancellationToken.Register (ignore >> OperationCanceledException >> onFailure, null)
          |> pipe.SetCancellationRegistration
        Async.Start (async {
          try
            do! producer pipe.Server
            producerDone.Trigger ()
            pipe.Server.Close ()
          with exn -> onFailure exn
        }, cancellationToken)
        // CONSUMER OPERATION ************************************************************************************
        Observable.once
          (fun () ->
            Async.Start (
              async {
                try
                  let! result = consumer pipe.Client
                  success result
                with exn -> onFailure exn
              }, pipe.CancellationTokenSource.Token))
          startProcessing
          |> pipe.SetConsumeSubscription
      )}

[<Sealed>]
type internal LazyDisposable () =
  let mutable disposable : IDisposable = null
  let mutable isDisposed = 0
  member __.Set value =
    if 0 <> isDisposed then ObjectDisposedException "LazyDisposable" |> raise
    if Interlocked.CompareExchange (&disposable, value, null) |> isNull |> not then
      invalidOp "trying to set lazy disposable multiple times"
  interface IDisposable with
    member __.Dispose () =
      if 0 = Interlocked.CompareExchange (&isDisposed, 1, 0) then
        match Interlocked.Exchange (&disposable, null) with
        | null -> ()
        | d    -> d.Dispose ()

[<Sealed>]
type private CancellationTokenSourceWrapper () =
  let mutable isDisposed = 0
  member __.Disposed = 0 <> isDisposed
  member val Source = new CancellationTokenSource ()
  member this.IsCancellationRequested = this.Source.IsCancellationRequested
  member this.Token = this.Source.Token
  member this.Cancel () =
    if not this.Disposed then
      this.Source.Cancel ()
  interface IDisposable with
    member this.Dispose () =
      if 0 = Interlocked.CompareExchange (&isDisposed, 1, 0) then
        this.Source.Dispose ()


/// Contains operations for asynchronous stream transformations.
[<Extension>]
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StreamTransformation =

  /// <summary>
  /// Creates new asynchronous stream transformation that passes output of the first transformation to the second
  /// transformation as input using anonymous pipe.
  /// </summary>
  /// <param name="first">First stream transformation.</param>
  /// <param name="second">Second stream transformation.</param>
  /// <returns>Newly created transformation.</returns>
  [<Extension>]
  [<CompiledName("Chain")>]
  let chain (first : IStreamTransformation) (second : IStreamTransformation) =
    let isDisposed = ref 0
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          CommonIOHelpers.asyncPipe
            (fun target -> first.AsyncPerform (input, target))
            (fun source -> second.AsyncPerform (source, output))
        member __.Dispose () =
          if 0 = Interlocked.CompareExchange (isDisposed, 1, 0) then
            first.Dispose ()
            second.Dispose ()
    }

  [<Struct>]
  [<DefaultAugmentation(false)>]
  [<NoEquality; NoComparison>]
  type private SubTransformation =
    | DoTransform of Output : Stream
    | DoNothing

  let inline private disposeSubvo (subvo) =
    match subvo with
    | ValueSome (DoTransform stream) -> stream.Dispose ()
    | _ -> ()

  // [<Struct>]
  // [<DefaultAugmentation(false)>]
  // [<NoEquality; NoComparison>]
  // type private SubTransformationKeepState<'TState> =
  //   | DoTransformKeepState of State : 'TState * Input : Stream * Transformation : IStreamTransformation
  //   | DoNothingKeepState

  /// <summary>
  /// Creates new asynchronous stream transformation that dynamically evaluates possible second transformation and if
  /// second transformation present, passes output of the first transformation to the second transformation as input
  /// using anonymous pipe.
  /// </summary>
  /// <param name="first">First stream transformation.</param>
  /// <param name="next">Function to evaluate second stream transformation.</param>
  /// <returns>Newly created transformation.</returns>
  [<CompiledName("Chain")>]
  let chainDependent (first : IDependentStreamTransformation<'TState>) (next : 'TState -> IStreamTransformation voption) =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          use cancellationTokenSource = new CancellationTokenSourceWrapper ()
          use cancellationRegistration = new LazyDisposable ()
          let! cancellationToken = Async.CancellationToken
          return!
            Async.FromContinuationsOneShot
              (fun (success, error, cancelled) ->
                let todo = ref ValueNone
                // handles failures, both in initial and optional dependent transformation
                let onFailure (exn : exn) =
                  cancellationTokenSource.Cancel ()
                  match exn with
                  | :? OperationCanceledException as exn -> cancelled exn
                  | _    -> error exn
                cancellationToken.Register (ignore >> OperationCanceledException >> onFailure, null)
                  |> cancellationRegistration.Set
                // starts dependent transformation asynchronously, called only if dependent transformation has been
                // returned, returns immediately.
                let startDependent (client : Stream) (transformation : IStreamTransformation) =
                  match cancellationTokenSource.IsCancellationRequested with
                  | true ->
                    transformation.Dispose ()
                    client.Dispose ()
                  | _ ->
                    Async.StartAsyncWithContinuations (
                      async.Delay (fun () -> transformation.AsyncPerform (client, output)),
                      (fun () ->
                        transformation.Dispose ()
                        client.Dispose ()
                        success ()),
                      (fun exn ->
                        transformation.Dispose ()
                        client.Dispose ()
                        onFailure exn),
                      (fun exn ->
                        transformation.Dispose ()
                        client.Dispose ()
                        onFailure exn),
                      cancellationTokenSource.Token)
                // initializes possible further transformation
                // if no exception has been raised updates todo
                // - triggers nextStep if further transformation is required
                let initOutput state =
                  match next state with
                  | ValueNone ->
                    todo := ValueSome DoNothing
                    output
                  | ValueSome transformation ->
                    let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
                    let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
                    todo := ValueSome (DoTransform server)
                    startDependent client transformation
                    (server :> Stream)
                // runs initial transformation, following cases are possible:
                // - initOutput is called without further transformation --> handled by initOutput
                // - initOutput is called with further transformation --> handled by initOutput
                // - initial transformation exited without calling callback --> error
                // - no further transformation should be performed --> trigger success
                // - further transformation should be performed --> disposes inernal server stream
                // - generic error occured while executing initial transformation --> trigger failure
                Async.StartWithContinuations (
                  first.AsyncPerform (input, initOutput),
                  (fun () ->
                    match !todo with
                    | ValueNone                      -> invalidOp "dependent transformation should call the callback prior exiting"
                    | ValueSome  DoNothing           -> success ()
                    | ValueSome (DoTransform server) -> server.Dispose ()),
                  (fun (exn) ->
                    onFailure exn
                    disposeSubvo !todo),
                  (fun (exn) ->
                    onFailure exn
                    disposeSubvo !todo),
                  cancellationToken
                )
              )
          }
        member __.Dispose () =
          first.Dispose ()
    }

  /// Used to evaluate dependent transformation.
  type DependentTransformationDelegate<'TState> =
    delegate of state:'TState * [<Out>] transformation:byref<IStreamTransformation> -> bool

  /// <summary>
  /// Creates new asynchronous stream transformation that dynamically evaluates possible second transformation and if
  /// second transformation present, passes output of the first transformation to the second transformation as input
  /// using anonymous pipe.
  /// </summary>
  /// <param name="first">First stream transformation.</param>
  /// <param name="next">Function to evaluate second stream transformation.</param>
  /// <returns>Newly created transformation.</returns>
  [<Extension>]
  [<CompiledName("Chain")>]
  let chainDependentFunc (first : IDependentStreamTransformation<'TState>) (next : DependentTransformationDelegate<'TState>) =
    let next' state =
      let mutable transformation = Unchecked.defaultof<_>
      match next.Invoke (state, &transformation) with
      | true -> ValueSome transformation
      | _    -> ValueNone
    chainDependent first next'

  /// <summary>
  /// Creates new asynchronous stream consumer that passes output of the specified transformation to the specified
  /// consumer as input using anonymous pipe.
  /// </summary>
  /// <param name="transformation">Stream transformation.</param>
  /// <param name="consumer">Stream consumer.</param>
  /// <returns>Newly created consumer.</returns>
  [<Extension>]
  [<CompiledName("Chain")>]
  let chainConsumer (transformation : IStreamTransformation) (consumer : IStreamConsumer) =
    let isDisposed = ref 0
    { new IStreamConsumer with
        member __.AsyncConsume input =
          CommonIOHelpers.asyncPipe
            (fun target -> transformation.AsyncPerform (input, target))
            consumer.AsyncConsume
        member __.Dispose () =
          if 0 = Interlocked.CompareExchange (isDisposed, 1, 0) then
            transformation.Dispose ()
            consumer.Dispose ()
    }

  /// <summary>
  /// Creates new asynchronous stream consumer that passes output of the specified transformation to the specified
  /// consumer as input using anonymous pipe.
  /// </summary>
  /// <param name="transformation">Stream transformation.</param>
  /// <param name="consumer">Stream consumer.</param>
  /// <returns>Newly created consumer.</returns>
  [<Extension>]
  [<CompiledName("Chain")>]
  let chainToResultConsumer (transformation : IStreamTransformation) (consumer : IStreamConsumer<'T>) =
    let isDisposed = ref 0
    { new IStreamConsumer<_> with
        member __.AsyncConsume input =
          CommonIOHelpers.asyncPipe
            (fun target -> transformation.AsyncPerform (input, target))
            consumer.AsyncConsume
        member __.Dispose () =
          if 0 = Interlocked.CompareExchange (isDisposed, 1, 0) then
            transformation.Dispose ()
            consumer.Dispose ()
    }

  [<RequireQualifiedAccess>]
  type private Status = Pending | Success | Failure of Exception:exn | Cancelled

  /// <summary>
  /// Creates new asynchronous stream producer that passes output of the specified producer to the specified
  /// transformation as input using anonymous pipe.
  /// </summary>
  /// <param name="transformation">Stream transformation.</param>
  /// <param name="producer">Stream producer.</param>
  /// <returns>Newly created producer.</returns>
  [<Extension>]
  [<CompiledName("Chain")>]
  let chainProducer (transformation : IStreamTransformation) (producer : IStreamProducer) =
    let isDisposed = ref 0
    { new IStreamProducer with
        member __.AsyncProduce output =
          CommonIOHelpers.asyncPipe
            producer.AsyncProduce
            (fun source -> transformation.AsyncPerform (source, output))
        member __.Dispose () =
          if 0 = Interlocked.CompareExchange (isDisposed, 1, 0) then
            transformation.Dispose ()
            producer.Dispose ()
    }

  /// <summary>
  /// Performs the transformation defined by the specified instance.
  /// </summary>
  /// <param name="source">Input stream.</param>
  /// <param name="target">Output stream.</param>
  /// <param name="transformation">Stream transfomration.</param>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("AsyncPerform")>]
  let asyncPerform source target (transformation : IStreamTransformation) =
    transformation.AsyncPerform (source, target)

/// Contains operations for asynchronous stream consumers.
[<Extension>]
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StreamConsumer =

  /// <summary>
  /// Creates new asynchronous stream consumer that passes output of the specified transformation to the specified
  /// consumer as input using anonymous pipe.
  /// </summary>
  /// <param name="consumer">Stream consumer.</param>
  /// <param name="transformation">Stream transformation.</param>
  /// <returns>Newly created consumer.</returns>
  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("ChainSource")>]
  let chain (consumer : IStreamConsumer) (transformation : IStreamTransformation) =
    StreamTransformation.chainConsumer transformation consumer

  /// <summary>
  /// Creates new asynchronous stream consumer that multiplexes the source passing the input to both spcified consumers.
  /// </summary>
  /// <param name="consumer">First stream consumer.</param>
  /// <param name="other">Second stream consumer.</param>
  /// <returns>Newly created consumer.</returns>
  [<Extension>]
  [<CompiledName("Combine")>]
  let combine (consumer : IStreamConsumer) (other : IStreamConsumer) =
    let consumers =
      match consumer, other with
      | (:? MultiplexingConsumer as m0), (:? MultiplexingConsumer as m1) ->
        Array.init (m0.Consumers.Count + m1.Consumers.Count)
          (fun i ->
            match i < m0.Consumers.Count with
            | true -> m0.Consumers.[i]
            | _    -> m1.Consumers.[i - m0.Consumers.Count])
      | (:? MultiplexingConsumer as m), c ->
        Array.init (m.Consumers.Count + 1)
          (fun i ->
            match i < m.Consumers.Count with
            | true -> m.Consumers.[i]
            | _    -> c)
      | c, (:? MultiplexingConsumer as m) ->
        Array.init (m.Consumers.Count + 1)
          (fun i ->
            match i < m.Consumers.Count with
            | true -> m.Consumers.[i]
            | _    -> c)
      | _ -> [| consumer; other |]
    new MultiplexingConsumer (consumers) :> IStreamConsumer

  /// <summary>
  /// Consumes the input stream using the specified consumer.
  /// </summary>
  /// <param name="source">Input stream.</param>
  /// <param name="consumer">Stream consumer.</param>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("AsyncConsume")>]
  let asyncConsume source (consumer : IStreamConsumer) =
    consumer.AsyncConsume source

  /// <summary>
  /// Creates new asynchronous stream consumer that passes output of the specified transformation to the specified
  /// consumer as input using anonymous pipe.
  /// </summary>
  /// <param name="transformation">Stream transformation.</param>
  /// <param name="consumer">Stream consumer.</param>
  /// <returns>Newly created consumer.</returns>
  [<CompiledName("AsyncConsume")>]
  let asyncConsumeProducer (producer : IStreamProducer) (consumer : IStreamConsumer) = async {
    try
      do! CommonIOHelpers.asyncPipe producer.AsyncProduce consumer.AsyncConsume
    finally
      producer.Dispose ()
      consumer.Dispose () }

/// Contains operations for asynchronous stream consumers that produces some result.
[<Extension>]
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StreamToResultConsumer =

  /// <summary>
  /// Consumes the input stream using the specified consumer.
  /// </summary>
  /// <param name="source">Input stream.</param>
  /// <param name="consumer">Stream consumer.</param>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("AsyncConsume")>]
  let asyncConsume source (consumer : IStreamConsumer<'T>) =
    consumer.AsyncConsume source

  /// <summary>
  /// Creates new asynchronous stream consumer that passes output of the specified transformation to the specified
  /// consumer as input using anonymous pipe.
  /// </summary>
  /// <param name="transformation">Stream transformation.</param>
  /// <param name="consumer">Stream consumer.</param>
  /// <returns>Newly created consumer.</returns>
  [<CompiledName("AsyncConsume")>]
  let asyncConsumeProducer (producer : IStreamProducer) (consumer : IStreamConsumer<'T>) = async {
    try
      return! CommonIOHelpers.asyncPipe producer.AsyncProduce consumer.AsyncConsume
    finally
      producer.Dispose ()
      consumer.Dispose () }

/// Contains operations for asynchronous stream producers.
[<Extension>]
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StreamProducer =

  /// <summary>
  /// Populates the output stream using the specified producer.
  /// </summary>
  /// <param name="target">Output stream.</param>
  /// <param name="producer">Stream producer.</param>
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("AsyncProduce")>]
  let asyncProduce target (producer : IStreamProducer) = producer.AsyncProduce target

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("FromStream")>]
  let ofStreamWithCleanUp stream leaveOpen  =
    { new IStreamProducer with
        member __.AsyncProduce output = Stream.asyncCopyTo output stream
        member __.Dispose () =
          if not leaveOpen then
            stream.Dispose ()
    }

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("FromStream")>]
  let ofStream stream = ofStreamWithCleanUp stream false

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("FromArray")>]
  let ofArrayPart array offset count  =
    { new IStreamProducer with
        member __.AsyncProduce output = async {
          do! output.AsyncWrite (array, offset, count)
          do! output.AsyncFlush () }
        member __.Dispose () = ()
    }

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("FromArray")>]
  let ofArray array = ofArrayPart array 0 array.Length

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  [<CompiledName("FromString")>]
  let ofString (input : string) (encoding : Encoding)  =
    { new IStreamProducer with
        member __.AsyncProduce output = async {
          use writer = new StreamWriter (output, encoding, 8192, true)
          do! Async.Adapt (fun _ -> writer.WriteAsync input)
          do! Async.Adapt (fun _ -> writer.FlushAsync ()) }
        member __.Dispose () = ()
    }


[<Extension>]
[<AbstractClass; Sealed>]
type StreamPipelineExtensions =

  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member ConsumeAsync (this : IStreamConsumer, producer : IStreamProducer, [<Optional>] cancellationToken : CancellationToken) =
    Async.StartAsTask (StreamConsumer.asyncConsumeProducer producer this, cancellationToken = cancellationToken)
    :> Task

  [<Extension>]
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  static member ConsumeAsync (this : IStreamConsumer<'T>, producer : IStreamProducer, [<Optional>] cancellationToken : CancellationToken) =
    Async.StartAsTask (StreamToResultConsumer.asyncConsumeProducer producer this, cancellationToken = cancellationToken)
