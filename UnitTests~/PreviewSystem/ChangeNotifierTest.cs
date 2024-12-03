using System.Collections;
using System.Collections.Generic;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnitTests
{
    public class ChangeNotifierTest : TestBase
    {
        [Test]
        public void WhenAssetReimported_InvalidatesListeners()
        {
            var path = "Assets/ChangeNotifierTest.txt";
            using (var f = System.IO.File.CreateText(path))
            {
                f.WriteLine("Hello, world!");
            }
            
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);

            var ctx = new ComputeContext("test");
            ctx.Observe(asset);
            
            using (var f = System.IO.File.CreateText(path))
            {
                f.WriteLine("Goodbye, world!");
            }
            
            Assert.IsFalse(ctx.IsInvalidated);
            
            AssetDatabase.Refresh();

            Assert.IsTrue(ctx.IsInvalidated);

            ctx = new ComputeContext("test");
            ctx.Observe(asset);
            
            AssetDatabase.DeleteAsset(path);
            
            Assert.IsTrue(ctx.IsInvalidated);
        }
    }
}