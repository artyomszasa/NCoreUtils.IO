using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NCoreUtils.IO
{
    public class TransformationTests
    {
        [Fact]
        public async Task EncryptAndDecrypt()
        {
            var enc = new EncryptTransformation();
            var dec = new DecryptTransformation();
            var seed = Encoding.ASCII.GetBytes(@"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.");
            using var input = new MemoryStream(seed, false);
            using var output = new MemoryStream();
            {
                await using var transformation = enc.Chain(dec);
                await transformation.PerformAsync(input, output);
                Assert.True(seed.SequenceEqual(output.ToArray()));
            }
            Assert.True(enc.HasStarted);
            Assert.True(enc.HasCompleted);
            Assert.Null(enc.Error);
            Assert.True(enc.HasBeenDisposed);
            Assert.True(dec.HasStarted);
            Assert.True(dec.HasCompleted);
            Assert.Null(dec.Error);
            Assert.True(dec.HasBeenDisposed);
        }
    }
}