#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.reporting;
using nadena.dev.ndmf.runtime.components;
using nadena.dev.ndmf.ui;
using nadena.dev.ndmf.util;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using UnityObject = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf
{
    internal sealed class ExecutionScope : IDisposable
    {
        private readonly ErrorReportScope _errorReportScope;
        [CanBeNull] private readonly ObjectRegistryScope _objectRegistryScope;

        public ExecutionScope(BuildContext ctx)
        {
            _errorReportScope = new ErrorReportScope(ctx._report);
            _objectRegistryScope =
                ObjectRegistry.ActiveRegistry != null ? null : new ObjectRegistryScope(ctx._registry);
        }

        public void Dispose()
        {
            _errorReportScope.Dispose();
            _objectRegistryScope?.Dispose();
        }
    }

    /// <summary>
    /// The BuildContext is passed to all plugins during the build process. It provides access to the avatar being
    /// built, as well as various other context information.
    /// </summary>
    [PublicAPI]
    public sealed partial class BuildContext
    {
        private readonly GameObject _avatarRootObject;
        private readonly Transform _avatarRootTransform;

        private Stopwatch sw = new Stopwatch();
        internal readonly ObjectRegistry _registry;
        internal readonly ErrorReport _report;

        public INDMFPlatformProvider PlatformProvider { get; }
        
        public ObjectRegistry ObjectRegistry => _registry;
        public ErrorReport ErrorReport => _report;

        /// <summary>
        /// The root GameObject of the avatar being built.
        /// </summary>
        public GameObject AvatarRootObject => _avatarRootObject;

        /// <summary>
        /// The root Transform of the avatar being built.
        /// </summary>
        public Transform AvatarRootTransform => _avatarRootTransform;

        /// <summary>
        /// An asset container that can be used to store generated assets. NDMF will automatically add any objects
        /// referenced by the avatar to this container when the build completes, but in some cases it can be necessary
        /// to manually save assets (e.g. when using AnimatorController builtins).
        /// </summary>
        public UnityObject AssetContainer => AssetSaver.CurrentContainer;

        public IAssetSaver AssetSaver { get; }
        
        public bool Successful => !_report.Errors.Any(e => e.TheError.Severity >= ErrorSeverity.Error);

        private Dictionary<Type, object> _state = new Dictionary<Type, object>();
        private Dictionary<Type, IExtensionContext> _extensions = new Dictionary<Type, IExtensionContext>();
        private readonly HashSet<Type> _availableExtensions = new();
        private Dictionary<Type, IExtensionContext> _initializedExtensions = new Dictionary<Type, IExtensionContext>();

        public T GetState<T>() where T : new()
        {
            return GetState(_ => new T());
        }

        public T GetState<T>(Func<BuildContext, T> init)
        {
            if (_state.TryGetValue(typeof(T), out var value))
            {
                return (T)value;
            }

            value = init(this);
            _state[typeof(T)] = value;
            return (T)value;
        }

        public T Extension<T>() where T : IExtensionContext
        {
            if (!_availableExtensions.Contains(typeof(T)))
            {
                throw new Exception($"Extension {typeof(T)} not active");
            }

            return ActivateExtensionContext<T>();
        }
        
        public BuildContext(GameObject obj, string assetRootPath, bool isClone = true)
            : this(obj, assetRootPath, null, isClone) 
        {}
        
        public BuildContext(GameObject obj, string assetRootPath, INDMFPlatformProvider platform, bool isClone = true)
        {
            platform ??= AmbientPlatform.DefaultPlatform;
            using var _platformScope = new AmbientPlatform.Scope(platform);
            
            if (!obj.GetComponent<NDMFAvatarRoot>())
            {
                obj.AddComponent<NDMFAvatarRoot>();
            }

            BuildEvent.Dispatch(new BuildEvent.BuildStarted(obj));
            _registry = new ObjectRegistry(obj.transform);
            _report = ErrorReport.Create(obj, isClone);
            if (obj.CompareTag("EditorOnly"))
            {
                var error = new InlineError(
                    NDMFLocales.L, ErrorSeverity.Error,
                    "Errors:EditorOnlyAvatarRoot"
                );
                error.AddReference(((IObjectRegistry)_registry).GetReference(obj));
                _report.AddError(error);
                ErrorReportWindow.ShowErrorReportWindow();

                throw new Exception(
                    "Can't process an avatar whose root object is marked as EditorOnly");
            }

            Debug.Log("Starting processing for avatar: " + obj.name);
            sw.Start();
            
            PlatformProvider = platform;

            _avatarRootObject = obj;
            _avatarRootTransform = obj.transform;

#if NDMF_VRCSDK3_AVATARS
            PlatformInit();
#endif

            var avatarName = _avatarRootObject.name;
            
            if (assetRootPath != null)
            {
                AssetSaver = new AssetSaver(assetRootPath, avatarName);
            }
            else
            {
                AssetSaver = new NullAssetSaver();
            }

            // Ensure that no prefab instances remain somehow
            foreach (Transform t in _avatarRootTransform.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.IsPartOfAnyPrefab(t))
                {
                    throw new Exception("Can't process an avatar that contains prefab instances/assets");
                }
            }

            sw.Stop();

            // Register all initially-existing GameObjects and Components
            using (new ObjectRegistryScope(_registry))
            {
                foreach (Transform xform in _avatarRootTransform.GetComponentsInChildren<Transform>(true))
                {
                    ObjectRegistry.GetReference(xform.gameObject);

                    foreach (Component c in xform.gameObject.GetComponents<Component>())
                    {
                        ObjectRegistry.GetReference(c);
                    }
                }
            }
        }

        public SerializationScope OpenSerializationScope()
        {
            return new SerializationScope(AssetSaver);
        }

        public bool IsTemporaryAsset(UnityObject obj)
        {
            return AssetSaver.IsTemporaryAsset(obj);
        }

        public void Serialize()
        {
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(AssetContainer)))
            {
                return; // unit tests with no serialized assets
            }

            Profiler.BeginSample("BuildContext.Serialize");

            try
            {
                HashSet<UnityObject> _savedObjects = new HashSet<UnityObject>(AssetSaver.GetPersistedAssets());

                _savedObjects.Remove(AssetContainer);

                int index = 0;
                var assetsToSave = new List<UnityObject>();
                foreach (var asset in _avatarRootObject.ReferencedAssets(traverseSaved: true, includeScene: false))
                {
                    if (asset is MonoScript)
                    {
                        // MonoScripts aren't considered to be a Main or Sub-asset, but they can't be added to asset
                        // containers either.
                        continue;
                    }

                    if (_savedObjects.Contains(asset))
                    {
                        _savedObjects.Remove(asset);
                        continue;
                    }

                    if (asset == null)
                    {
                        Debug.Log($"Asset {index} is null");
                    }

                    index++;

                    if (!EditorUtility.IsPersistent(asset))
                    {
                        try
                        {
                            assetsToSave.Add(asset);
                        }
                        catch (UnityException ex)
                        {
                            Debug.Log(
                                $"Error adding asset {asset} p={AssetDatabase.GetAssetOrScenePath(asset)} isMain={AssetDatabase.IsMainAsset(asset)} " +
                                $"isSub={AssetDatabase.IsSubAsset(asset)} isForeign={AssetDatabase.IsForeignAsset(asset)} isNative={AssetDatabase.IsNativeAsset(asset)}");
                            throw ex;
                        }
                    }
                }
                
                foreach (var assetToHide in AssetSaver.GetPersistedAssets().Concat(assetsToSave))
                {
                    if (GeneratedAssetBundleExtractor.IsAssetTypeHidden(assetToHide.GetType()))
                    {
                        assetToHide.hideFlags = HideFlags.HideInHierarchy;
                    }
                }

                AssetSaver.SaveAssets(assetsToSave);

                // Remove obsolete temporary assets
                foreach (var asset in _savedObjects)
                {
                    if (!(asset is Component || asset is GameObject))
                    {
                        // Traversal can't currently handle prefabs, so this must have been manually added. Avoid purging it.
                        continue;
                    }

                    UnityObject.DestroyImmediate(asset);
                }
            }
            finally
            {
                AssetSaver.Dispose();
            }

            Profiler.EndSample();
        }

        public void DeactivateExtensionContext<T>() where T : IExtensionContext
        {
            DeactivateExtensionContext(typeof(T));
        }

        public void DeactivateExtensionContext(Type t)
        {
            using (new ExecutionScope(this))
            using (ErrorReport.CurrentReport.WithExtensionContextTrace(t))
                try
                {
                    if (_initializedExtensions.ContainsKey(t))
                    {
                        var ctx = _initializedExtensions[t];
                        Profiler.BeginSample("NDMF Deactivate: " + t);
                        try
                        {
                            ctx.OnDeactivate(this);
                        }
                        finally
                        {
                            Profiler.EndSample();
                        }

                        _initializedExtensions.Remove(t);
                    }

                    _availableExtensions.Remove(t);
                }
                catch (Exception e)
                {
                    ErrorReport.ReportException(e);
                    throw;
                }
        }

        internal void RunPass(ConcretePass pass)
        {
            using (new ExecutionScope(this))
            using (ErrorReport.CurrentReport.WithContext(pass.Plugin as PluginBase))
            using (ErrorReport.CurrentReport.WithContextPassName(pass.Description))
            {
                sw.Start();

                ImmutableDictionary<Type, double> deactivationTimes = ImmutableDictionary<Type, double>.Empty;

                foreach (var ty in pass.DeactivatePlugins)
                {
                    Stopwatch sw2 = new Stopwatch();
                    sw2.Start();
                    DeactivateExtensionContext(ty);
                    deactivationTimes = deactivationTimes.Add(ty, sw2.Elapsed.TotalMilliseconds);
                }

                ImmutableDictionary<Type, double> activationTimes = ImmutableDictionary<Type, double>.Empty;
                foreach (var ty in pass.ActivatePlugins)
                {
                    Stopwatch sw2 = new Stopwatch();
                    sw2.Start();
                    PrepareExtensionContext(ty);
                    activationTimes = activationTimes.Add(ty, sw2.Elapsed.TotalMilliseconds);
                }

                ExecutePassBody(pass, activationTimes, deactivationTimes);

                sw.Stop();
            }
        }

        internal void RunExtensionContextDeactivation(ConcretePass pass, Type extensionType)
        {
            using var _platformScope = new AmbientPlatform.Scope(PlatformProvider);
            using (new ExecutionScope(this))
            using (ErrorReport.CurrentReport.WithContext(pass.Plugin as PluginBase))
            using (ErrorReport.CurrentReport.WithContextPassName(pass.Description))
            {
                sw.Start();
                try
                {
                    DeactivateExtensionContext(extensionType);
                }
                finally
                {
                    sw.Stop();
                }
            }
        }

        internal void RunExtensionContextActivation(ConcretePass pass, Type extensionType)
        {
            using var _platformScope = new AmbientPlatform.Scope(PlatformProvider);
            using (new ExecutionScope(this))
            using (ErrorReport.CurrentReport.WithContext(pass.Plugin as PluginBase))
            using (ErrorReport.CurrentReport.WithContextPassName(pass.Description))
            {
                sw.Start();
                try
                {
                    PrepareExtensionContext(extensionType);
                }
                finally
                {
                    sw.Stop();
                }
            }
        }

        internal void RunPassBody(ConcretePass pass)
        {
            using var _platformScope = new AmbientPlatform.Scope(PlatformProvider);
            using (new ExecutionScope(this))
            using (ErrorReport.CurrentReport.WithContext(pass.Plugin as PluginBase))
            using (ErrorReport.CurrentReport.WithContextPassName(pass.Description))
            {
                sw.Start();
                try
                {
                    ExecutePassBody(
                        pass,
                        ImmutableDictionary<Type, double>.Empty,
                        ImmutableDictionary<Type, double>.Empty
                    );
                }
                finally
                {
                    sw.Stop();
                }
            }
        }

        private void ExecutePassBody(
            ConcretePass pass,
            ImmutableDictionary<Type, double> activationTimes,
            ImmutableDictionary<Type, double> deactivationTimes
        )
        {
            var passTimer = new Stopwatch();
            passTimer.Start();
            Profiler.BeginSample(pass.Description);
            try
            {
                pass.Execute(this);
            }
            catch (Exception e)
            {
                pass.Plugin.OnUnhandledException(e);
                ErrorReport.ReportException(e);
            }
            finally
            {
                Profiler.EndSample();
                passTimer.Stop();
            }

            BuildEvent.Dispatch(new BuildEvent.PassExecuted(
                pass.InstantiatedPass.QualifiedName,
                passTimer.Elapsed.TotalMilliseconds,
                activationTimes,
                deactivationTimes
            ));
        }

        public void DeactivateAllExtensionContexts()
        {
            using var _platformScope = new AmbientPlatform.Scope(PlatformProvider);
            
            Dictionary<Type, List<Type>> depIndex = new();
            foreach (var ty in _availableExtensions)
            {
                foreach (var dep in ty.RequiredContexts())
                {
                    if (!depIndex.ContainsKey(dep))
                    {
                        depIndex[dep] = new List<Type>();
                    }

                    depIndex[dep].Add(ty);
                }
            }

            while (_availableExtensions.Count > 0)
            {
                Type next = _availableExtensions.First();
                Type candidate;
                do
                {
                    candidate = next;
                    var revDeps = depIndex.GetValueOrDefault(next) as IEnumerable<Type>
                                  ?? Array.Empty<Type>();
                    next = revDeps.FirstOrDefault(t => _availableExtensions.Contains(t));
                } while (next != null);
                
                DeactivateExtensionContext(candidate);
            }
        }

        public T ActivateExtensionContextRecursive<T>() where T : IExtensionContext
        {
            return (T) ActivateExtensionContextRecursive(typeof(T));
        }
        
        public IExtensionContext ActivateExtensionContextRecursive(Type ty)
        {
            foreach (var dependency in ty.RequiredContexts())
            {
                ActivateExtensionContextRecursive(dependency);
            }

            return ActivateExtensionContext(ty);
        }
        
        private void PrepareExtensionContext(Type ty)
        {
            if (_availableExtensions.Contains(ty))
            {
                return;
            }

            if (Attribute.IsDefined(ty, typeof(LazyExtension)))
            {
                _availableExtensions.Add(ty);
            }
            else
            {
                InitializeExtensionContext(ty);
            }
        }

        public T ActivateExtensionContext<T>() where T : IExtensionContext
        {
            return (T)ActivateExtensionContext(typeof(T));
        }

        public IExtensionContext ActivateExtensionContext(Type ty)
        {
            if (_initializedExtensions.TryGetValue(ty, out var context))
            {
                return context;
            }

            foreach (var dependency in ty.RequiredContexts())
            {
                if (_availableExtensions.Contains(dependency))
                {
                    ActivateExtensionContext(dependency);
                }
            }

            return InitializeExtensionContext(ty);
        }

        private IExtensionContext InitializeExtensionContext(Type ty)
        {
            using var _platformScope = new AmbientPlatform.Scope(PlatformProvider);
            
            using (new ExecutionScope(this))
            using (ErrorReport.CurrentReport.WithExtensionContextTrace(ty))
                try
                {
                    if (!_extensions.TryGetValue(ty, out var ctx))
                    {
                        ctx = (IExtensionContext)ty.GetConstructor(Type.EmptyTypes).Invoke(Array.Empty<object>());
                        _extensions[ty] = ctx;
                    }

                    Profiler.BeginSample("NDMF Activate: " + ty);
                    try
                    {
                        ctx.OnActivate(this);
                    }
                    finally
                    {
                        Profiler.EndSample();
                    }

                    _initializedExtensions.Add(ty, ctx);
                    _availableExtensions.Add(ty);
                    return ctx;
                }
                catch (Exception e)
                {
                    ErrorReport.ReportException(e);
                    throw;
                }
        }

        internal void Finish()
        {
            using var _platformScope = new AmbientPlatform.Scope(PlatformProvider);
            
            using (new ExecutionScope(this))
            {
                sw.Start();
                DeactivateAllExtensionContexts();

                RecalculateAllMeshes();

                Serialize();
                sw.Stop();

                BuildEvent.Dispatch(new BuildEvent.BuildEnded(sw.ElapsedMilliseconds, _report.Errors.Count == 0));

                if (!Application.isBatchMode && _report.Errors.Count > 0)
                {
                    ErrorReportWindow.ShowReport(_report);
                }
            }
        }

        private readonly HashSet<Mesh> _meshesExcludedFromRecalculation = new();

        /// <summary>
        ///     NDMF will automatically invoke RecalculateUVDistributionMetrics on all temporary asset meshes to ensure
        ///     they work properly with streaming mipmaps. If you don't want this to happen for a specific mesh (eg if you
        ///     invoked RecalculateUVDistributionMetric with a specific UV channel), invoke this function with enabled=false
        ///     to opt out.
        /// </summary>
        /// <param name="mesh">The mesh to control</param>
        /// <param name="enabled">False to disable UV distribution recalculation</param>
        public void SetEnableUVDistributionRecalculation(Mesh mesh, bool enabled)
        {
            if (enabled)
            {
                _meshesExcludedFromRecalculation.Remove(mesh);
            }
            else
            {
                _meshesExcludedFromRecalculation.Add(mesh);
            }
        }
        
        private void RecalculateAllMeshes()
        {
            foreach (var mesh in EnumerateMeshes())
            {
                if (!_meshesExcludedFromRecalculation.Contains(mesh))
                {
                    mesh.RecalculateUVDistributionMetrics();
                }
            }
        }

        private IEnumerable<Mesh> EnumerateMeshes()
        {
            foreach (var meshFilter in _avatarRootObject.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = meshFilter.sharedMesh;
                if (mesh != null && IsTemporaryAsset(mesh)) yield return mesh;
            }

            foreach (var skinnedMeshRenderer in _avatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = skinnedMeshRenderer.sharedMesh;
                if (mesh != null && IsTemporaryAsset(mesh)) yield return mesh;
            }
        }
    }
}
