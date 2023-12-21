using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#pragma warning disable CS0618

namespace NCoreUtils.IO;

[JsonSerializable(typeof(int))]
internal partial class TestSerializerContext : JsonSerializerContext { }

public class ArgumentValidationTests
{
    private sealed class DummyTransformation : IStreamTransformation
    {
        public ValueTask DisposeAsync()
            => default;

        public ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
            => new(input.CopyToAsync(output, -1, cancellationToken));
    }

    [Fact]
    public void Consumer()
    {
        Assert.Equal("target", Assert.Throws<ArgumentNullException>(() => StreamConsumer.ToStream(default!)).ParamName);
        Assert.Equal("consume", Assert.Throws<ArgumentNullException>(() => StreamConsumer.Create(default!)).ParamName);
        Assert.Equal("consume", Assert.Throws<ArgumentNullException>(() => StreamConsumer.Create<int>(default!)).ParamName);
        Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => StreamConsumer.Delay(default!)).ParamName);
    }

    [Fact]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
        Justification = "Dummy")]
    public void Producer()
    {
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => StreamProducer.FromStream(default!)).ParamName);
        Assert.Equal("data", Assert.Throws<ArgumentNullException>(() => StreamProducer.FromArray(default!)).ParamName);
        Assert.Equal("input", Assert.Throws<ArgumentNullException>(() => StreamProducer.FromString(default!)).ParamName);
        Assert.Equal("produce", Assert.Throws<ArgumentNullException>(() => StreamProducer.Create(default!)).ParamName);
        Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => StreamProducer.Delay(default!)).ParamName);
        Assert.Equal("valueType", Assert.Throws<ArgumentNullException>(() => new JsonStreamProducer(default, default!, TestSerializerContext.Default)).ParamName);
    }

    [Fact]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
        Justification = "Dummy")]
    public void Streamer()
    {
        Assert.Equal("consumer", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Bind<int>(default!, _ => { })).ParamName);
        Assert.Equal("store", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Bind<int>(new JsonStreamConsumer<int>(TestSerializerContext.Default.Int32), default!)).ParamName);
        Assert.Equal("producer", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Chain(default(IStreamProducer)!, new DummyTransformation())).ParamName);
        Assert.Equal("transformation", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Chain(StreamProducer.FromString(string.Empty), default!)).ParamName);
        Assert.Equal("first", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Chain(default(IStreamTransformation)!, new DummyTransformation())).ParamName);
        Assert.Equal("second", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Chain(new DummyTransformation(), default!)).ParamName);
        Assert.Equal("consumer", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Chain(default(IStreamConsumer)!, new DummyTransformation())).ParamName);
        Assert.Equal("transformation", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Chain(StreamConsumer.ToStream(new MemoryStream()), default!)).ParamName);
        Assert.Equal("consumer", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Chain(default(IStreamConsumer<string>)!, new DummyTransformation())).ParamName);
        Assert.Equal("transformation", Assert.Throws<ArgumentNullException>(() => PipeStreamer.Chain(StreamConsumer.ToString(), default!)).ParamName);

        Assert.Equal("producer", Assert.ThrowsAsync<ArgumentNullException>(() => PipeStreamer.StreamAsync(default!, StreamConsumer.ToStream(new MemoryStream())).AsTask()).Result.ParamName);
        Assert.Equal("consumer", Assert.ThrowsAsync<ArgumentNullException>(() => PipeStreamer.StreamAsync(StreamProducer.FromString(string.Empty), default!).AsTask()).Result.ParamName);
    }
}

#pragma warning restore CS0618