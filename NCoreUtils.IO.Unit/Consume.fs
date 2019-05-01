module NCoreUtils.IO.``consume``

open System.IO
open System.Text
open Newtonsoft.Json
open Xunit

[<CLIMutable>]
type Item = {
  X : int
  Y : string }

[<Fact>]
let ``successfull from Stream with inner object`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let stream =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
  let item = ref { X = 0; Y = "" }
  let consumer =
    StreamConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          item := jsonSerializer.Deserialize<Item> jreader
        )
      })
  StreamConsumer.asyncConsume stream consumer |> Async.RunSynchronously
  Assert.Equal (2, item.contents.X)
  Assert.Equal ("abc", item.contents.Y)

[<Fact>]
let ``successfull from Stream with inner object as Task`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let stream =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
  let item = ref { X = 0; Y = "" }
  let consumer =
    StreamConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          item := jsonSerializer.Deserialize<Item> jreader
        )
      })
  consumer.ConsumeAsync(stream).Wait ()
  Assert.Equal (2, item.contents.X)
  Assert.Equal ("abc", item.contents.Y)


[<Fact>]
let ``successfull with inner object`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let producer =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
    |> StreamProducer.ofStream
  let item = ref { X = 0; Y = "" }
  let consumer =
    StreamConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          item := jsonSerializer.Deserialize<Item> jreader
        )
      })
  StreamConsumer.asyncConsumeProducer producer consumer |> Async.RunSynchronously
  Assert.Equal (2, item.contents.X)
  Assert.Equal ("abc", item.contents.Y)

[<Fact>]
let ``successfull with inner object as Task`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let producer =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
    |> StreamProducer.ofStream
  let item = ref { X = 0; Y = "" }
  let consumer =
    StreamConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          item := jsonSerializer.Deserialize<Item> jreader
        )
      })
  consumer.ConsumeAsync(producer).Wait ()
  Assert.Equal (2, item.contents.X)
  Assert.Equal ("abc", item.contents.Y)


[<Fact>]
let ``successfull to object`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let producer =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
    |> StreamProducer.ofStream
  let consumer =
    StreamToResultConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          jsonSerializer.Deserialize<Item> jreader
        )
      })
  let item = StreamToResultConsumer.asyncConsumeProducer producer consumer |> Async.RunSynchronously
  Assert.Equal (2, item.X)
  Assert.Equal ("abc", item.Y)

[<Fact>]
let ``successfull to object as Task`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let producer =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
    |> StreamProducer.ofStream
  let consumer =
    StreamToResultConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          jsonSerializer.Deserialize<Item> jreader
        )
      })
  let item = consumer.ConsumeAsync(producer).Result
  Assert.Equal (2, item.X)
  Assert.Equal ("abc", item.Y)


[<Fact>]
let ``successfull Stream to object`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let stream =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
  let consumer =
    StreamToResultConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          jsonSerializer.Deserialize<Item> jreader
        )
      })
  let item = StreamToResultConsumer.asyncConsume stream consumer |> Async.RunSynchronously
  Assert.Equal (2, item.X)
  Assert.Equal ("abc", item.Y)

[<Fact>]
let ``successfull Stream to object as Task`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let stream =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
  let consumer =
    StreamToResultConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          jsonSerializer.Deserialize<Item> jreader
        )
      })
  let item = consumer.ConsumeAsync(stream).Result
  Assert.Equal (2, item.X)
  Assert.Equal ("abc", item.Y)


