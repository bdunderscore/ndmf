using System;
using nadena.dev.ndmf;
using NUnit.Framework;

namespace UnitTests
{
    public class AvatarNameFilterTests
    {
        [Test]
        public void TestAvatarNameFilter()
        {
            Assert.AreEqual(
                "foo",
                BuildContext.FilterAvatarName("foo")
            );
            
            Assert.AreEqual(
                "_con",
                BuildContext.FilterAvatarName("con")
            );
            
            Assert.AreEqual(
                "_LPT4",
                BuildContext.FilterAvatarName("LPT4")
            );
            
            Assert.AreEqual(
                "_AUX.avatar",
                BuildContext.FilterAvatarName("AUX.avatar")
            );
            
            Assert.AreEqual(
                "foo_bar",
                BuildContext.FilterAvatarName("foo/bar")
            );
            
            Assert.AreEqual(
                "foo_bar_baz_quux",
                BuildContext.FilterAvatarName("foo\\bar?baz*quux")
            );
            
            Assert.AreEqual(
                "foo",
                BuildContext.FilterAvatarName(" foo")
            );
            
            Assert.AreEqual(
                "foo",
                BuildContext.FilterAvatarName("foo ")
            );
            Assert.AreEqual(
                "f",
                BuildContext.FilterAvatarName(" f ")
            );
            
            Assert.AreEqual(
                Guid.NewGuid().ToString().Length,
                BuildContext.FilterAvatarName("   ").Length
            );
        }
    }
}