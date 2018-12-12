module NCoreUtils.IO.``produce-transform``

open Xunit
open System.Text
open NCoreUtils
open System.IO
open System

let private data = Encoding.UTF8.GetBytes "test"

[<Fact>]
let ``successfull`` () =
  let producerDisposed = ref false
  let producer =
    { new IStreamProducer with
        member __.AsyncProduce output = async {
          do! output.AsyncWrite data
          do! output.AsyncFlush ()
          output.Close () }
        member __.Dispose () = producerDisposed := true
    }
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformationDisposed := true
    }
  do
    use chain = StreamTransformation.chainProducer transformation producer
    use buffer = new MemoryStream ()
    StreamProducer.asyncProduce buffer chain |> Async.RunSynchronously
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.True !producerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``successfull no-close`` () =
  let producerDisposed = ref false
  let producer =
    { new IStreamProducer with
        member __.AsyncProduce output = async {
          do! output.AsyncWrite data
          do! output.AsyncFlush () }
        member __.Dispose () = producerDisposed := true
    }
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformationDisposed := true
    }
  do
    use chain = StreamTransformation.chainProducer transformation producer
    use buffer = new MemoryStream ()
    chain.AsyncProduce buffer |> Async.RunSynchronously
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.True !producerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``failed produce after first write`` () =
  let producerDisposed = ref false
  let producer =
    { new IStreamProducer with
        member __.AsyncProduce output = async {
          do! output.AsyncWrite data
          failwith "failed" }
        member __.Dispose () = producerDisposed := true
    }
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformationDisposed := true
    }
  do
    use chain = StreamTransformation.chainProducer transformation producer
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> chain.AsyncProduce buffer |> Async.RunSynchronously) |> ignore
  Assert.True !producerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``failed produce before first write`` () =
  let producerDisposed = ref false
  let producer =
    { new IStreamProducer with
        member __.AsyncProduce _ = failwith "failed"
        member __.Dispose () = producerDisposed := true
    }
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformationDisposed := true
    }
  do
    use chain = StreamTransformation.chainProducer transformation producer
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> chain.AsyncProduce buffer |> Async.RunSynchronously) |> ignore
  Assert.True !producerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``failed transform after first write`` () =
  let producerDisposed = ref false
  let producer =
    { new IStreamProducer with
        member __.AsyncProduce output = async {
          do! output.AsyncWrite data
          do! output.AsyncFlush ()
          output.Close () }
        member __.Dispose () = producerDisposed := true
    }
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          do! Stream.asyncCopyTo output input
          failwith "failed" }
        member __.Dispose () = transformationDisposed := true
    }
  do
    use chain = StreamTransformation.chainProducer transformation producer
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> chain.AsyncProduce buffer |> Async.RunSynchronously) |> ignore
  Assert.True !producerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``failed transform before first write`` () =
  let producerDisposed = ref false
  let producer =
    { new IStreamProducer with
        member __.AsyncProduce output = async {
          do! output.AsyncWrite data
          do! output.AsyncFlush ()
          output.Close () }
        member __.Dispose () = producerDisposed := true
    }
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (_, _) = failwith "failed"
        member __.Dispose () = transformationDisposed := true
    }
  do
    use chain = StreamTransformation.chainProducer transformation producer
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> chain.AsyncProduce buffer |> Async.RunSynchronously) |> ignore
  Assert.True !producerDisposed
  Assert.True !transformationDisposed
