using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class JsonStreamConsumer<T> : IStreamConsumer<T?>
    {
        public JsonSerializerOptions? JsonSerializerOptions { get; }

        public JsonStreamConsumer(JsonSerializerOptions? jsonSerializerOptions = default)
            => JsonSerializerOptions = jsonSerializerOptions;

        public ValueTask<T?> ConsumeAsync(Stream input, CancellationToken cancellationToken = default)
            => JsonSerializer.DeserializeAsync<T>(input, JsonSerializerOptions, cancellationToken);

        public ValueTask DisposeAsync()
            => default;
    }
}