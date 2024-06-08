#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal class ProxyObjectController : IDisposable
    {
        private static HashSet<int> _proxyObjectInstanceIds = new();
        
        private readonly Renderer _originalRenderer;
        private Renderer _replacementRenderer;
        internal Renderer Renderer => _replacementRenderer;
        public bool IsValid => _originalRenderer != null && _replacementRenderer != null;

        public static bool IsProxyObject(GameObject obj)
        {
            if (obj == null) return false;

            return _proxyObjectInstanceIds.Contains(obj.GetInstanceID());
        }
        
        public ProxyObjectController(Renderer originalRenderer)
        {
            _originalRenderer = originalRenderer;

            CreateReplacementObject();
        }

        internal bool OnPreFrame()
        {
            if (_replacementRenderer == null || _originalRenderer == null)
            {
                return false;
            }

            SkinnedMeshRenderer smr = null;
            if (_originalRenderer is SkinnedMeshRenderer smr_)
            {
                smr = smr_;

                var replacementSMR = (SkinnedMeshRenderer)_replacementRenderer;
                replacementSMR.sharedMesh = smr_.sharedMesh;
                replacementSMR.bones = smr_.bones;
            }
            else
            {
                var originalFilter = _originalRenderer.GetComponent<MeshFilter>();
                var filter = _replacementRenderer.GetComponent<MeshFilter>();
                filter.sharedMesh = originalFilter.sharedMesh;
            }

            _replacementRenderer.sharedMaterials = _originalRenderer.sharedMaterials;

            var target = _replacementRenderer;
            var original = _originalRenderer;

            if (target.gameObject.scene != original.gameObject.scene &&
                original.gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(target.gameObject, original.gameObject.scene);
            }

            target.transform.position = original.transform.position;
            target.transform.rotation = original.transform.rotation;

            target.localBounds = original.localBounds;
            if (target is SkinnedMeshRenderer targetSMR && original is SkinnedMeshRenderer originalSMR)
            {
                targetSMR.rootBone = originalSMR.rootBone;
                targetSMR.quality = originalSMR.quality;

                if (targetSMR.sharedMesh != null)
                {
                    var blendShapeCount = targetSMR.sharedMesh.blendShapeCount;
                    for (var i = 0; i < blendShapeCount; i++)
                    {
                        targetSMR.SetBlendShapeWeight(i, originalSMR.GetBlendShapeWeight(i));
                    }
                }
            }

            target.shadowCastingMode = original.shadowCastingMode;
            target.receiveShadows = original.receiveShadows;
            target.lightProbeUsage = original.lightProbeUsage;
            target.reflectionProbeUsage = original.reflectionProbeUsage;
            target.probeAnchor = original.probeAnchor;
            target.motionVectorGenerationMode = original.motionVectorGenerationMode;
            target.allowOcclusionWhenDynamic = original.allowOcclusionWhenDynamic;

            return true;
        }

        private bool CreateReplacementObject()
        {
            if (_originalRenderer == null) return false;
            
            var replacementGameObject = new GameObject("Proxy renderer for " + _originalRenderer.gameObject.name);
            _proxyObjectInstanceIds.Add(replacementGameObject.GetInstanceID());
            replacementGameObject.hideFlags = HideFlags.DontSave;

#if MODULAR_AVATAR_DEBUG_HIDDEN
            replacementGameObject.hideFlags = HideFlags.DontSave;
#endif

            replacementGameObject.AddComponent<SelfDestructComponent>().KeepAlive = this;

            if (_originalRenderer is SkinnedMeshRenderer smr)
            {
                _replacementRenderer = replacementGameObject.AddComponent<SkinnedMeshRenderer>();
            }
            else if (_originalRenderer is MeshRenderer mr)
            {
                _replacementRenderer = replacementGameObject.AddComponent<MeshRenderer>();
                replacementGameObject.AddComponent<MeshFilter>();
            }
            else
            {
                Debug.Log("Unsupported renderer type: " + _replacementRenderer.GetType());
                Object.DestroyImmediate(replacementGameObject);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (_replacementRenderer != null)
            {
                _proxyObjectInstanceIds.Remove(_replacementRenderer.gameObject.GetInstanceID());
                Object.DestroyImmediate(_replacementRenderer.gameObject);
                _replacementRenderer = null;
            }
        }
    }
}