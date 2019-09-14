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
    Async.RunSynchronously (StreamProducer.asyncProduce buffer chain, 300)
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
    Async.RunSynchronously (chain.AsyncProduce buffer, 300)
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.True !producerDisposed
  Assert.True !transformationDisposed

[<Fact>]
[<CompiledName("FailedProduceAfterFirstWrite")>]
let ``failed produce after first write`` () =

  // if not(System.Diagnostics.Debugger.IsAttached) then
  //   printfn "Please attach a debugger, PID: %d" (System.Diagnostics.Process.GetCurrentProcess().Id)
  // while not(System.Diagnostics.Debugger.IsAttached) do
  //   System.Threading.Thread.Sleep(100)
  // System.Diagnostics.Debugger.Break()

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
    Assert.Throws<Exception>(fun () -> Async.RunSynchronously (chain.AsyncProduce buffer, 300)) |> Assert.IsNotType<TimeoutException>
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
    Assert.Throws<Exception>(fun () -> Async.RunSynchronously (chain.AsyncProduce buffer, 300)) |> Assert.IsNotType<TimeoutException>
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
    Assert.Throws<Exception>(fun () -> Async.RunSynchronously (chain.AsyncProduce buffer, 300)) |> Assert.IsNotType<TimeoutException>
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
    Assert.Throws<Exception>(fun () -> Async.RunSynchronously (chain.AsyncProduce buffer, 300)) |> Assert.IsNotType<TimeoutException>
  Assert.True !producerDisposed
  Assert.True !transformationDisposed
