using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NCoreUtils.IO
{
    public class FailWhileWriteProducer : IStreamProducer
    {
        static readonly byte[] _seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");

        public ValueTask DisposeAsync() => default;

        public async ValueTask ProduceAsync(Stream output, CancellationToken cancellationToken = default)
        {
            await output.WriteAsync(_seed, cancellationToken);
            await output.FlushAsync(cancellationToken);
            await Task.Delay(10);
            throw new ExpectedException();
        }
    }
}