module NCoreUtils.IO.``transform-transform``

open Xunit
open System.Text
open NCoreUtils
open System.IO
open System

let private data = Encoding.UTF8.GetBytes "test"

[<Fact>]
let ``successfull`` () =
  let transformation1Disposed = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chain transformation2 transformation1
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed

[<Fact>]
let ``successfull no-close`` () =
  let transformation1Disposed = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chain transformation2 transformation1
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    chain.PerformAsync(source, buffer).Wait()
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed

[<Fact>]
let ``failed first after first write`` () =
  let transformation1Disposed = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          do! Stream.asyncCopyTo output input
          failwith "failed" }
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chain transformation2 transformation1
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> chain.AsyncPerform (source, buffer) |> Async.RunSynchronously) |> ignore
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed

[<Fact>]
let ``failed first before first write`` () =
  let transformation1Disposed = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (_, _) = failwith "failed"
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chain transformation2 transformation1
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> chain.AsyncPerform (source, buffer) |> Async.RunSynchronously) |> ignore
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed

[<Fact>]
let ``failed second after first write`` () =
  let transformation1Disposed = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          do! Stream.asyncCopyTo output input
          failwith "failed" }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chain transformation2 transformation1
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> chain.AsyncPerform (source, buffer) |> Async.RunSynchronously) |> ignore
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed

[<Fact>]
let ``failed second before first write`` () =
  let transformation1Disposed = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IStreamTransformation with
        member __.AsyncPerform (_, _) = failwith "failed"
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chain transformation2 transformation1
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> chain.AsyncPerform (source, buffer) |> Async.RunSynchronously) |> ignore
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed
