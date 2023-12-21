using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public sealed class TypedJsonStreamProducer<T>(T value, JsonTypeInfo<T> typeInfo) : IStreamProducer
{
    public T Value { get; } = value;

    public JsonTypeInfo<T> TypeInfo { get; } = typeInfo;

    public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeAsync(output, Value, TypeInfo, cancellationToken));

    public ValueTask DisposeAsync()
        => default;
}