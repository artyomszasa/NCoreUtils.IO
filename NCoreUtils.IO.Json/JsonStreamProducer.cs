using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class JsonStreamProducer : IStreamProducer
    {
        [Obsolete("Use context or type info based overrides when possible.")]
        [RequiresUnreferencedCode("Make sure all accessible types are preserved from trimming")]
        public static JsonStreamProducer<T> Create<T>(T value, JsonSerializerOptions? jsonSerializerOptions = default)
            => new(value, jsonSerializerOptions);

        public static JsonStreamProducer<T> Create<T>(T value, JsonSerializerContext context)
            => new(value, context);

        public static TypedJsonStreamProducer<T> Create<T>(T value, JsonTypeInfo<T> typeInfo)
            => new(value, typeInfo);

        public object? Value { get; }

        public Type ValueType { get; }

        public JsonSerializerContext? Context { get; }

        public JsonSerializerOptions? JsonSerializerOptions { get; }

        public JsonStreamProducer(object? value, Type valueType, JsonSerializerContext context)
        {
            Value = value;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            Context = context;
        }

        [Obsolete("Use context or type info based overrides when possible.")]
        [RequiresUnreferencedCode("Make sure all accessible types are preserved from trimming")]
        public JsonStreamProducer(object? value, Type valueType, JsonSerializerOptions? jsonSerializerOptions = default)
        {
            Value = value;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            JsonSerializerOptions = jsonSerializerOptions;
        }

#pragma warning disable CA1816
        public ValueTask DisposeAsync()
            => default;
#pragma warning restore CA1816

#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "Warning is emitted at ctor.")]
#endif
        public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        {
            if (Context is not null)
            {
                return new ValueTask(JsonSerializer.SerializeAsync(output, Value!, ValueType, Context, cancellationToken));
            }
            return new ValueTask(JsonSerializer.SerializeAsync(output, Value!, ValueType, JsonSerializerOptions, cancellationToken));
        }
    }

    public class JsonStreamProducer<T> : JsonStreamProducer
    {
        [Obsolete("Use context based overrides when possible.")]
        [RequiresUnreferencedCode("Make sure all accessible types are preserved from trimming")]
        public JsonStreamProducer(T value, JsonSerializerOptions? jsonSerializerOptions = default)
            : base(value, typeof(T), jsonSerializerOptions)
        { }

        public JsonStreamProducer(T value, JsonSerializerContext context)
            : base(value, typeof(T), context)
        { }
    }
}