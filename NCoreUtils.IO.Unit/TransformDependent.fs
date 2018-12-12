module NCoreUtils.IO.``transform-dependent``

open System
open System.IO
open System.Text
open Xunit
open NCoreUtils

let private data = Encoding.UTF8.GetBytes "test"

[<Fact>]
let ``successfull`` () =
  let transformation1Disposed = ref false
  let transformation1HasRun = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          transformation1HasRun := true
          Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IDependentStreamTransformation<bool> with
        member __.AsyncPerform (input, outputFactory) = async {
          let output = outputFactory true
          do! Stream.asyncCopyTo output input }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chainDependent transformation2 (function true -> ValueSome transformation1 | false -> ValueNone)
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed
  Assert.True !transformation1HasRun

[<Fact>]
let ``successfull delegate`` () =
  let transformation1Disposed = ref false
  let transformation1HasRun = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          transformation1HasRun := true
          Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IDependentStreamTransformation<bool> with
        member __.AsyncPerform (input, outputFactory) = async {
          let output = outputFactory true
          do! Stream.asyncCopyTo output input }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain =
      StreamTransformation.chainDependentFunc transformation2
        (StreamTransformation.DependentTransformationDelegate<bool>
          (fun state transformation ->
            match state with
            | true ->
              transformation <- transformation1
              true
            | false ->
              transformation <- Unchecked.defaultof<_>
              false
          )
        )
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed
  Assert.True !transformation1HasRun


[<Fact>]
let ``successfully no-dep`` () =
  let transformation1Disposed = ref false
  let transformation1HasRun = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          transformation1HasRun := true
          Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IDependentStreamTransformation<bool> with
        member __.AsyncPerform (input, outputFactory) = async {
          let output = outputFactory false
          do! Stream.asyncCopyTo output input }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain =
      StreamTransformation.chainDependent transformation2 (function true -> ValueSome transformation1 | false -> ValueNone)
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.False !transformation1Disposed
  Assert.True !transformation2Disposed
  Assert.False !transformation1HasRun

[<Fact>]
let ``successfull delegate no-dep`` () =
  let transformation1Disposed = ref false
  let transformation1HasRun = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          transformation1HasRun := true
          Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IDependentStreamTransformation<bool> with
        member __.AsyncPerform (input, outputFactory) = async {
          let output = outputFactory false
          do! Stream.asyncCopyTo output input }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain =
      StreamTransformation.chainDependentFunc transformation2
        (StreamTransformation.DependentTransformationDelegate<bool>
          (fun state transformation ->
            match state with
            | true ->
              transformation <- transformation1
              true
            | false ->
              transformation <- Unchecked.defaultof<_>
              false
          )
        )
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously
    Assert.Equal ("test", buffer.ToArray () |> Encoding.UTF8.GetString)
  Assert.False !transformation1Disposed
  Assert.True !transformation2Disposed
  Assert.False !transformation1HasRun


[<Fact>]
let ``failed outer after write`` () =
  let transformation1Disposed = ref false
  let transformation1HasRun = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          transformation1HasRun := true
          Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IDependentStreamTransformation<bool> with
        member __.AsyncPerform (input, outputFactory) = async {
          let output = outputFactory true
          do! Stream.asyncCopyTo output input
          failwith "failed" }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain =
      StreamTransformation.chainDependent transformation2 (function true -> ValueSome transformation1 | false -> ValueNone)
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously) |> ignore
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed

[<Fact>]
let ``failed outer before write`` () =
  let transformation1Disposed = ref false
  let transformation1HasRun = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) =
          transformation1HasRun := true
          Stream.asyncCopyTo output input
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IDependentStreamTransformation<bool> with
        member __.AsyncPerform (_, outputFactory) = async {
          outputFactory true |> ignore
          failwith "failed" }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain =
      StreamTransformation.chainDependent transformation2 (function true -> ValueSome transformation1 | false -> ValueNone)
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously) |> ignore
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed

[<Fact>]
let ``failed inner after write`` () =
  let transformation1Disposed = ref false
  let transformation1HasRun = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (input, output) = async {
          transformation1HasRun := true
          do! Stream.asyncCopyTo output input
          failwith "failed" }
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IDependentStreamTransformation<bool> with
        member __.AsyncPerform (input, outputFactory) = async {
          let output = outputFactory true
          do! Stream.asyncCopyTo output input }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chainDependent transformation2 (function true -> ValueSome transformation1 | false -> ValueNone)
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously) |> ignore
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed

[<Fact>]
let ``failed inner before write`` () =
  let transformation1Disposed = ref false
  let transformation1 =
    { new IStreamTransformation with
        member __.AsyncPerform (_, _) = failwith "failed"
        member __.Dispose () = transformation1Disposed := true
    }
  let transformation2Disposed = ref false
  let transformation2 =
    { new IDependentStreamTransformation<bool> with
        member __.AsyncPerform (input, outputFactory) = async {
          let output = outputFactory true
          do! Stream.asyncCopyTo output input }
        member __.Dispose () = transformation2Disposed := true
    }
  do
    use chain = StreamTransformation.chainDependent transformation2 (function true -> ValueSome transformation1 | false -> ValueNone)
    use source = new MemoryStream (data, false)
    use buffer = new MemoryStream ()
    Assert.Throws<Exception>(fun () -> StreamTransformation.asyncPerform source buffer chain |> Async.RunSynchronously) |> ignore
  Assert.True !transformation1Disposed
  Assert.True !transformation2Disposed
