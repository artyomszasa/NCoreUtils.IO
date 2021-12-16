using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class FailWhileWriteProducer : IStreamProducer
    {
        static readonly byte[] _seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");

#pragma warning disable CA1816
        public ValueTask DisposeAsync() => default;
#pragma warning restore CA1816

        public async ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        {
            await output.WriteAsync(_seed, cancellationToken);
            await output.FlushAsync(cancellationToken);
#pragma warning disable CA2016
            await Task.Delay(10);
#pragma warning restore CA2016
            throw new ExpectedException();
        }
    }
}