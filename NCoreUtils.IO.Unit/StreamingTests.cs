using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NCoreUtils.IO;

[JsonSerializable(typeof(StreamingTests.Item))]
internal partial class ItemSerializerContext : JsonSerializerContext { }

public partial class StreamingTests
{
    public sealed class Item
    {
        public string? StringValue { get; set; }

        public int IntegerValue { get; set; }
    }

    private static async ValueTask OptimisticCopyToAsync(Stream source, Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int read;
            do
            {
                read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read != 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            while (read > 0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [Fact]
    public async Task String()
    {
        var seed = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.";
        var prod = StreamProducer.FromString(seed, Encoding.ASCII);
        var output = await prod.ToStringAsync(Encoding.ASCII);
        Assert.Equal(seed, output);
    }

    [Fact]
    public async Task StringDefaultEncoding()
    {
        var seed = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.";
        var prod = StreamProducer.FromString(seed);
        var output = await prod.ToStringAsync();
        Assert.Equal(seed, output);
    }

    [Fact]
    public async Task Array()
    {
        var seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");
        var prod = StreamProducer.FromArray(seed);
        var output = await prod.ToArrayAsync();
        Assert.True(seed.SequenceEqual(output));
    }

    [Fact]
    public async Task ArrayInline()
    {
        var seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");
        await using var prod = StreamProducer.FromArray(seed);
        await using var cons = StreamConsumer.Create(async (stream, ctoken) =>
        {
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, 8192, ctoken);
            return buffer.ToArray();
        });
        var output = await prod.ConsumeAsync(cons);
        Assert.True(seed.SequenceEqual(output));
    }

    [Fact]
    public async Task Memory()
    {
        var seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");
        await using var prod = StreamProducer.Delay(_ => new ValueTask<IStreamProducer>(StreamProducer.FromMemory(seed.AsMemory())));
        await using var cons = StreamConsumer.Create(async (stream, ctoken) =>
        {
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, 8192, ctoken);
            return buffer.ToArray();
        });
        var output = await prod.ConsumeAsync(cons);
        Assert.True(seed.SequenceEqual(output));
    }

    [Fact]
    public async Task MemoryInline()
    {
        var seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");
        await using var prod = StreamProducer.Delay(_ => new ValueTask<IStreamProducer>(StreamProducer.Create(async (stream, ctoken) =>
        {
            await stream.WriteAsync(seed.AsMemory(), ctoken);
        })));
        await using var cons = StreamConsumer.Create(async (stream, ctoken) =>
        {
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, 8192, ctoken);
            return buffer.ToArray();
        });
        var output = await prod.ConsumeAsync(cons);
        Assert.True(seed.SequenceEqual(output));
    }

    [Fact]
    public async Task Stream()
    {
        var seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");
        await using var prod = StreamProducer.FromStream(new MemoryStream(seed, false));
        var buffer = new MemoryStream();
        await using var cons = StreamConsumer.ToStream(buffer);
        await prod.ConsumeAsync(cons);
        Assert.True(seed.SequenceEqual(buffer.ToArray()));
    }

    [Fact]
    public async Task StreamInline()
    {
        var seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");
        await using var prod = StreamProducer.FromStream(new MemoryStream(seed, false));
        var buffer = new MemoryStream();
        await using var cons = StreamConsumer.Create(async (stream, ctoken) =>
        {
            await stream.CopyToAsync(buffer, 8192, ctoken);
        });
        await prod.ConsumeAsync(cons);
        Assert.True(seed.SequenceEqual(buffer.ToArray()));
    }

    [Fact]
    public async Task StreamDelayed()
    {
        var seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");
        await using var prod = StreamProducer.FromStream(new MemoryStream(seed, false));
        var buffer = new MemoryStream();
        await using var cons = StreamConsumer.Delay(_ => new ValueTask<IStreamConsumer>(StreamConsumer.Create((stream, ctoken) =>
        {
            return OptimisticCopyToAsync(stream, buffer, 8192, ctoken);
        })));
        await prod.ConsumeAsync(cons);
        Assert.True(seed.SequenceEqual(buffer.ToArray()));
    }

