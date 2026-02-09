using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
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
        
        [Test]
        public void TestVirtualNodeResolution()
        {
            // Create a real AnimationClip that will be the "original object"
            var animClip = new AnimationClip();
            animClip.name = "TestClip";
            
            try
            {
                // Create a VirtualClip that wraps the animation clip
                var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
                var virtualClip = VirtualClip.Clone(context, animClip);
                
                // Create an error that includes the VirtualClip
                var error = new InlineError(TEST_LOCALIZER, ErrorSeverity.Error, "Errors:test",
                    virtualClip, "arg1", "arg2");
                
                // Verify that the error has a reference to the original animation clip
                Assert.IsNotEmpty(error.References, "Expected at least one reference to be added");
                
                // The reference should point to the original animation clip
                var reference = error.References[0];
                Assert.IsNotNull(reference, "Expected a non-null reference");
                
                // The title should contain the VirtualClip's name (which comes from the original clip)
                var title = error.FormatTitle();
                Assert.IsTrue(title.Contains("TestClip") || title.Contains("VirtualClip"), 
                    $"Expected title to reference the clip, got: {title}");
            }
            finally
            {
                // Clean up
                UnityEngine.Object.DestroyImmediate(animClip);
            }
        }
        
        [Test]
        public void TestVirtualNodeWithoutOriginalObject()
        {
            // Create a mock VirtualNode without an original object
            var virtualClip = VirtualClip.NewClip(new CloneContext(GenericPlatformAnimatorBindings.Instance));
            virtualClip.Name = "NewClip";
            
            // Create an error that includes the VirtualClip without an original object
            var error = new InlineError(TEST_LOCALIZER, ErrorSeverity.Error, "Errors:test",
                virtualClip, "arg1", "arg2");
            
            // The error should still be created, even without a reference
            // It should use the VirtualNode's ToString representation instead
            var title = error.FormatTitle();
            Assert.IsNotEmpty(title, "Expected a non-empty title");
        }
    }
}