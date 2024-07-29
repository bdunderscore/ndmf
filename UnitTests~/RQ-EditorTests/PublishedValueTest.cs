using nadena.dev.ndmf.preview;
using NUnit.Framework;

namespace UnitTests.EditorTests
{
    public class PublishedValueTest
    {
        [Test]
        public void BasicObserve()
        {
            PublishedValue<string> val = new PublishedValue<string>("foo");
            ComputeContext ctx = new ComputeContext();
            
            string observed = ctx.Observe(val);
            Assert.AreEqual("foo", observed);
            
            Assert.IsFalse(ctx.IsInvalidated);
            val.Value = observed; // no-op
            Assert.IsFalse(ctx.IsInvalidated);
            val.Value = "bar";
            Assert.IsTrue(ctx.IsInvalidated);
        }

        [Test]
        public void ObserveWithExtract()
        {
            PublishedValue<string> val = new PublishedValue<string>("foo");
            ComputeContext ctx = new ComputeContext();
            
            int len = ctx.Observe(val, s => s.Length);
            Assert.AreEqual(3, len);
            
            Assert.IsFalse(ctx.IsInvalidated);
            val.Value = "foo"; // no-op
            Assert.IsFalse(ctx.IsInvalidated);
            val.Value = "bar";
            Assert.IsFalse(ctx.IsInvalidated);
            val.Value = "quux";
            Assert.IsTrue(ctx.IsInvalidated);
        }

        [Test]
        public void ObserveWithExtractAndEquals()
        {
            PublishedValue<string> val = new PublishedValue<string>("foo");
            ComputeContext ctx = new ComputeContext();

            string observed = ctx.Observe(val, a => a, (a, b) => a.ToLowerInvariant().Equals(b.ToLowerInvariant()));
            Assert.AreEqual("foo", observed);
            
            Assert.IsFalse(ctx.IsInvalidated);
            val.Value = "Foo";
            Assert.IsFalse(ctx.IsInvalidated);
            val.Value = "bar";
            Assert.IsTrue(ctx.IsInvalidated);
        }
    }
}