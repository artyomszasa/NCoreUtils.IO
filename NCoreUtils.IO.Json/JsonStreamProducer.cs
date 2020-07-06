using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class JsonStreamProducer : IStreamProducer
    {
        public static JsonStreamProducer<T> Create<T>(T value, JsonSerializerOptions? jsonSerializerOptions = default)
            => new JsonStreamProducer<T>(value, jsonSerializerOptions);

        public object? Value { get; }

        public Type ValueType { get; }

        public JsonSerializerOptions? JsonSerializerOptions { get; }

        public JsonStreamProducer(object? value, Type valueType, JsonSerializerOptions? jsonSerializerOptions = default)
        {
            Value = value;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            JsonSerializerOptions = jsonSerializerOptions;
        }

        public ValueTask DisposeAsync()
            => default;

        public ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
            => new ValueTask(JsonSerializer.SerializeAsync(output, Value!, ValueType, JsonSerializerOptions, cancellationToken));
    }

    public class JsonStreamProducer<T> : JsonStreamProducer
    {
        public JsonStreamProducer(T value, JsonSerializerOptions? jsonSerializerOptions = default)
            : base(value, typeof(T), jsonSerializerOptions)
        { }
    }
}