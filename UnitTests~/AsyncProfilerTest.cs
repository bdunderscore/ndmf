using System;
using System.Reflection;
using nadena.dev.ndmf;
using NUnit.Framework;

namespace UnitTests
{
    public class AsyncProfilerTest
    {
        [Test]
        public void NDMF0024_DisposingNestedScopeRestoresParentFrame()
        {
            var currentFrameField = typeof(AsyncProfiler).GetField("_currentFrame", BindingFlags.Static | BindingFlags.NonPublic);
            var currentFrame = currentFrameField.GetValue(null);
            var valueProperty = currentFrame.GetType().GetProperty("Value");

            using (AsyncProfiler.PushProfilerContext("parent"))
            {
                var child = AsyncProfiler.PushProfilerContext("child");
                child.Dispose();

                var restoredFrame = valueProperty.GetValue(currentFrame);
                var contextField = restoredFrame.GetType().GetField("Context");
                Assert.AreEqual("parent", contextField.GetValue(restoredFrame));
            }
        }
    }
}
