namespace NCoreUtils.IO

open System
open System.IO
open System.IO.Pipes
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open NCoreUtils
open System.Text

type internal WriteNotifyStream (baseStream : Stream, [<Optional>] leaveOpen : bool) =
  inherit Stream ()
  let started = Event<EventHandler<_>, EventArgs> ()
  let mutable debounce = 0
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member private this.Trigger () =
    if 0 = Interlocked.CompareExchange (&debounce, 1, 0) then
      started.Trigger (this, EventArgs.Empty)
  [<CLIEvent>]
  member __.Started = started.Publish
  member val BaseStream = baseStream
  override this.CanRead = this.BaseStream.CanRead
  override this.CanSeek = this.BaseStream.CanSeek
  override this.CanTimeout = this.BaseStream.CanTimeout
  override this.CanWrite = this.BaseStream.CanWrite
  override this.Length = this.BaseStream.Length
  override this.Position with get () = this.BaseStream.Position and set value = this.BaseStream.Position <- value
  override this.ReadTimeout with get () = this.BaseStream.ReadTimeout and set value = this.BaseStream.ReadTimeout <- value
  override this.WriteTimeout with get () = this.BaseStream.WriteTimeout and set value = this.BaseStream.WriteTimeout <- value
  override this.BeginRead (buffer, offset, count, callback, state) = this.BaseStream.BeginRead (buffer, offset, count, callback, state)
  override this.BeginWrite (buffer, offset, count, callback, state) =
    this.Trigger ()
    this.BaseStream.BeginWrite (buffer, offset, count, callback, state)
  override this.Close () = this.BaseStream.Close ()
  override this.CopyToAsync (destination, bufferSize, cancellationToken) = this.BaseStream.CopyToAsync (destination, bufferSize, cancellationToken)
  override this.Dispose disposing =
    if disposing && not leaveOpen then
      this.BaseStream.Dispose ()
  override this.EndRead asyncResult = this.BaseStream.EndRead asyncResult
  override this.EndWrite asyncResult = this.BaseStream.EndWrite asyncResult
  override this.Flush () =
    this.Trigger ()
    this.BaseStream.Flush ()
  override this.FlushAsync cancellationToken =
    this.Trigger ()
    this.BaseStream.FlushAsync cancellationToken
  override this.Read (buffer, offset, count) = this.BaseStream.Read (buffer, offset, count)
  override this.ReadAsync (buffer, offset, count, cancellationToken) = this.BaseStream.ReadAsync (buffer, offset, count, cancellationToken)
  override this.ReadByte () = this.BaseStream.ReadByte ()
  override this.Seek (offset, origin) = this.BaseStream.Seek (offset, origin)
  override this.SetLength value = this.BaseStream.SetLength value
  override this.Write (buffer, offset, count) =
    this.Trigger ()
    this.BaseStream.Write (buffer, offset, count)
  override this.WriteAsync (buffer, offset, count, cancellationToken) =
    this.Trigger ()
    this.BaseStream.WriteAsync (buffer, offset, count, cancellationToken)
  override this.WriteByte value =
    this.Trigger ()
    this.BaseStream.WriteByte value