    [Fact]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
        Justification = "No dynamic dependencies.")]
    public async Task Json()
    {
        var item0 = new Item { StringValue = "xxx", IntegerValue = 2 };
        await using var prod = JsonStreamProducer.Create(item0, ItemSerializerContext.Default);
        await using var cons = new JsonStreamConsumer<Item>(ItemSerializerContext.Default.Item);
        var item1 = await prod.ConsumeAsync(cons);
        Assert.NotNull(item1);
        Assert.Equal(item0.StringValue, item1!.StringValue);
        Assert.Equal(item0.IntegerValue, item1.IntegerValue);
    }

    [Fact]
    public async Task EncryptAndDecrypt()
    {
        var seed = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.";
        var enc = new EncryptTransformation();
        var dec = new DecryptTransformation();
        var prod = StreamProducer.FromString(seed, Encoding.ASCII);
        var cons = StreamConsumer.ToString(Encoding.ASCII);
        var output = await prod
            .Chain(enc)
            .Chain(dec)
            .ConsumeAsync(cons);
        Assert.Equal(seed, output);
        Assert.True(enc.HasStarted);
        Assert.True(enc.HasCompleted);
        Assert.Null(enc.Error);
        Assert.True(enc.HasBeenDisposed);
        Assert.True(dec.HasStarted);
        Assert.True(dec.HasCompleted);
        Assert.Null(dec.Error);
        Assert.True(dec.HasBeenDisposed);
    }

    [Fact]
    public async Task EncryptAndDecryptAlternative()
    {
        var seed = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.";
        var enc = new EncryptTransformation();
        var dec = new DecryptTransformation();
        var prod = StreamProducer.FromString(seed, Encoding.ASCII);
        var cons = StreamConsumer.ToString(Encoding.ASCII);
        var output = await prod.ConsumeAsync(cons
            .Chain(dec)
            .Chain(enc)
        );
        Assert.Equal(seed, output);
        Assert.True(enc.HasStarted);
        Assert.True(enc.HasCompleted);
        Assert.Null(enc.Error);
        Assert.True(enc.HasBeenDisposed);
        Assert.True(dec.HasStarted);
        Assert.True(dec.HasCompleted);
        Assert.Null(dec.Error);
        Assert.True(dec.HasBeenDisposed);
    }

    [Fact]
    public async Task EncryptAndDecryptAlternativeWithBind()
    {
        var seed = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.";
        var enc = new EncryptTransformation();
        var dec = new DecryptTransformation();
        var prod = StreamProducer.FromString(seed, Encoding.ASCII);
        string output = string.Empty;
        var cons = StreamConsumer.ToString(Encoding.ASCII).Bind(res => output = res);
        await prod.ConsumeAsync(cons
            .Chain(dec)
            .Chain(enc)
        );
        Assert.Equal(seed, output);
        Assert.True(enc.HasStarted);
        Assert.True(enc.HasCompleted);
        Assert.Null(enc.Error);
        Assert.True(enc.HasBeenDisposed);
        Assert.True(dec.HasStarted);
        Assert.True(dec.HasCompleted);
        Assert.Null(dec.Error);
        Assert.True(dec.HasBeenDisposed);
    }

    [Fact]
    public async Task CancelDuringAsyncProduce()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var data = new byte[128 * 1024];
        var producer = StreamProducer.Create(async (output, cancellationToken) =>
        {
            while (true)
            {
                await output.WriteAsync(data.AsMemory(), cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
            }
        });
        var consumerCancellationTriggered = false;
        var consumer = StreamConsumer.Create(async (input, cancellationToken) =>
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (true)
                {
                    await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                consumerCancellationTriggered = true;
                throw;
            }
        });
        var task = Assert.ThrowsAnyAsync<OperationCanceledException>(() => PipeStreamer.StreamAsync(producer, consumer, cancellationTokenSource.Token).AsTask());
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(50));
        await task;
        Assert.True(consumerCancellationTriggered);
    }

    [Fact]
    public async Task CancelInsideAsyncProduce()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var data = new byte[128 * 1024];
        var producer = StreamProducer.Create(async (output, _) =>
        {
            while (true)
            {
                await output.WriteAsync(data.AsMemory(), cancellationTokenSource.Token);
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationTokenSource.Token);
            }
        });
        var consumerCancellationTriggered = false;
        var consumer = StreamConsumer.Create(async (input, cancellationToken) =>
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (true)
                {
                    await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                consumerCancellationTriggered = true;
                throw;
            }
        });
        var task = Assert.ThrowsAnyAsync<OperationCanceledException>(() => PipeStreamer.StreamAsync(producer, consumer, CancellationToken.None).AsTask());
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(50));
        await task;
        Assert.True(consumerCancellationTriggered);
    }

    [Fact]
    public async Task CancelInsideAsyncProduceDelayed()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var data = new byte[128 * 1024];
        var producer = StreamProducer.Create(async (output, _) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationTokenSource.Token);
        });
        var consumer = StreamConsumer.Create((input, cancellationToken) =>
        {
            Assert.True(false, "consumer should not be started until input");
            return default;
        });
        var task = Assert.ThrowsAnyAsync<OperationCanceledException>(() => PipeStreamer.StreamAsync(producer, consumer, CancellationToken.None).AsTask());
        cancellationTokenSource.Cancel();
        await task;
    }

    [Fact]
    public async Task FailBeforeProduce()
    {
        var enc = new EncryptTransformation();
        var dec = new DecryptTransformation();
        var prod = new FailBeforeWriteProducer();
        var cons = StreamConsumer.ToString(Encoding.ASCII);
        await Assert.ThrowsAsync<ExpectedException>(() =>
        {
            return prod
                .Chain(enc)
                .Chain(dec)
                .ConsumeAsync(cons)
                .AsTask();
        });
        Assert.False(enc.HasStarted);
        Assert.False(enc.HasCompleted);
        Assert.Null(enc.Error);
        Assert.True(enc.HasBeenDisposed);
        Assert.False(dec.HasStarted);
        Assert.False(dec.HasCompleted);
        Assert.Null(dec.Error);
        Assert.True(dec.HasBeenDisposed);
    }

    [Fact]
    public async Task FailWhileProduce()
    {
        var enc = new EncryptTransformation();
        var dec = new DecryptTransformation();
        var prod = new FailWhileWriteProducer();
        var cons = StreamConsumer.ToString(Encoding.ASCII);
        await Assert.ThrowsAsync<ExpectedException>(() =>
        {
            return prod
                .Chain(enc)
                .Chain(dec)
                .ConsumeAsync(cons)
                .AsTask();
        });
        Assert.True(enc.HasBeenDisposed);
        Assert.True(dec.HasBeenDisposed);
    }

    [Fact]
    public async Task FailWhileProduceAlternative()
    {
        var enc = new EncryptTransformation();
        var dec = new DecryptTransformation();
        var prod = new FailWhileWriteProducer();
        var cons = StreamConsumer.ToString(Encoding.ASCII);
        await Assert.ThrowsAsync<ExpectedException>(() =>
        {
            return prod.ConsumeAsync(cons
                .Chain(dec)
                .Chain(enc)
            ).AsTask();
        });
        Assert.True(enc.HasBeenDisposed);
        Assert.True(dec.HasBeenDisposed);
    }

    [Fact]
    public async Task FailBeforeTransforming()
    {
        var enc = new EncryptTransformation();
        var dec = new DecryptTransformation();
        var prod = new FailWhileWriteProducer();
        var cons = StreamConsumer.ToString(Encoding.ASCII);
        await Assert.ThrowsAsync<ExpectedException>(() =>
        {
            return prod
                .Chain(enc)
                .Chain(new FailBeforeTransformTransformation())
                .Chain(dec)
                .ConsumeAsync(cons)
                .AsTask();
        });
        Assert.True(enc.HasBeenDisposed);
        Assert.True(dec.HasBeenDisposed);
    }

    [Fact]
    public async Task FailWhileTransforming()
    {
        var enc = new EncryptTransformation();
        var dec = new DecryptTransformation();
        var prod = new FailWhileWriteProducer();
        var cons = StreamConsumer.ToString(Encoding.ASCII);
        await Assert.ThrowsAsync<ExpectedException>(() =>
        {
            return prod
                .Chain(enc)
                .Chain(new FailWhileTransformTransformation())
                .Chain(dec)
                .ConsumeAsync(cons)
                .AsTask();
        });
        Assert.True(enc.HasBeenDisposed);
        Assert.True(dec.HasBeenDisposed);
    }
}
