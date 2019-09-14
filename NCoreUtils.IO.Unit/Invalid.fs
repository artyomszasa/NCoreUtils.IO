module NCoreUtils.IO.``invalid cases``

open System
open Xunit

type private DummyDisposable () =
  member val Disposed = false with get, set
  interface IDisposable with
    member this.Dispose () = this.Disposed <- true

[<Fact>]
let lazyDisposable () =
  let disposable = new DummyDisposable ()
  do
    let lazyDisposable = new LazyDisposable ()
    lazyDisposable.Set disposable
    Assert.Throws<InvalidOperationException> (fun () -> lazyDisposable.Set disposable) |> ignore
    (lazyDisposable :> IDisposable).Dispose ()
    (lazyDisposable :> IDisposable).Dispose () // reentrance
    Assert.Throws<ObjectDisposedException> (fun () -> lazyDisposable.Set disposable) |> ignore
  do
    let lazyDisposable = new LazyDisposable ()
    (lazyDisposable :> IDisposable).Dispose ()
  Assert.True disposable.Disposed
