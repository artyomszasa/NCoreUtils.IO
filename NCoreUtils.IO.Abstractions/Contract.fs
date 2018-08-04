namespace NCoreUtils.IO

open System
open System.IO

// type IStreamDataProducer =
//   abstract AsyncProduce : output:Stream -> Async<unit>
//
// type IStreamDataConsumer =
//   abstract AsyncConsume : input:Stream -> Async<unit>
//
// type IStreamTransformation =
//   inherit IStreamDataConsumer
//   inherit IStreamDataProducer

type IStreamTransformation =
  inherit IDisposable
  abstract AsyncPerform : input:Stream * output:Stream -> Async<unit>

type IDependentStreamTransformation<'TState> =
  inherit IDisposable
  abstract AsyncPerform : input:Stream * dependentOutput:('TState -> Stream) -> Async<unit>

type StreamTransformation =

  static member From (transformation : Stream -> Stream -> Async<unit>, ?dispose : unit -> unit) =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          transformation input output
        member __.Dispose () =
          match dispose with
          | None -> ()
          | Some dispose -> dispose ()
    }