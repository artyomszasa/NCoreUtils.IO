using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public sealed class DecryptTransformation : EncryptionBase, ITraceStreamTransformation
    {
        public bool HasStarted { get; private set; }

        public bool HasCompleted { get; private set; }

        public Exception? Error { get; private set; }

        public bool HasBeenDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            HasBeenDisposed = true;
            return default;
        }

        public async ValueTask PerformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
        {
            HasStarted = true;
            try
            {
                using var enc = Create();
                using var decryptor = enc.CreateDecryptor();
                using var decryptingStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
                await decryptingStream.CopyToAsync(output, 16 * 1024, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            catch (Exception exn)
            {
                Error = exn;
                throw;
            }
            finally
            {
                HasCompleted = true;
            }
        }
    }
}