using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnitTests.LocalizationFallback
{
    public class LocalizationFallbackTest
    {
        Localizer localizer = new Localizer("en-US", () => new List<LocalizationAsset>(
            new List<LocalizationAsset>() {
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>("Packages/nadena.dev.ndmf/UnitTests/LocalizationFallback/en-us.po"),
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>("Packages/nadena.dev.ndmf/UnitTests/LocalizationFallback/pt-pt.po"),
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>("Packages/nadena.dev.ndmf/UnitTests/LocalizationFallback/pt-br.po"),
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>("Packages/nadena.dev.ndmf/UnitTests/LocalizationFallback/en-gb.po"),
                
            }
        ));
        
        [Test]
        public void TestLanguageSelection()
        {
            var originalLanguage = LanguagePrefs.Language;
            
            LanguagePrefs.Language = "en-US";
            
            Assert.AreEqual("en-us-1", localizer.GetLocalizedString("test1"));
            Assert.AreEqual("en-us-2", localizer.GetLocalizedString("test2"));
            Assert.AreEqual("en-us-3", localizer.GetLocalizedString("test3"));
            
            LanguagePrefs.Language = "pt-BR";
            
            Assert.AreEqual("pt-br-1", localizer.GetLocalizedString("test1"));
            Assert.AreEqual("en-us-2", localizer.GetLocalizedString("test2"));
            Assert.AreEqual("pt-pt-3", localizer.GetLocalizedString("test3"));
            
            LanguagePrefs.Language = "en-GB";
            
            Assert.AreEqual("en-gb-1", localizer.GetLocalizedString("test1"));
            Assert.AreEqual("en-us-2", localizer.GetLocalizedString("test2"));
            Assert.AreEqual("en-gb-3", localizer.GetLocalizedString("test3"));

            LanguagePrefs.Language = originalLanguage;
        }
    }
}