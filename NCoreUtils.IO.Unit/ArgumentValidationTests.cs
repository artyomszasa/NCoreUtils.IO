using System;
using Xunit;

namespace NCoreUtils.IO
{
    public class ArgumentValidationTests
    {
        [Fact]
        public void Consumer()
        {
            Assert.Equal("target", Assert.Throws<ArgumentNullException>(() => StreamConsumer.ToStream(default!)).ParamName);
            Assert.Equal("consume", Assert.Throws<ArgumentNullException>(() => StreamConsumer.Create(default!)).ParamName);
            Assert.Equal("consume", Assert.Throws<ArgumentNullException>(() => StreamConsumer.Create<int>(default!)).ParamName);
            Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => StreamConsumer.Delay(default!)).ParamName);
        }

        [Fact]
        public void Producer()
        {
            Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => StreamProducer.FromStream(default!)).ParamName);
            Assert.Equal("data", Assert.Throws<ArgumentNullException>(() => StreamProducer.FromArray(default!)).ParamName);
            Assert.Equal("input", Assert.Throws<ArgumentNullException>(() => StreamProducer.FromString(default!)).ParamName);
            Assert.Equal("produce", Assert.Throws<ArgumentNullException>(() => StreamProducer.Create(default!)).ParamName);
            Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => StreamProducer.Delay(default!)).ParamName);
            Assert.Equal("valueType", Assert.Throws<ArgumentNullException>(() => new JsonStreamProducer(default, default!)).ParamName);
        }
    }
}