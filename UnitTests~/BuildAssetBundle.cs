using NUnit.Framework;
using UnityEditor;

namespace UnitTests
{
    /// <summary>
    /// This tests compilation error in runtime build with building asset bundle.
    /// </summary>
    public class BuildAssetBundle
    {
        [Test]
        public void Build()
        {
            BuildPipeline.BuildAssetBundles("Assets/",
                new[]
                {
                    new AssetBundleBuild
                    {
                        assetNames = new[] { "Packages/ndmf/UnitTests/Empty.prefab" },
                        assetBundleName = "asset.unity3d"
                    }
                },
                BuildAssetBundleOptions.None,
                EditorUserBuildSettings.activeBuildTarget);
        }
    }
}