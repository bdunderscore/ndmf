using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnitTests
{
    public class InlineErrorTests
    {
        private Localizer TEST_LOCALIZER = new Localizer("en-US", () => new List<LocalizationAsset>()
        {
            AssetDatabase.LoadAssetAtPath<LocalizationAsset>("Packages/nadena.dev.ndmf/UnitTests/InlineErrorAsset.po")
        });

        class CustomContext : IErrorContext
        {
            public List<ObjectReference> References = new List<ObjectReference>();
            public IEnumerable<ObjectReference> ContextReferences => References;
        }
        
        [Test]
        public void TestInlineError()
        {
            var error = new InlineError(TEST_LOCALIZER, ErrorSeverity.Error, "Errors:test", "arg0", "arg1", "arg2");
            
            Assert.AreEqual("Test error arg0", error.FormatTitle());
            Assert.AreEqual("Test error description arg1", error.FormatDetails());
            Assert.AreEqual("Test error hint arg2", error.FormatHint());
        }
        
        [Test]
        public void NDMF0020_DestroyedUnityObjectFormatsAsMissing()
        {
            var destroyedObject = new GameObject("destroyed");
            try
            {
                UnityEngine.Object.DestroyImmediate(destroyedObject);

                var error = new InlineError(TEST_LOCALIZER, ErrorSeverity.Error, "Errors:test", destroyedObject);

                Assert.AreEqual("Test error <missing>", error.FormatTitle());
            }
            finally
            {
                if (destroyedObject != null) UnityEngine.Object.DestroyImmediate(destroyedObject);
            }
        }

        [Test]
        public void NDMF0021_ErrorReportResolvesAvatarFromItsOriginatingAdditiveScene()
        {
            var activeScenePath = "Assets/NDMF0021_ActiveScene.unity";
            var activeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(activeScene, activeScenePath);
            var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.IsTrue(SceneManager.SetActiveScene(activeScene));
            var avatar = new GameObject("test avatar");
            SceneManager.MoveGameObjectToScene(avatar, testScene);

            try
            {
                Assert.AreNotEqual(testScene, SceneManager.GetActiveScene());

                var report = ErrorReport.Create(avatar, false);
                Assert.IsTrue(report.TryResolveAvatar(out var resolvedAvatar));
                Assert.AreSame(avatar, resolvedAvatar);
            }
            finally
            {
                if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
                EditorSceneManager.CloseScene(testScene, true);
                AssetDatabase.DeleteAsset(activeScenePath);
            }
        }

        [Test]
        public void NDMF0022_ResolveEmptyReferencePathToAvatarRoot()
        {
            var avatar = new GameObject("avatar");
            try
            {
                using (new ObjectRegistryScope(new ObjectRegistry(avatar.transform)))
                {
                    var reference = ObjectRegistry.GetReference(avatar);
                    var report = ErrorReport.Create(avatar, false);

                    Assert.AreEqual("", reference.Path);
                    Assert.IsTrue(reference.TryResolve(report, out var resolvedObject));
                    Assert.AreSame(avatar, resolvedObject);
                }
            }
            finally
            {
                ErrorReport.Clear();
                if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
            }
        }
        [Test]
        public void TestEnumerableExpansion()
        {
            var or1 = new ObjectReference(null, "a");
            var or2 = new ObjectReference(null, "b");
            var or3 = new ObjectReference(null, "c");
            
            var error = new InlineError(TEST_LOCALIZER, ErrorSeverity.Error, "Errors:test2",
                "arg0",
                new object[]
                {
                    "arg1",
                    new CustomContext()
                    {
                        References = new List<ObjectReference>()
                        {
                            or1,
                        }
                    },
                    or2,
                    or3
                });
            
            Assert.AreEqual("Test error arg0", error.FormatTitle());
            Assert.AreEqual("Test error description arg1", error.FormatDetails());
            Assert.AreEqual("Test error hint a b", error.FormatHint());
        }
    }
}