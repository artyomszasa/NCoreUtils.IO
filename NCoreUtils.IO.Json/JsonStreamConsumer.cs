using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public static class JsonStreamConsumer
    {
        public static JsonStreamConsumer<T> Create<T>(JsonTypeInfo<T> typeInfo)
            => new(typeInfo);
    }

    public class JsonStreamConsumer<T> : IStreamConsumer<T?>
    {
        public JsonTypeInfo<T>? TypeInfo { get; }

        public JsonSerializerOptions? JsonSerializerOptions { get; }

        public JsonStreamConsumer(JsonTypeInfo<T> typeInfo)
        {
            TypeInfo = typeInfo;
            JsonSerializerOptions = default;
        }

        [Obsolete("Use JsonTypeInfo based version when possible.")]
        [RequiresUnreferencedCode("Make sure all accessible types are preserved from trimming")]
        public JsonStreamConsumer(JsonSerializerOptions? jsonSerializerOptions = default)
        {
            TypeInfo = default;
            JsonSerializerOptions = jsonSerializerOptions;
        }

#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "Warning is emitted at ctor.")]
#endif
        public ValueTask<T?> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
        {
            if (TypeInfo is not null)
            {
                return JsonSerializer.DeserializeAsync(input, TypeInfo, cancellationToken);
            }
            return JsonSerializer.DeserializeAsync<T>(input, JsonSerializerOptions, cancellationToken);
        }

#pragma warning disable CA1816
        public ValueTask DisposeAsync()
            => default;
#pragma warning restore CA1816
    }
}