using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public sealed class EncryptTransformation : EncryptionBase, ITraceStreamTransformation
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
                using var encryptor = enc.CreateEncryptor();
                using var encryptingStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write);
                await input.CopyToAsync(encryptingStream, 16 * 1024, cancellationToken);
                await encryptingStream.FlushAsync(cancellationToken);
            }
            catch (Exception exn)
            {
                Error = exn;
            }
            finally
            {
                HasCompleted = true;
            }
        }
    }
}