[<Fact>]
let ``successfull multiconsume asc`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let producer =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
    |> StreamProducer.ofStream
  let item0 = ref { X = 0; Y = "" }
  let item1 = ref { X = 0; Y = "" }
  let item2 = ref { X = 0; Y = "" }
  let consumer0 =
    StreamToResultConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          jsonSerializer.Deserialize<Item> jreader
        )
      })
    |> StreamToResultConsumer.BindTo item0
  let consumer1 =
    StreamConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          item1 := jsonSerializer.Deserialize<Item> jreader
        )
      })
  let consumer2 =
    StreamConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          item2 := jsonSerializer.Deserialize<Item> jreader
        )
      })
  let consumer = StreamConsumer.combine consumer0 consumer1 |> StreamConsumer.combine consumer2
  StreamConsumer.asyncConsumeProducer producer consumer |> Async.RunSynchronously
  Assert.Equal (2, item0.contents.X)
  Assert.Equal ("abc", item0.contents.Y)
  Assert.Equal (2, item1.contents.X)
  Assert.Equal ("abc", item1.contents.Y)
  Assert.Equal (2, item2.contents.X)
  Assert.Equal ("abc", item2.contents.Y)


[<Fact>]
let ``successfull multiconsume desc`` () =
  let raw = "{\"X\":2,\"Y\":\"abc\"}"
  let producer =
    let data = Encoding.ASCII.GetBytes raw
    new MemoryStream (data, 0, data.Length, false, true)
    |> StreamProducer.ofStream
  let item0 = ref { X = 0; Y = "" }
  let item1 = ref { X = 0; Y = "" }
  let item2 = ref { X = 0; Y = "" }
  let consumer0 =
    StreamToResultConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          jsonSerializer.Deserialize<Item> jreader
        )
      })
    |> StreamToResultConsumer.BindTo item0
  let consumer1 =
    StreamConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          item1 := jsonSerializer.Deserialize<Item> jreader
        )
      })
  let consumer2 =
    StreamConsumer.Of
      (fun stream -> async {
        return (
          let jsonSerializer = JsonSerializer.CreateDefault()
          use reader = new StreamReader (stream, Encoding.ASCII, false)
          use jreader = new JsonTextReader (reader)
          item2 := jsonSerializer.Deserialize<Item> jreader
        )
      })
  let consumer = StreamConsumer.combine (StreamConsumer.combine consumer0 consumer1) consumer2
  StreamConsumer.asyncConsumeProducer producer consumer |> Async.RunSynchronously
  Assert.Equal (2, item0.contents.X)
  Assert.Equal ("abc", item0.contents.Y)
  Assert.Equal (2, item1.contents.X)
  Assert.Equal ("abc", item1.contents.Y)
  Assert.Equal (2, item2.contents.X)
  Assert.Equal ("abc", item2.contents.Y)

[<Fact>]
let ``successfull multiconsume cross`` () =
  let disposed0 = ref false
  let disposed1 = ref false
  let disposed2 = ref false
  let disposed3 = ref false
  do
    let raw = "{\"X\":2,\"Y\":\"abc\"}"
    let producer =
      let data = Encoding.ASCII.GetBytes raw
      new MemoryStream (data, 0, data.Length, false, true)
      |> StreamProducer.ofStream
    let item0 = ref { X = 0; Y = "" }
    let item1 = ref { X = 0; Y = "" }
    let item2 = ref { X = 0; Y = "" }
    let item3 = ref { X = 0; Y = "" }
    let consumer0 =
      StreamToResultConsumer.Of (
        (fun stream -> async {
          return (
            let jsonSerializer = JsonSerializer.CreateDefault()
            use reader = new StreamReader (stream, Encoding.ASCII, false)
            use jreader = new JsonTextReader (reader)
            jsonSerializer.Deserialize<Item> jreader
          )
        }),
        fun () -> disposed0 := true
      )
      |> StreamToResultConsumer.BindTo item0
    let consumer1 =
      StreamConsumer.Of (
        (fun stream -> async {
          return (
            let jsonSerializer = JsonSerializer.CreateDefault()
            use reader = new StreamReader (stream, Encoding.ASCII, false)
            use jreader = new JsonTextReader (reader)
            item1 := jsonSerializer.Deserialize<Item> jreader
          )
        }),
        fun () -> disposed1 := true
      )
    let consumer2 =
      StreamConsumer.Of (
        (fun stream -> async {
          return (
            let jsonSerializer = JsonSerializer.CreateDefault()
            use reader = new StreamReader (stream, Encoding.ASCII, false)
            use jreader = new JsonTextReader (reader)
            item2 := jsonSerializer.Deserialize<Item> jreader
          )
        }),
        fun () -> disposed2 := true
      )
    let consumer3 =
      StreamConsumer.Of (
        (fun stream -> async {
          return (
            let jsonSerializer = JsonSerializer.CreateDefault()
            use reader = new StreamReader (stream, Encoding.ASCII, false)
            use jreader = new JsonTextReader (reader)
            item3 := jsonSerializer.Deserialize<Item> jreader
          )
        }),
        fun () -> disposed3 := true
      )
    use consumer = StreamConsumer.combine (StreamConsumer.combine consumer0 consumer1) (StreamConsumer.combine consumer2 consumer3)
    StreamConsumer.asyncConsumeProducer producer consumer |> Async.RunSynchronously
    Assert.Equal (2, item0.contents.X)
    Assert.Equal ("abc", item0.contents.Y)
    Assert.Equal (2, item1.contents.X)
    Assert.Equal ("abc", item1.contents.Y)
    Assert.Equal (2, item2.contents.X)
    Assert.Equal ("abc", item2.contents.Y)
    Assert.Equal (2, item3.contents.X)
    Assert.Equal ("abc", item3.contents.Y)
  Assert.True (!disposed0)
  Assert.True (!disposed1)
  Assert.True (!disposed2)
  Assert.True (!disposed3)