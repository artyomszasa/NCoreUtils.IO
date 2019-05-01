module NCoreUtils.IO.``produce``

open System.IO
open System.Text
open Xunit

[<Fact>]
let ``from array`` () =
  let data = Array.init 200 byte
  use producer = StreamProducer.ofArray data
  use stream = new MemoryStream ()
  producer.ProduceAsync(stream).Wait ()
  Seq.forall2 (=) data (stream.ToArray ())

[<Fact>]
let ``from string`` () =
  let utf8 = UTF8Encoding false
  let data = "qwertzuiopasdfghjklő"
  use producer = StreamProducer.ofString data utf8
  use stream = new MemoryStream ()
  producer.ProduceAsync(stream).Wait ()
  Assert.Equal (data, utf8.GetString (stream.ToArray ()))