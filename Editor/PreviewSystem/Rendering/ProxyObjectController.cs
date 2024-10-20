#region

using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal class ProxyObjectController : IDisposable
    {
        private readonly ProxyObjectCache _cache;
        private readonly Renderer _originalRenderer;
        private bool _setupComplete;
        private Renderer _setupProxy, _primaryProxy;
        private ProxyObjectCache.IProxyHandle _proxyHandle;

        internal Renderer Renderer => _setupComplete ? _primaryProxy : _setupProxy;

        internal RenderAspects ChangeFlags;

        internal Material[] _initialMaterials;
        internal Mesh _initialSharedMesh;
        internal ComputeContext _monitorRenderer, _monitorMaterials, _monitorMesh;

        internal ComputeContext InvalidateMonitor;

        private bool _visibilityOffOriginal;
        private bool _pickingOffOriginal, _pickingOffReplacement;
        private long _lastVisibilityCheck = long.MinValue;

        private readonly Stopwatch _lastWarning = new();
        
        private static CustomSampler _onPreFrameSampler = CustomSampler.Create("ProxyObjectController.OnPreFrame");

        public static bool IsProxyObject(GameObject obj)
        {
            return ProxyObjectCache.IsProxyObject(obj);
        }
        
        public ProxyObjectController(ProxyObjectCache cache, Renderer originalRenderer, ProxyObjectController _priorController)
        {
            _cache = cache;
            _originalRenderer = originalRenderer;
            
            SetupRendererMonitoring(originalRenderer);
            
            if (_priorController != null)
            {
                if (_priorController._monitorRenderer.IsInvalidated)
                {
                    ChangeFlags |= RenderAspects.Shapes;
                    
                    if (!_initialMaterials.SequenceEqual(_priorController._initialMaterials))
                    {
                        ChangeFlags |= RenderAspects.Material | RenderAspects.Texture;
                    }
                    
                    if (_initialSharedMesh != _priorController._initialSharedMesh)
                    {
                        ChangeFlags |= RenderAspects.Mesh;
                    }
                }

                if (_priorController._monitorMaterials.IsInvalidated)
                {
                    ChangeFlags |= RenderAspects.Material | RenderAspects.Texture;
                }
                
                if (_priorController._monitorMesh.IsInvalidated)
                {
                    ChangeFlags |= RenderAspects.Mesh;
                }
            }

            CreateReplacementObject();
        }

        private void SetupRendererMonitoring(Renderer r)
        {
            if (r == null)
            {
                InvalidateMonitor = ComputeContext.NullContext;
                return;
            }
            
            var gameObjectName = r.gameObject.name;
            _monitorRenderer = new ComputeContext("Renderer Monitor for " + gameObjectName);
            _monitorMaterials = new ComputeContext("Material Monitor for " + gameObjectName);
            _monitorMesh = new ComputeContext("Mesh Monitor for " + gameObjectName);

            _monitorRenderer.Observe(r);
            if (r is SkinnedMeshRenderer smr)
            {
                _monitorMesh.Observe(smr.sharedMesh);
                _initialSharedMesh = smr.sharedMesh;
            }
            else if (r is MeshRenderer mr)
            {
                var meshRenderer = _monitorMesh.GetComponent<MeshFilter>(r.gameObject);
                if (meshRenderer != null)
                {
                    _monitorMesh.Observe(meshRenderer.sharedMesh);
                    _initialSharedMesh = meshRenderer.sharedMesh;
                }
            }

            _initialMaterials = (Material[]) r.sharedMaterials.Clone();
            foreach (var material in r.sharedMaterials)
            {
                _monitorMaterials.Observe(material);
                if (material == null) continue;
                
                var texPropIds = material.GetTexturePropertyNameIDs();
                foreach (var texPropId in texPropIds)
                {
                    var tex = material.GetTexture(texPropId);
                    if (tex != null)
                    {
                        _monitorMaterials.Observe(tex);
                    }
                }
            }
            
            InvalidateMonitor = new ComputeContext("ProxyObjectController for " + gameObjectName);
            _monitorMesh.Invalidates(InvalidateMonitor);
            _monitorMaterials.Invalidates(InvalidateMonitor);
            _monitorRenderer.Invalidates(InvalidateMonitor);
        }
        
        internal bool OnPreFrame()
        {
            if (Renderer == null || _originalRenderer == null)
            {
                if (Renderer == null)
                {
                    if (!_lastWarning.IsRunning || _lastWarning.ElapsedMilliseconds > 1000)
                    {
                        Debug.LogWarning("Proxy object was destroyed improperly! Resetting pipeline...");
                        _lastWarning.Restart();
                    }
                }
                return false;
            }

            _onPreFrameSampler.Begin(_originalRenderer.gameObject);

            try
            {
                var target = Renderer;
                var original = _originalRenderer;

                if (VisibilityMonitor.Sequence != _lastVisibilityCheck)
                {
                    _pickingOffOriginal = SceneVisibilityManager.instance.IsPickingDisabled(original.gameObject);
                    _visibilityOffOriginal = SceneVisibilityManager.instance.IsHidden(original.gameObject);

                    var pickingOffTarget = SceneVisibilityManager.instance.IsPickingDisabled(target.gameObject);
                    if (_pickingOffOriginal != pickingOffTarget)
                    {
                        if (_pickingOffOriginal)
                        {
                            SceneVisibilityManager.instance.DisablePicking(target.gameObject, false);
                        }
                        else
                        {
                            SceneVisibilityManager.instance.EnablePicking(target.gameObject, false);
                        }
                    }

                    _lastVisibilityCheck = VisibilityMonitor.Sequence;
                }

                target.enabled = false;

                SkinnedMeshRenderer smr = null;
                if (_originalRenderer is SkinnedMeshRenderer smr_)
                {
                    smr = smr_;

                    var replacementSMR = (SkinnedMeshRenderer)Renderer;
                    replacementSMR.sharedMesh = smr_.sharedMesh;
                    replacementSMR.bones = smr_.bones;

                    target.transform.position = original.transform.position;
                    target.transform.rotation = original.transform.rotation;
                }
                else
                {
                    var originalFilter = _originalRenderer.GetComponent<MeshFilter>();
                    var filter = Renderer.GetComponent<MeshFilter>();
                    filter.sharedMesh = originalFilter.sharedMesh;

                    var shadowBone = ShadowBoneManager.Instance.GetBone(_originalRenderer.transform).proxy;

                    var rendererTransform = Renderer.transform;
                    if (shadowBone != rendererTransform.parent)
                    {
                        rendererTransform.SetParent(shadowBone, false);
                        rendererTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        rendererTransform.localScale = Vector3.one;
                    }
                }

                target.enabled = original.enabled && original.gameObject.activeInHierarchy;

                Renderer.sharedMaterials = _originalRenderer.sharedMaterials;


                target.localBounds = original.localBounds;
                if (target is SkinnedMeshRenderer targetSMR && original is SkinnedMeshRenderer originalSMR)
                {
                    targetSMR.rootBone = originalSMR.rootBone != null ? originalSMR.rootBone : originalSMR.transform;
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
            finally
            {
                _onPreFrameSampler.End();
            }
        }

        internal void FinishPreFrame(bool isSceneViewCamera)
        {
            if (Renderer != null)
            {
                var shouldEnable = Renderer.enabled & !(isSceneViewCamera && _visibilityOffOriginal);
                Mesh currentSharedMesh = null;

                if (Renderer is SkinnedMeshRenderer smr)
                    currentSharedMesh = smr.sharedMesh;
                else if (Renderer is MeshRenderer mr)
                    currentSharedMesh = mr.GetComponent<MeshFilter>().sharedMesh;

                if (currentSharedMesh != _initialSharedMesh) Renderer.enabled = false;

                Renderer.enabled = shouldEnable;
            }
        }

        private void CreateReplacementObject()
        {
            if (_originalRenderer == null) return;

            _proxyHandle = _cache.GetHandle(_originalRenderer, () =>
            {
                var replacementGameObject = new GameObject("Proxy renderer for " + _originalRenderer.gameObject.name);
                replacementGameObject.hideFlags = HideFlags.DontSave;
                SceneManager.MoveGameObjectToScene(replacementGameObject, NDMFPreviewSceneManager.GetPreviewScene());

#if MODULAR_AVATAR_DEBUG_HIDDEN
                replacementGameObject.hideFlags = HideFlags.DontSave;
#endif

                replacementGameObject.AddComponent<SelfDestructComponent>().KeepAlive = this;

                Renderer renderer;
                if (_originalRenderer is SkinnedMeshRenderer smr)
                {
                    renderer = replacementGameObject.AddComponent<SkinnedMeshRenderer>();
                }
                else if (_originalRenderer is MeshRenderer mr)
                {
                    renderer = replacementGameObject.AddComponent<MeshRenderer>();
                    replacementGameObject.AddComponent<MeshFilter>();
                }
                else
                {
                    Debug.Log("Unsupported renderer type: " + _originalRenderer.GetType());
                    Object.DestroyImmediate(replacementGameObject);
                    return null;
                }

                renderer.forceRenderingOff = true;
                
                return renderer;
            });

            _primaryProxy = _proxyHandle.PrimaryProxy;
            _setupProxy = _proxyHandle.GetSetupProxy();
        }

        public void FinishSetup()
        {
            if (_setupComplete) return;

            _setupComplete = true;
            _proxyHandle.ReturnSetupProxy(_setupProxy);
            _setupProxy = null;
        }

        public void Dispose()
        {
            if (_setupProxy != null)
            {
                _proxyHandle.ReturnSetupProxy(_setupProxy);
            }

            _setupProxy = null;
            _proxyHandle.Dispose();
        }
    }
}