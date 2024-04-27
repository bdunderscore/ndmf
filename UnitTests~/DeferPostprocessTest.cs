using System.Collections.Generic;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests
{
    public class DeferPostprocessTest : TestBase
    {
        [Test]
        public void Test()
        {
            var root = CreateRoot("root");
            
            var meshRenderer = root.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Standard"));
            var tex = new Texture2D(1, 1);
            var tex2 = new Texture2D(1, 1);
            
            material.mainTexture = tex;
            meshRenderer.material = material;

            var log = new List<string>();
            
            BuildContext bc = CreateContext(root);
            bc.DeferPostprocessAsset(tex, obj =>
            {
                Assert.AreSame(tex, obj);
                log.Add("1");
            });
            bc.DeferPostprocessAsset(tex2, _ =>
            {
                log.Add("2");
            });
            bc.DeferPostprocessAsset(material, obj =>
            {
                Assert.AreSame(material, obj);
                log.Add("3");
            });
            bc.DeferPostprocessAsset(tex, _ =>
            {
                log.Add("4");
            });
            
            bc.SerializeInternal();
            
            Assert.AreEqual(new List<string> { "1", "3", "4" }, log);
        }
    }
}