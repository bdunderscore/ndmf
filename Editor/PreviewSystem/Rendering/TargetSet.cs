using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.preview.trace;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace nadena.dev.ndmf.preview
{
    internal class TargetSet
    {
        private static readonly Comparer<RenderGroup> GroupComparer = Comparer<RenderGroup>.Create(
            (a, b) => a.Renderers.First().GetInstanceID().CompareTo(b.Renderers.First().GetInstanceID())
        );

        private readonly ImmutableList<IRenderFilter> _filters;
        private readonly ImmutableHashSet<Renderer> _hideRenderers;
        private readonly ComputeContext _targetSetContext = new ComputeContext("Target Set");
        private readonly PropCache<IRenderFilter, CachedGroups> _groupsByFilterCache;
        private ImmutableList<Stage> _stages;


        private sealed class CachedGroups
        {
            internal ImmutableList<RenderGroup> SortedGroups { get; }

            internal CachedGroups(ImmutableList<RenderGroup> sortedGroups)
            {
                SortedGroups = sortedGroups;
            }
        }

        public struct Stage
        {
            public IRenderFilter Filter;
            public ImmutableList<RenderGroup> Groups; 
        }
        
        public TargetSet(
            ImmutableList<IRenderFilter> filters,
            ImmutableHashSet<Renderer> hideRenderers,
            TargetSet prior = null
        )
        {
            _filters = filters;
            _hideRenderers = hideRenderers;
            _groupsByFilterCache = prior?._groupsByFilterCache ?? new PropCache<IRenderFilter, CachedGroups>(
                "TargetSet.GetTargetGroups",
                ComputeGroupsForFilter,
                (a, b) => a.SortedGroups.SequenceEqual(b.SortedGroups),
                // Use reference equality comparer to avoid issues if user has overridden Equals or GetHashCode
                ReferenceEqualityComparer<IRenderFilter>.Instance
            );
            
            TraceBuffer.RecordTraceEvent("TargetSet.ctor", (ev) => "Get target groups");
            
            Profiler.BeginSample("TargetSet.ctor");
            try
            {
                var builder = ImmutableList.CreateBuilder<Stage>();
                foreach (var filter in _filters)
                {
                    if (!filter.IsEnabled(_targetSetContext)) continue;
                    
                    Profiler.BeginSample("TargetSet.GetTargetGroups[" + filter + "]");
                    var sortedGroups = _groupsByFilterCache.Get(_targetSetContext, filter).SortedGroups;
                    Profiler.EndSample();
                    if (sortedGroups.IsEmpty) continue;
                    
                    builder.Add(new Stage
                    {
                        Filter = filter,
                        Groups = sortedGroups
                    });
                }
                
                _stages = builder.ToImmutable();
            }
            finally
            {
                Profiler.EndSample();
            }
        }
        
        public TargetSet Refresh(ImmutableList<IRenderFilter> filters, ImmutableHashSet<Renderer> hideRenderers)
        {
            if (!_targetSetContext.IsInvalidated && _filters.SequenceEqual(filters) && hideRenderers.SetEquals(_hideRenderers))
            {
                return this;
            }
            
            return new TargetSet(filters, hideRenderers, this);
        }

        private static CachedGroups ComputeGroupsForFilter(ComputeContext context, IRenderFilter filter)
        {
            var groups = filter.GetTargetGroups(context);
            if (groups.IsEmpty)
            {
                return new CachedGroups(ImmutableList<RenderGroup>.Empty);
            }

            var unsupportedRenderer = groups.SelectMany(g => g.Renderers)
                .FirstOrDefault(x => x is not MeshRenderer and not SkinnedMeshRenderer);
            if (unsupportedRenderer != null)
            {
                Debug.LogError("[" + filter + "] Unsupported renderer " + unsupportedRenderer +
                               " in groups: " + string.Join(", ", groups));
                return new CachedGroups(ImmutableList<RenderGroup>.Empty);
            }

            var duplicateRenderers = groups.SelectMany(g => g.Renderers)
                .GroupBy(r => r)
                .FirstOrDefault(agg => agg.Count() > 1);
            if (duplicateRenderers != null)
            {
                Debug.LogError("[" + filter + "] Duplicate renderer " + duplicateRenderers.Key +
                               " in groups: " + string.Join(", ", groups));
                return new CachedGroups(ImmutableList<RenderGroup>.Empty);
            }

            var sortedGroups = groups.Sort(GroupComparer);
            return new CachedGroups(sortedGroups);
        }

        private bool RendererIsShown(ComputeContext context, Renderer renderer)
        {
            if (renderer == null) return false;
            if (_hideRenderers.Contains(renderer)) return false;
            if (!context.ActiveInHierarchy(renderer.gameObject)) return false;

            return context.Observe(renderer, r => r.enabled);
        }
        
        public ImmutableList<Stage> ResolveActiveStages(ComputeContext context)
        {
            Profiler.BeginSample("TargetSet.ResolveActiveStages");
            
            TraceBuffer.RecordTraceEvent("TargetSet.ResolveActiveStages", (ev) => "TargetSet: Resolve active stages");
            
            _targetSetContext.Invalidates(context);

            var targetRenderers = _stages
                .SelectMany(s => s.Groups)
                .SelectMany(g => g.Renderers)
                .Where(r => r != null)
                .Select(r => (r, SceneVisibilityManager.instance.IsHidden(r.gameObject, true))).ToArray();
            VisibilityMonitor.OnVisibilityChange.Register(_ => targetRenderers.Any(rp => rp.r == null || rp.Item2 != SceneVisibilityManager.instance.IsHidden(rp.r.gameObject, true)), context);

            var maybeActiveRenderers = new HashSet<Renderer>();
            
            // Register all visible (or potentially forced) renderers first
            foreach (var stage in _stages)
            {
                foreach (var group in stage.Groups)
                {
                    foreach (var renderer in group.Renderers)
                    {
                        if (RendererIsShown(context, renderer) || stage.Filter.CanEnableRenderers)
                        {
                            maybeActiveRenderers.Add(renderer);
                        }
                    }
                }
            }
            
            // If a maybe-active renderer is in the same target group as an inactive renderer, and the filter for that
            // group is marked strict, we need to force all of its neighbors in the group as well. This then proceeds up
            // to earlier stages in the pipeline.
            for (int i = _stages.Count - 1; i >= 0; i--)
            {
                var stage = _stages[i];
                if (!stage.Filter.StrictRenderGroup) continue;
                
                foreach (var group in _stages[i].Groups)
                {
                    bool anyActive = group.Renderers.Any(maybeActiveRenderers.Contains);

                    if (anyActive)
                    {
                        foreach (var renderer in group.Renderers)
                        {
                            maybeActiveRenderers.Add(renderer);
                        }
                    }
                }
            }
            
            // Now rebuild the stages considering the maybe-active set.
            var builder = ImmutableList.CreateBuilder<Stage>();
            
            foreach (var stage in _stages)
            {
                var activeGroups = ImmutableList.CreateBuilder<RenderGroup>();
                foreach (var group in stage.Groups)
                {
                    RenderGroup filtered = group.Filter(maybeActiveRenderers);
                    if (!filtered.IsEmpty)
                    {
                        activeGroups.Add(filtered);
                    }
                }

                if (activeGroups.Count > 0)
                {
                    builder.Add(new Stage
                    {
                        Filter = stage.Filter,
                        Groups = activeGroups.ToImmutable()
                    });
                }
            }
            
            Profiler.EndSample();
            
            return builder.ToImmutable();
        }
    }
}