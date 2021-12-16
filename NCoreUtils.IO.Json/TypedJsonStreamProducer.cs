using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public sealed class TypedJsonStreamProducer<T> : IStreamProducer
{
    public T Value { get; }

    public JsonTypeInfo<T> TypeInfo { get; }

    public TypedJsonStreamProducer(T value, JsonTypeInfo<T> typeInfo)
    {
        Value = value;
        TypeInfo = typeInfo;
    }

    public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeAsync(output, Value, TypeInfo, cancellationToken));

    public ValueTask DisposeAsync()
        => default;
}