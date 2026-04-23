using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO;

public static class JsonStreamConsumer
{
    public static JsonStreamConsumer<T> Create<T>(JsonTypeInfo<T> typeInfo)
        => new(typeInfo);
}

public class JsonStreamConsumer<T>(JsonTypeInfo<T> typeInfo) : IStreamConsumer<T?>
{
    public JsonTypeInfo<T> TypeInfo { get; } = typeInfo.ThrowIfNull();

    public ValueTask<T?> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
        => JsonSerializer.DeserializeAsync(input, TypeInfo, cancellationToken);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return default;
    }
}