[<AutoOpen>]
module private ObservableHelpers =

  [<CompiledName("AwaitObservable")>]
  let awaitObservable (observable : IObservable<EventArgs>) =
    let completionSource = TaskCompletionSource<int> ()
    let dedup = ref 0
    let observableSubscription = ref Unchecked.defaultof<IDisposable>
    let dispose () = observableSubscription.Value.Dispose ()
    observableSubscription :=
      observable.Subscribe
        (fun _ ->
          if 0 = Interlocked.CompareExchange (dedup, 1, 0) then
            completionSource.SetResult Unchecked.defaultof<_>
            dispose ()
        )
    async.Bind (
      Async.CancellationToken,
      fun cancellationToken -> async {
        use _ =
          cancellationToken.Register
            (fun _ ->
              if 0 = Interlocked.CompareExchange (dedup, 1, 0) then
                completionSource.SetCanceled ()
                dispose ()
            )
        do! Async.Adapt (fun _ -> completionSource.Task :> Task) }
    )

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
    let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
    let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
    let producer = new WriteNotifyStream (server)
    let firstFinished = Event<EventHandler<_>, EventArgs> ()
    let trigger = Observable.merge producer.Started firstFinished.Publish
    { new IStreamTransformation with
        member this.AsyncPerform (input, output) =
          let start = awaitObservable trigger
          [ async {
              try do! first.AsyncPerform (input, producer)
              finally
                firstFinished.Trigger (this, EventArgs.Empty)
                producer.Close () }
            async {
              do! start
              do! second.AsyncPerform (client, output) } ]
          |> Async.Parallel
          |> Async.Ignore
        member __.Dispose () =
          first.Dispose ()
          second.Dispose ()
          producer.Dispose ()
          client.Dispose () }

  [<Struct>]
  [<DefaultAugmentation(false)>]
  [<NoEquality; NoComparison>]
  type private SubTransformation =
    | DoTransform of Input : Stream * Transformation : IStreamTransformation
    | DoNothing

  [<Struct>]
  [<DefaultAugmentation(false)>]
  [<NoEquality; NoComparison>]
  type private SubTransformationKeepState<'TState> =
    | DoTransformKeepState of State : 'TState * Input : Stream * Transformation : IStreamTransformation
    | DoNothingKeepState


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
    let toDispose = ref ([] : IDisposable list)
    { new IStreamTransformation with
        member __.Dispose () =
          first.Dispose ()
          !toDispose |> List.iter (fun disposable -> disposable.Dispose ())
        member __.AsyncPerform (input, output) =
          let server = ref Unchecked.defaultof<AnonymousPipeServerStream>
          let todo = TaskCompletionSource ()
          let initDependent (second0 : IStreamTransformation voption) =
            match second0 with
            | ValueNone ->
              todo.TrySetResult DoNothing |> ignore
              output
            | ValueSome second ->
              server := new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
              let client = new AnonymousPipeClientStream (PipeDirection.In, server.Value.ClientSafePipeHandle)
              toDispose := (!server :> _) :: (client :> _) :: (second :> _) :: (!toDispose)
              todo.TrySetResult <| DoTransform (client, second) |> ignore
              !server :> _
          let second = async {
            match! Async.Adapt (fun _ -> todo.Task) with
            | DoTransform (input, second) -> do! second.AsyncPerform (input, output)
            | DoNothing                   -> () }
          let first = async {
            try do! first.AsyncPerform (input, next >> initDependent)
            finally
              // if callback has never been called
              todo.TrySetResult DoNothing |> ignore
              // if callback has been called
              match !server with
              | null -> ()
              | server -> server.Close () }
          [ first
            second ]
          |> Async.Parallel
          |> Async.Ignore
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
    let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
    let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
    let producer = new WriteNotifyStream (server)
    let firstFinished = Event<EventHandler<_>, EventArgs> ()
    let trigger = Observable.merge producer.Started firstFinished.Publish
    { new IStreamConsumer with
        member this.AsyncConsume input =
          let start = awaitObservable trigger
          [ async {
              try do! transformation.AsyncPerform (input, producer)
              finally
                firstFinished.Trigger (this, EventArgs.Empty)
                producer.Close () }
            async {
              do! start
              do! consumer.AsyncConsume client } ]
          |> Async.Parallel
          |> Async.Ignore
        member __.Dispose () =
          transformation.Dispose ()
          consumer.Dispose ()
          producer.Dispose ()
          client.Dispose () }

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
    let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
    let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
    let producer = new WriteNotifyStream (server)
    let firstFinished = Event<EventHandler<_>, EventArgs> ()
    let trigger = Observable.merge producer.Started firstFinished.Publish
    { new IStreamConsumer<_> with
        member this.AsyncConsume input =
          let start = awaitObservable trigger
          let result = ref Unchecked.defaultof<_>
          let computation =
            [ async {
                try do! transformation.AsyncPerform (input, producer)
                finally
                  firstFinished.Trigger (this, EventArgs.Empty)
                  producer.Close () }
              async {
                do! start
                let! res = consumer.AsyncConsume client
                result := res } ]
            |> Async.Parallel
            |> Async.Ignore
          async.Bind (computation, fun () -> async.Return !result)
        member __.Dispose () =
          transformation.Dispose ()
          consumer.Dispose ()
          producer.Dispose ()
          client.Dispose () }

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
    let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
    let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
    let producerStream = new WriteNotifyStream (server)
    let firstFinished = Event<EventHandler<_>, EventArgs> ()
    let trigger = Observable.merge producerStream.Started firstFinished.Publish
    { new IStreamProducer with
        member this.AsyncProduce output =
          let start = awaitObservable trigger
          [ async {
              do! start
              do! transformation.AsyncPerform (client, output) }
            async {
              try do! producer.AsyncProduce producerStream
              finally
                firstFinished.Trigger (this, EventArgs.Empty)
                producerStream.Close () } ]
          |> Async.Parallel
          |> Async.Ignore
        member __.Dispose () =
          transformation.Dispose ()
          producer.Dispose ()
          producerStream.Dispose ()
          client.Dispose () }

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
      match consumer with
      | :? MultiplexingConsumer as m ->
        Array.init m.Comsumers.Count
          (fun i ->
            match i < m.Comsumers.Count with
            | true -> m.Comsumers.[i]
            | _    -> other)
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
  let asyncConsumeProducer (streamProducer : IStreamProducer) (consumer : IStreamConsumer) =
    let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
    let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
    let producer = new WriteNotifyStream (server)
    let firstFinished = Event<EventHandler<_>, EventArgs> ()
    let trigger = Observable.merge producer.Started firstFinished.Publish
    let start = awaitObservable trigger
    async {
      try
        do!
          [ async {
              try do! streamProducer.AsyncProduce (producer)
              finally
                firstFinished.Trigger (streamProducer, EventArgs.Empty)
                producer.Close () }
            async {
              do! start
              do! consumer.AsyncConsume client } ]
          |> Async.Parallel
          |> Async.Ignore
      finally
        server.Dispose ()
        client.Dispose() }

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
  let asyncConsumeProducer (streamProducer : IStreamProducer) (consumer : IStreamConsumer<'T>) =
    let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
    let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
    let producer = new WriteNotifyStream (server)
    let firstFinished = Event<EventHandler<_>, EventArgs> ()
    let trigger = Observable.merge producer.Started firstFinished.Publish
    let start = awaitObservable trigger
    let result = ref Unchecked.defaultof<_>
    let computation =
      [ async {
          try do! streamProducer.AsyncProduce (producer)
          finally
            firstFinished.Trigger (streamProducer, EventArgs.Empty)
            producer.Close () }
        async {
          do! start
          let! res = consumer.AsyncConsume client
          result := res } ]
      |> Async.Parallel
      |> Async.Ignore
    async {
      try
        do! computation
        return !result
      finally
        server.Dispose ()
        client.Dispose() }

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
        member __.AsyncProduce output = output.AsyncWrite (array, offset, count)
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
