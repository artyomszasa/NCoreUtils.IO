namespace NCoreUtils.IO

open System
open System.IO
open System.IO.Pipes
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks

type WriteNotifyStream (baseStream : Stream, [<Optional>] leaveOpen : bool) =
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





[<Extension>]
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StreamTransformation =

  let private awaitObservable (observable : IObservable<EventArgs>) = async {
    let! cancellationToken = Async.CancellationToken
    do!
      Async.FromContinuations
        (fun (succ, _, off) ->
          let mutable cancellationSubscription = Unchecked.defaultof<IDisposable>
          let mutable observableSubscription = Unchecked.defaultof<IDisposable>
          let dispose () =
            if not (isNull cancellationSubscription) then cancellationSubscription.Dispose ()
            if not (isNull observableSubscription)   then observableSubscription.Dispose ()
          let dedup = ref 0
          cancellationSubscription <-
            cancellationToken.Register
              (fun () ->
                if 0 = Interlocked.CompareExchange (dedup, 1, 0) then
                  dispose ();
                  off <| new TaskCanceledException ())
          observableSubscription <-
            observable.Subscribe
              (fun _ ->
                if 0 = Interlocked.CompareExchange (dedup, 1, 0) then
                  dispose ();
                  succ ())
        ) }

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
          [ async {
              try do! first.AsyncPerform (input, producer)
              finally firstFinished.Trigger (this, EventArgs.Empty) }
            async {
              do! awaitObservable trigger
              do! second.AsyncPerform (client, output) } ]
          |> Async.Parallel
          |> Async.Ignore
        member __.Dispose () =
          first.Dispose ()
          second.Dispose ()
          producer.Dispose ()
          client.Dispose () }

  [<Struct>]
  type private SubTransformation =
    | DoTransform of Input : Stream * Transformation : IStreamTransformation
    | DoNothing

  [<Struct>]
  type private SubTransformationKeepState<'TState> =
    | DoTransformKeepState of State : 'TState * Input : Stream * Transformation : IStreamTransformation
    | DoNothingKeepState


  [<Extension>]
  [<CompiledName("Chain")>]
  let chainDependent (first : IDependentStreamTransformation<'TState>) (next : 'TState -> IStreamTransformation option) =
    let toDispose = ref ([] : IDisposable list)
    { new IStreamTransformation with
        member __.Dispose () =
          !toDispose |> List.iter (fun disposable -> disposable.Dispose ())
        member __.AsyncPerform (input, output) =
          let todo = TaskCompletionSource ()
          let second = async {
            let! todo = Async.AwaitTask todo.Task
            match todo with
            | DoTransform (input, second) ->
              toDispose := (second :> _) :: (!toDispose)
              do! second.AsyncPerform (input, output)
            | DoNothing -> () }
          let first = async {
            let lazyInit (second0 : IStreamTransformation option) =
              match second0 with
              | None ->
                todo.TrySetResult DoNothing |> ignore
                output
              | Some second ->
                let server = new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None)
                let client = new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle)
                toDispose := (server :> _) :: (client :> _) :: (!toDispose)
                todo.TrySetResult <| DoTransform (client, second) |> ignore
                server :> _
            do! first.AsyncPerform (input, next >> lazyInit) }
          [ first
            second ]
          |> Async.Parallel
          |> Async.Ignore
    }