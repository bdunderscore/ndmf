using System;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;

namespace UnitTests
{
    public class AvatarNameFilterTests
    {
        [Test]
        public void TestAvatarNameFilter()
        {
            Assert.AreEqual(
                "foo",
                AssetSaver.FilterAssetName("foo")
            );
            
            Assert.AreEqual(
                "_con",
                AssetSaver.FilterAssetName("con")
            );
            
            Assert.AreEqual(
                "_LPT4",
                AssetSaver.FilterAssetName("LPT4")
            );
            
            Assert.AreEqual(
                "_AUX.avatar",
                AssetSaver.FilterAssetName("AUX.avatar")
            );
            
            Assert.AreEqual(
                "foo_bar",
                AssetSaver.FilterAssetName("foo/bar")
            );
            
            Assert.AreEqual(
                "foo_bar_baz_quux",
                AssetSaver.FilterAssetName("foo\\bar?baz*quux")
            );
            
            Assert.AreEqual(
                "foo",
                AssetSaver.FilterAssetName(" foo")
            );
            
            Assert.AreEqual(
                "foo",
                AssetSaver.FilterAssetName("foo ")
            );
            Assert.AreEqual(
                "f",
                AssetSaver.FilterAssetName(" f ")
            );
            
            Assert.AreEqual(
                Guid.NewGuid().ToString().Length,
                AssetSaver.FilterAssetName("   ").Length
            );
            
            Assert.AreEqual(
                "fallback",
                AssetSaver.FilterAssetName("   ", "fallback")
            );
        }
    }
}