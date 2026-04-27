using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public class JsonStreamProducer(object? value, Type valueType, JsonSerializerContext context) : IStreamProducer
{
    public static JsonStreamProducer<T> Create<T>(T value, JsonSerializerContext context)
        => new(value, context);

    public static TypedJsonStreamProducer<T> Create<T>(T value, JsonTypeInfo<T> typeInfo)
        => new(value, typeInfo);

    public object? Value { get; } = value;

    public Type ValueType { get; } = valueType.ThrowIfNull();

    public JsonSerializerContext Context { get; } = context;

    protected virtual ValueTask DisposeAsyncCore() => default;

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeAsync(output, Value!, ValueType, Context, cancellationToken));
}

public sealed class JsonStreamProducer<T>(T value, JsonSerializerContext context)
    : JsonStreamProducer(value, typeof(T), context)
{ }