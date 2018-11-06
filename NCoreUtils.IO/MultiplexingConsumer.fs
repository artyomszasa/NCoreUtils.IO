namespace NCoreUtils.IO

open System
open System.Collections.Generic
open System.IO
open System.IO.Pipes
open NCoreUtils

[<AutoOpen>]
module private MultiplexingConsumerHelpers =
  let inline internal bindToValue op ref = async {
    let! value = op
    ref := value }


type private MultiplexingConsumer (bufferSize : int, consumers : IReadOnlyList<IStreamConsumer>) =

  new (consumers) = new MultiplexingConsumer (8192, consumers)

  member val Comsumers  = consumers
  member val BufferSize = bufferSize

  interface IDisposable with
    member this.Dispose () =
      this.Comsumers
      |> Seq.iter (fun consumer -> consumer.Dispose ())
  interface IStreamConsumer with
    member this.AsyncConsume source = async {
      let servers = Array.init consumers.Count (fun _ -> new AnonymousPipeServerStream (PipeDirection.Out, HandleInheritability.None))
      let clients = servers |> Array.map (fun server -> new AnonymousPipeClientStream (PipeDirection.In, server.ClientSafePipeHandle))
      try
        let multiplex = async {
          let buffer = Array.zeroCreate bufferSize
          // COPY
          let read = ref 0
          do! bindToValue (source.AsyncRead buffer) read
          while !read <> 0 do
            do!
              servers
              |> Array.map (fun server -> server.AsyncWrite (buffer, 0, !read))
              |> Async.Parallel
              |> Async.Ignore
            do! bindToValue (source.AsyncRead buffer) read
          // FLUSH
          do!
            servers
            |> Array.map (fun server -> server.AsyncFlush ())
            |> Async.Parallel
            |> Async.Ignore
          // CLOSE
          do
            servers
            |> Array.iter (fun server -> server.Close()) }
        let computations =
          Array.init (consumers.Count + 1)
            (fun i ->
              match i with
              | 0 -> multiplex
              | _ -> this.Comsumers.[i - 1].AsyncConsume clients.[i - 1]
            )
        do!
          Async.Parallel computations
          |> Async.Ignore
      finally
        servers |> Array.iter (fun server -> server.Dispose ())
        clients |> Array.iter (fun client -> client.Dispose ()) }
