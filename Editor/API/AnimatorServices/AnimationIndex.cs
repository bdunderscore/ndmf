#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     AnimationIndex indexes the full set of animations known to the `VirtualControllerContext`, and allows for
    ///     efficient querying of animations by object path or binding, as well as some bulk editing operations.
    ///     The AnimationIndex registers invalidation callbacks with all nodes in the virtual controller hierarchy, and
    ///     will therefore be automatically updated if anything changes in the hierarchy.
    ///     Normally, you should obtain an AnimationIndex from the `AnimatorServicesContext`, but constructors are provided
    ///     for testing purposes.
    /// </summary>
    public sealed class AnimationIndex
    {
        private readonly Func<IEnumerable<VirtualNode>> _getRoots;
        private readonly Func<long> _getInvalidationToken;

        private long _lastInvalidationToken;

        private readonly Action _invalidateAction;
        private bool _isValid;

        private bool IsValid => _isValid && _lastInvalidationToken == _getInvalidationToken();

        private readonly Dictionary<string, HashSet<VirtualClip>> _objectPathToClip = new();
        private readonly Dictionary<EditorCurveBinding, HashSet<VirtualClip>> _bindingToClip = new();
        private readonly Dictionary<VirtualClip, HashSet<EditorCurveBinding>> _lastBindings = new();
        private readonly HashSet<VirtualClip> _pptrClips = new();

        internal IPlatformAnimatorBindings PlatformBindings = GenericPlatformAnimatorBindings.Instance;
        
        internal AnimationIndex(
            Func<IEnumerable<VirtualAnimatorController>> getRoots,
            Func<long> getInvalidationToken)
        {
            _getRoots = getRoots;
            _getInvalidationToken = getInvalidationToken;
            _invalidateAction = () => _isValid = false;
        }

        /// <summary>
        ///     Creates an animation index over a set of virtualized animator controllers.
        ///     This method is primarily intended for use in tests.
        /// </summary>
        /// <param name="controllers"></param>
        public AnimationIndex(IEnumerable<VirtualNode> controllers)
        {
            _invalidateAction = () => _isValid = false;
            var controllerList = new List<VirtualNode>(controllers);
            _getRoots = () => controllerList;
            _getInvalidationToken = () => _lastInvalidationToken;
        }

        public IEnumerable<VirtualClip> ClipsWithObjectCurves
        {
            get
            {
                if (!IsValid) RebuildCache();

                return _pptrClips.Select(x => x);
            }
        }

        [PublicAPI]
        public IEnumerable<Object> GetPPtrReferencedObjects
        {
            get
            {
                return ClipsWithObjectCurves.SelectMany(
                    clip => clip.GetObjectCurveBindings()
                        .SelectMany(ecb => clip.GetObjectCurve(ecb))
                        .Select(kf => kf.value)
                        .Where(obj => obj != null)
                ).Distinct();
            }
        }

        /// <summary>
        ///     Maps all object curves in the animation index according to the provided mapping function.
        ///     The mapping function must not return null. <see cref="ClipsWithObjectCurves" /> if you need to perform
        ///     more complex manipulations.
        /// </summary>
        /// <param name="mapping">Mapping function to apply</param>
        [PublicAPI]
        public void RewriteObjectCurves(Func<Object, Object> mapping)
        {
            var clips = ClipsWithObjectCurves.ToList();

            foreach (var clip in clips)
            {
                foreach (var ecb in clip.GetObjectCurveBindings())
                {
                    var curve = clip.GetObjectCurve(ecb)!;
                    for (var i = 0; i < curve.Length; i++)
                    {
                        curve[i].value = mapping(curve[i].value) ??
                                         throw new InvalidOperationException("Mapping function returned null");
                    }

                    clip.SetObjectCurve(ecb, curve);
                }
            }
        }
        
        /// <summary>
        ///     Returns all clips associated with a given virtual object path.
        /// </summary>
        /// <param name="objectPath"></param>
        /// <returns></returns>
        public IEnumerable<VirtualClip> GetClipsForObjectPath(string objectPath)
        {
            if (!IsValid) RebuildCache();

            if (_objectPathToClip.TryGetValue(objectPath, out var clips))
            {
                return clips;
            }

            return Enumerable.Empty<VirtualClip>();
        }

        /// <summary>
        ///     Returns all clips containing curves for a given binding.
        /// </summary>
        /// <param name="binding"></param>
        /// <returns></returns>
        public IEnumerable<VirtualClip> GetClipsForBinding(EditorCurveBinding binding)
        {
            if (!IsValid) RebuildCache();

            if (_bindingToClip.TryGetValue(binding, out var clips))
            {
                return clips;
            }

            return Enumerable.Empty<VirtualClip>();
        }
        
        /// <summary>
        ///     Rewrites all object paths in animations and avatar masks according to the provided mapping function. If the
        ///     mapping function returns null, all animations referencing the path will be removed from the animation.
        /// </summary>
        /// <param name="rewriteRules"></param>
        public void RewritePaths(Func<string, string?> rewriteRules)
        {
            if (!IsValid) RebuildCache();

            var rewriteSet = _objectPathToClip.Values.SelectMany(s => s).Distinct();
            
            RewritePaths(rewriteSet, rewriteRules);
            RewriteNodes(rewriteRules);
        }

        private void RewriteNodes(Func<string, string?> rewriteRules)
        {
            foreach (var root in _getRoots())
            {
                if (root is VirtualAnimatorController vac)
                {
                    foreach (var layer in vac.Layers)
                    {
                        if (layer.AvatarMask is not null)
                        {
                            RewriteAvatarMask(layer.AvatarMask, rewriteRules);
                        }
                    }
                }

                foreach (var node in root.AllReachableNodes())
                {
                    if (node is VirtualStateMachine vsm)
                    {
                        foreach (var sb in vsm.Behaviours)
                        {
                            PlatformBindings.RemapPathsInStateBehaviour(sb, rewriteRules);
                        }
                    }
                    else if (node is VirtualState vs)
                    {
                        foreach (var sb in vs.Behaviours)
                        {
                            PlatformBindings.RemapPathsInStateBehaviour(sb, rewriteRules);
                        }
                    }
                }
            }
        }

        private void RewriteAvatarMask(VirtualAvatarMask layerAvatarMask, Func<string, string?> rewriteRules)
        {
            Dictionary<string, float> outputDict = new();

            foreach (var kvp in layerAvatarMask.Elements)
            {
                var rewritten = rewriteRules(kvp.Key);
                if (rewritten != null)
                {
                    outputDict[rewritten] = kvp.Value;
                }
            }

            layerAvatarMask.Elements = outputDict.ToImmutableDictionary();
        }

        /// <summary>
        ///     Rewrites all object paths in animations and avatar masks according to the provided mapping dictionary. If the
        ///     path is not present in the dictionary, it will be unchanged; if it is present and mapped to null, it will be
        ///     deleted from animations.
        /// </summary>
        /// <param name="rewriteRules"></param>
        public void RewritePaths(Dictionary<string, string?> rewriteRules)
        {
            if (!IsValid) RebuildCache();
            
            HashSet<VirtualClip> rewriteSet = new();

            foreach (var (key, value) in rewriteRules)
            {
                if (key == value) continue;
                if (!_objectPathToClip.TryGetValue(key, out var clips)) continue;
                rewriteSet.UnionWith(clips);
            }

            Func<string, string?> rewriteFunc = k =>
            {
                // Note: We don't use GetValueOrDefault here as we want to distinguish between null and missing keys
                // ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
                if (rewriteRules.TryGetValue(k, out var v)) return v;
                return k;
            };
            
            RewritePaths(rewriteSet, rewriteFunc);
            RewriteNodes(rewriteFunc);
        }

        private void RewritePaths(IEnumerable<VirtualClip> rewriteSet, Func<string, string?> rewriteFunc)
        {
            List<VirtualClip> recacheNeeded = new();

            foreach (var clip in rewriteSet)
            {
                clip.EditPaths(rewriteFunc);
                if (!_isValid)
                {
                    recacheNeeded.Add(clip);
                }

                _isValid = true;
            }

            foreach (var clip in recacheNeeded)
            {
                CacheClip(clip);
            }
        }

        /// <summary>
        ///     Applies an arbitrary callback to all clips associated with a given object path. This operation can be more
        ///     efficient than querying for all clips associated with a path and then applying the callback, as it avoids
        ///     rebuilding the entire animation index when clips are edited.
        /// </summary>
        /// <param name="binding"></param>
        /// <param name="processClip"></param>
        public void EditClipsByBinding(IEnumerable<EditorCurveBinding> binding, Action<VirtualClip> processClip)
        {
            if (!IsValid) RebuildCache();

            var clips = binding.SelectMany(GetClipsForBinding).ToHashSet();
            var toRecache = new List<VirtualClip>();
            foreach (var clip in clips)
            {
                processClip(clip);
                if (!_isValid)
                {
                    toRecache.Add(clip);
                }

                _isValid = true;
            }

            foreach (var clip in toRecache)
            {
                CacheClip(clip);
            }
        }

        private void RebuildCache()
        {
            Profiler.BeginSample("AnimationIndex.RebuildCache");
            _objectPathToClip.Clear();
            _bindingToClip.Clear();
            _lastBindings.Clear();
            _pptrClips.Clear();

            foreach (var clip in EnumerateClips())
            {
                CacheClip(clip);
            }

            _isValid = true;
            Profiler.EndSample();
        }

        private void CacheClip(VirtualClip clip)
        {
            if (_lastBindings.TryGetValue(clip, out var lastBindings))
            {
                foreach (var binding in lastBindings)
                {
                    _bindingToClip[binding].Remove(clip);
                    _objectPathToClip[binding.path].Remove(clip);
                }
            }
            else
            {
                lastBindings = new HashSet<EditorCurveBinding>();
                _lastBindings[clip] = lastBindings;
            }

            lastBindings.Clear();
            lastBindings.UnionWith(clip.GetObjectCurveBindings());
            lastBindings.UnionWith(clip.GetFloatCurveBindings());

            if (clip.GetObjectCurveBindings().Any())
            {
                _pptrClips.Add(clip);
            }

            foreach (var binding in lastBindings)
            {
                if (!_bindingToClip.TryGetValue(binding, out var clips))
                {
                    clips = new HashSet<VirtualClip>();
                    _bindingToClip[binding] = clips;
                }

                clips.Add(clip);

                if (!_objectPathToClip.TryGetValue(binding.path, out var pathClips))
                {
                    pathClips = new HashSet<VirtualClip>();
                    _objectPathToClip[binding.path] = pathClips;
                }

                pathClips.Add(clip);
            }
        }

        private IEnumerable<VirtualClip> EnumerateClips()
        {
            HashSet<object> visited = new();
            Queue<VirtualNode> queue = new();

            _lastInvalidationToken = _getInvalidationToken();
            foreach (var controller in _getRoots())
            {
                queue.Enqueue(controller);
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                node.RegisterCacheObserver(_invalidateAction);
                
                if (!visited.Add(node))
                {
                    continue;
                }

                foreach (var child in node.EnumerateChildren())
                {
                    if (!visited.Contains(child)) queue.Enqueue(child);
                }

                if (node is VirtualClip clip)
                {
                    yield return clip;
                }
            }
        }

        /// <summary>
        ///     Applies a prefix to all paths in the index. Implicitly adds a "/" to the end of the path.
        /// </summary>
        /// <param name="basePath"></param>
        public void ApplyPathPrefix(string basePath)
        {
            if (basePath != "") RewritePaths(p => p != "" ? basePath + "/" + p : basePath);
        }
    }
}