using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.build_framework;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;

namespace UnitTests
{
    public class TestBase
    {
        private static Dictionary<System.Type, string> _scriptToDirectory = null;
        private List<GameObject> objects;

        [SetUp]
        public virtual void Setup()
        {
            if (_scriptToDirectory == null)
            {
                _scriptToDirectory = new Dictionary<System.Type, string>();
                foreach (var path in AssetDatabase.FindAssets("t:MonoScript", new string[] { "Assets/UnitTests" }))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (obj != null)
                    {
                        _scriptToDirectory.Add(obj.GetClass(), Path.GetDirectoryName(path));
                    }
                }
            }
            
            //BuildReport.Clear();
            objects = new List<GameObject>();
        }

        [TearDown]
        public virtual void Teardown()
        {
            foreach (var obj in objects)
            {
                Object.DestroyImmediate(obj);
            }

            //Util.DeleteTemporaryAssets();
        }

        protected BuildContext CreateContext(GameObject root)
        {
            return new BuildContext(root, "Assets/ZZZ_Temp"); // TODO - cleanup
        }

        protected GameObject CreateRoot(string name)
        {
            //var path = AssetDatabase.GUIDToAssetPath(MinimalAvatarGuid);
            //var go = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var go = new GameObject();
            go.name = name;
            go.AddComponent<Animator>();
            go.AddComponent<VRCAvatarDescriptor>();
            go.AddComponent<PipelineManager>();

            objects.Add(go);
            return go;
        }

        protected GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.parent = parent.transform;
            objects.Add(go);
            return go;
        }

        protected GameObject CreatePrefab(string relPath)
        {
            var prefab = LoadAsset<GameObject>(relPath);

            var go = Object.Instantiate(prefab);
            objects.Add(go);
            return go;
        }

        protected T LoadAsset<T>(string relPath) where T : UnityEngine.Object
        {
            var root = _scriptToDirectory[GetType()] + "/";
            var path = root + relPath;

            var obj = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.NotNull(obj, "Missing test asset {0}", path);

            return obj;
        }


        protected static AnimationClip findFxClip(GameObject prefab, string layerName)
        {
            var motion = findFxMotion(prefab, layerName) as AnimationClip;
            Assert.NotNull(motion);
            return motion;
        }

        protected static Motion findFxMotion(GameObject prefab, string layerName)
        {
            var layer = findFxLayer(prefab, layerName);
            var state = layer.stateMachine.states[0].state;
            Assert.NotNull(state);

            return state.motion;
        }

        protected static AnimatorState FindStateInLayer(AnimatorControllerLayer layer, string stateName)
        {
            foreach (var state in layer.stateMachine.states)
            {
                if (state.state.name == stateName) return state.state;
            }

            return null;
        }

        protected static AnimatorControllerLayer findFxLayer(GameObject prefab, string layerName)
        {
            var fx = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);

            Assert.NotNull(fx);
            var ac = fx.animatorController as AnimatorController;
            Assert.NotNull(ac);
            Assert.False(fx.isDefault);

            var layer = ac.layers.FirstOrDefault(l => l.name == layerName);
            Assert.NotNull(layer);
            return layer;
        }
    }
}