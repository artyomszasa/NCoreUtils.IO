using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NCoreUtils.IO
{
    public partial class StreamingTests
    {
        [Fact]
        public async Task EncryptAndDecrypt()
        {
            var seed = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce elementum nisi vel magna rhoncus, in aliquam ipsum accumsan. Phasellus efficitur lectus quis malesuada aliquet. Proin mattis sagittis magna vitae blandit. Cras vel diam sagittis, fringilla nunc vitae, vehicula mi. Nullam et auctor mi. Proin vel pharetra tortor. Donec posuere elementum risus, et aliquet magna pharetra non. Curabitur volutpat maximus sem at euismod. Fusce porta, lacus vel varius varius, lacus felis faucibus ante, fermentum sollicitudin elit neque rhoncus tortor. Aenean eget turpis consequat, luctus lorem vehicula, ullamcorper erat.";
            var enc = new EncryptTransformation();
            var dec = new DecryptTransformation();
            var prod = StreamProducer.FromString(seed, Encoding.ASCII);
            var cons = StreamConsumer.ToString(Encoding.ASCII);
            var output = await prod
                .Chain(enc)
                .Chain(dec)
                .ConsumeAsync(cons);
            Assert.Equal(seed, output);
            Assert.True(enc.HasStarted);
            Assert.True(enc.HasCompleted);
            Assert.Null(enc.Error);
            Assert.True(enc.HasBeenDisposed);
            Assert.True(dec.HasStarted);
            Assert.True(dec.HasCompleted);
            Assert.Null(dec.Error);
            Assert.True(dec.HasBeenDisposed);
        }

        [Fact]
        public async Task FailBeforeProduce()
        {
            var enc = new EncryptTransformation();
            var dec = new DecryptTransformation();
            var prod = new FailBeforeWriteProducer();
            var cons = StreamConsumer.ToString(Encoding.ASCII);
            await Assert.ThrowsAsync<ExpectedException>(() =>
            {
                return prod
                    .Chain(enc)
                    .Chain(dec)
                    .ConsumeAsync(cons);
            });
            Assert.False(enc.HasStarted);
            Assert.False(enc.HasCompleted);
            Assert.Null(enc.Error);
            Assert.True(enc.HasBeenDisposed);
            Assert.False(dec.HasStarted);
            Assert.False(dec.HasCompleted);
            Assert.Null(dec.Error);
            Assert.True(dec.HasBeenDisposed);
        }

        [Fact]
        public async Task FailWhileProduce()
        {
            var enc = new EncryptTransformation();
            var dec = new DecryptTransformation();
            var prod = new FailWhileWriteProducer();
            var cons = StreamConsumer.ToString(Encoding.ASCII);
            await Assert.ThrowsAsync<ExpectedException>(() =>
            {
                return prod
                    .Chain(enc)
                    .Chain(dec)
                    .ConsumeAsync(cons);
            });
            Assert.True(enc.HasBeenDisposed);
            Assert.True(dec.HasBeenDisposed);
        }

        [Fact]
        public async Task FailBeforeTransforming()
        {
            var enc = new EncryptTransformation();
            var dec = new DecryptTransformation();
            var prod = new FailWhileWriteProducer();
            var cons = StreamConsumer.ToString(Encoding.ASCII);
            await Assert.ThrowsAsync<ExpectedException>(() =>
            {
                return prod
                    .Chain(enc)
                    .Chain(new FailBeforeTransformTransformation())
                    .Chain(dec)
                    .ConsumeAsync(cons);
            });
            Assert.True(enc.HasBeenDisposed);
            Assert.True(dec.HasBeenDisposed);
        }

        [Fact]
        public async Task FailWhileTransforming()
        {
            var enc = new EncryptTransformation();
            var dec = new DecryptTransformation();
            var prod = new FailWhileWriteProducer();
            var cons = StreamConsumer.ToString(Encoding.ASCII);
            await Assert.ThrowsAsync<ExpectedException>(() =>
            {
                return prod
                    .Chain(enc)
                    .Chain(new FailWhileTransformTransformation())
                    .Chain(dec)
                    .ConsumeAsync(cons);
            });
            Assert.True(enc.HasBeenDisposed);
            Assert.True(dec.HasBeenDisposed);
        }
    }
}