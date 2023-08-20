using nadena.dev.build_framework;
using nadena.dev.build_framework.animation;
using NUnit.Framework;

namespace UnitTests
{
    using UnityObject = UnityEngine.Object;
    
    public class TrackObjectRenamesContextTests : TestBase
    {
        [Test]
        public void testBasicContextInitialization()
        {
            var av = CreateRoot("root");

            var bc = CreateContext(av);
            var toc = new TrackObjectRenamesContext();
            
            toc.OnActivate(bc);
            toc.OnDeactivate(bc);
        }
        
        
        [Test]
        public void TracksSimpleRenames()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");

            var toc = new TrackObjectRenamesContext();
            toc.OnActivate(CreateContext(root));
            Assert.AreEqual("a", toc.MapPath("a"));
            a.name = "b";
            toc.ClearCache();
            Assert.AreEqual("b", toc.MapPath("a"));
        }

        [Test]
        public void TracksObjectMoves()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(root, "b");

            var toc = new TrackObjectRenamesContext();
            toc.OnActivate(CreateContext(root));
            
            Assert.AreEqual("a", toc.MapPath("a"));
            a.transform.parent = b.transform;
            toc.ClearCache();
            Assert.AreEqual("b/a", toc.MapPath("a"));
        }

        [Test]
        public void TracksCollapses()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            var c = CreateChild(b, "c");

            var toc = new TrackObjectRenamesContext();
            toc.OnActivate(CreateContext(root));
            
            toc.MarkRemoved(b);
            c.transform.parent = a.transform;
            UnityObject.DestroyImmediate(b);

            Assert.AreEqual("a/c", toc.MapPath("a/b/c"));
        }

        [Test]
        public void TransformLookthrough()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            var c = CreateChild(b, "c");
            var d = CreateChild(c, "d");

            var toc = new TrackObjectRenamesContext();
            toc.OnActivate(CreateContext(root));
            
            toc.MarkTransformLookthrough(b);
            toc.MarkTransformLookthrough(c);
            Assert.AreEqual("a/b/c", toc.MapPath("a/b/c"));
            Assert.AreEqual("a", toc.MapPath("a/b/c", true));
            Assert.AreEqual("a/b/c/d", toc.MapPath("a/b/c/d", true));
        }
    }
}