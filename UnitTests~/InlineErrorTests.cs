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