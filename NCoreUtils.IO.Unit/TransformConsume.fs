module NCoreUtils.IO.``transform-consume``

open Xunit
open System.Text
open NCoreUtils
open System.IO
open System

let private data = Encoding.UTF8.GetBytes "test"

[<Fact>]
let ``successfull`` () =
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          do! Stream.asyncCopyTo output input
          output.Close () }
        member __.Dispose () = transformationDisposed := true
    }
  let consumerDisposed = ref false
  let consumer =
    { new IStreamConsumer with
        member __.AsyncConsume input = async {
          use buffer = new MemoryStream ()
          do! Stream.asyncCopyTo buffer input
          Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString) }
        member __.Dispose () = consumerDisposed := true
    }
  do
    use chain = StreamTransformation.chainConsumer transformation consumer
    use buffer = new MemoryStream (data, false)
    StreamConsumer.asyncConsume buffer chain |> Async.RunSynchronously
  Assert.True !consumerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``successfull to result`` () =
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          do! Stream.asyncCopyTo output input
          output.Close () }
        member __.Dispose () = transformationDisposed := true
    }
  let consumerDisposed = ref false
  let consumer =
    { new IStreamConsumer<string> with
        member __.AsyncConsume input = async {
          use buffer = new MemoryStream ()
          do! Stream.asyncCopyTo buffer input
          return buffer.ToArray () |> Encoding.UTF8.GetString }
        member __.Dispose () = consumerDisposed := true
    }
  do
    use chain = StreamTransformation.chainToResultConsumer transformation consumer
    use buffer = new MemoryStream (data, false)
    let res = StreamToResultConsumer.asyncConsume buffer chain |> Async.RunSynchronously
    Assert.Equal ("test", res)
  Assert.True !consumerDisposed
  Assert.True !transformationDisposed


[<Fact>]
let ``successfull no-close`` () =
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformationDisposed := true
    }
  let consumerDisposed = ref false
  let consumer =
    { new IStreamConsumer with
        member __.AsyncConsume input = async {
          use buffer = new MemoryStream ()
          do! Stream.asyncCopyTo buffer input
          Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString) }
        member __.Dispose () = consumerDisposed := true
    }
  do
    use chain = StreamTransformation.chainConsumer transformation consumer
    use buffer = new MemoryStream (data, false)
    StreamConsumer.asyncConsume buffer chain |> Async.RunSynchronously
  Assert.True !consumerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``failed transform after first write`` () =
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          do! Stream.asyncCopyTo output input
          do! output.AsyncFlush ()
          failwith "failed" }
        member __.Dispose () = transformationDisposed := true
    }
  let consumerDisposed = ref false
  let consumer =
    { new IStreamConsumer with
        member __.AsyncConsume input = async {
          use buffer = new MemoryStream ()
          do! Stream.asyncCopyTo buffer input
          Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString) }
        member __.Dispose () = consumerDisposed := true
    }
  do
    use chain = StreamTransformation.chainConsumer transformation consumer
    use buffer = new MemoryStream (data, false)
    Assert.Throws<Exception> (fun () -> StreamConsumer.asyncConsume buffer chain |> Async.RunSynchronously) |> ignore
  Assert.True !consumerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``failed transform before first write`` () =
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (_, _) = failwith "failed"
        member __.Dispose () = transformationDisposed := true
    }
  let consumerDisposed = ref false
  let consumer =
    { new IStreamConsumer with
        member __.AsyncConsume input = async {
          use buffer = new MemoryStream ()
          do! Stream.asyncCopyTo buffer input
          Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString) }
        member __.Dispose () = consumerDisposed := true
    }
  do
    use chain = StreamTransformation.chainConsumer transformation consumer
    use buffer = new MemoryStream (data, false)
    Assert.Throws<Exception> (fun () -> StreamConsumer.asyncConsume buffer chain |> Async.RunSynchronously) |> ignore
  Assert.True !consumerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``failed consume after read`` () =
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          do! Stream.asyncCopyTo output input
          output.Close () }
        member __.Dispose () = transformationDisposed := true
    }
  let consumerDisposed = ref false
  let consumer =
    { new IStreamConsumer with
        member __.AsyncConsume input = async {
          use buffer = new MemoryStream ()
          do! Stream.asyncCopyTo buffer input
          failwith "failed" }
        member __.Dispose () = consumerDisposed := true
    }
  do
    use chain = StreamTransformation.chainConsumer transformation consumer
    use buffer = new MemoryStream (data, false)
    Assert.Throws<Exception> (fun () -> StreamConsumer.asyncConsume buffer chain |> Async.RunSynchronously) |> ignore
  Assert.True !consumerDisposed
  Assert.True !transformationDisposed

[<Fact>]
let ``failed consume before read`` () =
  let transformationDisposed = ref false
  let transformation =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          do! Stream.asyncCopyTo output input
          output.Close () }
        member __.Dispose () = transformationDisposed := true
    }
  let consumerDisposed = ref false
  let consumer =
    { new IStreamConsumer with
        member __.AsyncConsume _ = failwith "failed"
        member __.Dispose () = consumerDisposed := true
    }
  do
    use chain = StreamConsumer.chain consumer transformation
    use buffer = new MemoryStream (data, false)
    Assert.Throws<Exception> (fun () -> StreamConsumer.asyncConsume buffer chain |> Async.RunSynchronously) |> ignore
  Assert.True !consumerDisposed
  Assert.True !transformationDisposed
