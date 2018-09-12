namespace Sazzy.Tests
{
    using System;
    using NUnit.Framework;
    using Sazzy;

    public class HttpContentStreamTests
    {
        [Test]
        public void OpenWithNullStream()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                HttpContentStream.Open(null));
            Assert.That(e.ParamName, Is.EqualTo("input"));
        }
    }
}
