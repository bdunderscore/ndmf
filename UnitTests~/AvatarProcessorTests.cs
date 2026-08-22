#nullable enable

using System;
using System.Linq;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnitTests
{
    public class AvatarProcessorTests : TestBase
    {
        [Test]
        public void NDMF0005ManualProcessDestroysCloneWhenContextCreationFails()
        {
            var source = TrackObject(new GameObject($"NDMF-0005-{Guid.NewGuid():N}"));
            source.tag = "EditorOnly";
            var cloneName = source.name + "(Clone)";

            try
            {
                Assert.That(
                    () => AvatarProcessor.ManualProcessAvatar(source),
                    Throws.TypeOf<Exception>()
                );

                Assert.That(
                    Resources.FindObjectsOfTypeAll<GameObject>()
                        .Where(obj => obj != source && obj.name == cloneName),
                    Is.Empty
                );
            }
            finally
            {
                foreach (var clone in Resources.FindObjectsOfTypeAll<GameObject>()
                             .Where(obj => obj != source && obj.name == cloneName)
                             .ToArray())
                {
                    Object.DestroyImmediate(clone);
                }
            }
        }
    }
}
