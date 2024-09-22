#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf
{
    public enum ParameterNamespace
    {
        /// <summary>
        /// Indicates that this is an animator or expressions menu parameter
        /// </summary>
        Animator,

        /// <summary>
        /// Indicates that this is a PhysBones parameter prefix
        /// </summary>
        PhysBonesPrefix
    }

    public struct ParameterMapping
    {
        /// <summary>
        /// The name of the parameter to expose outside of the renaming scope
        /// </summary>
        public string ParameterName;

        /// <summary>
        /// If true, this parameter is set to be hidden outside of the renaming scope.
        /// </summary>
        public bool IsHidden;

        public ParameterMapping(string name, bool isHidden = false)
        {
            ParameterName = name;
            IsHidden = isHidden;
        }
    }

    /// <summary>
    /// This class declares a parameter that is supplied by a NDMF component. This is intended to be used for
    /// introspection via the ParameterInfo API. Note that exposing a ProvidedParameter does _not_ actually add the
    /// parameter to the Expressions Parameters asset; this is left up to individual NDMF plugins. However, it can be
    /// used to detect parameter names and in-use bit counts for use in user-facing UI.
    /// </summary>
    public sealed class ProvidedParameter : ICloneable
    {
        private bool Equals(ProvidedParameter other)
        {
            return _effectiveName == other._effectiveName &&
                   _isAnimatorOnly == other._isAnimatorOnly &&
                   OriginalName == other.OriginalName &&
                   Namespace == other.Namespace &&
                   Equals(Source, other.Source) &&
                   Equals(Plugin, other.Plugin) &&
                   ParameterType == other.ParameterType &&
                   ExpandTypeOnConflict == other.ExpandTypeOnConflict &&
                   IsHidden == other.IsHidden &&
                   WantSynced == other.WantSynced;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is ProvidedParameter other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_effectiveName != null ? _effectiveName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ _isAnimatorOnly.GetHashCode();
                hashCode = (hashCode * 397) ^ (OriginalName != null ? OriginalName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)Namespace;
                hashCode = (hashCode * 397) ^ (Source != null ? Source.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Plugin != null ? Plugin.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ ParameterType.GetHashCode();
                hashCode = (hashCode * 397) ^ ExpandTypeOnConflict.GetHashCode();
                hashCode = (hashCode * 397) ^ IsHidden.GetHashCode();
                hashCode = (hashCode * 397) ^ WantSynced.GetHashCode();
                return hashCode;
            }
        }

        public ProvidedParameter(
            string name,
            ParameterNamespace namespace_,
            Component source,
            PluginBase plugin,
            AnimatorControllerParameterType? parameterType
        )
        {
            OriginalName = name;
            Namespace = namespace_;
            Source = source;
            Plugin = plugin;
            ParameterType = parameterType;
        }

        internal (ParameterNamespace, string) NamePair => (Namespace, EffectiveName);

        public int BitUsage
        {
            get
            {
                if (IsAnimatorOnly || WantSynced != true || ParameterType == null)
                {
                    return 0;
                }

                switch (ParameterType)
                {
                    case AnimatorControllerParameterType.Bool:
                        return 1;
                    case AnimatorControllerParameterType.Int:
                    case AnimatorControllerParameterType.Float:
                        return 8;
                    default:
                        return 0;
                }
            }
        }

        /// <summary>
        /// The name of this parameter, as originally set by the component.
        /// </summary>
        public string OriginalName { get; private set; }

        public ParameterNamespace Namespace { get; private set; }

        private string _effectiveName;

        /// <summary>
        /// The name of this parameter after remapping is applied. Defaults to the same as OriginalName.
        /// </summary>
        public string EffectiveName
        {
            get => _effectiveName ?? OriginalName;
            set => _effectiveName = value;
        }

        /// <summary>
        /// The component which originated this parameter.
        /// </summary>
        public Component Source { get; }

        /// <summary>
        /// The NDMF plugin which sourced this parameter.
        /// </summary>
        public PluginBase Plugin { get; }

        /// <summary>
        /// The parameter type of this parameter. Ignored for PhysBones prefixes. May be null if the type is undefined
        /// (e.g. with contact receivers).
        /// </summary>
        public AnimatorControllerParameterType? ParameterType { get; set; }

        /// <summary>
        /// If true, conflicting parameter types will be expanded to Bool --> Int --> Float.
        /// </summary>
        public bool ExpandTypeOnConflict { get; set; }

        /// <summary>
        /// If true, this parameter will not be registered in the VRC Expressions Parameters asset. Forced to true for
        /// PhysBones prefixes.
        ///
        /// Conflict resolution: "false" wins. 
        /// </summary>
        private bool _isAnimatorOnly;

        public bool IsAnimatorOnly
        {
            get => _isAnimatorOnly || Namespace == ParameterNamespace.PhysBonesPrefix;
            set => _isAnimatorOnly = value;
        }

        /// <summary>
        /// If true, this parameter is an internal parameter that should be hidden in user-facing UI. Normally, these
        /// parameters should also use generated names to avoid collisions.
        ///
        /// Conflict resolution: "false" wins.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// If true, this parameter requests that it be synced across the network. If false, it requests not to be
        /// synced.
        ///
        /// Ignored for animator-only values.
        /// </summary>
        public bool WantSynced { get; set; }

        /// <summary>
        ///     The default value of this parameter, if known.
        /// </summary>
        public float? DefaultValue { get; set; }

        public IEnumerable<ProvidedParameter> SubParameters()
        {
            if (Namespace == ParameterNamespace.Animator)
            {
                return new ProvidedParameter[] { this };
            }
            return new ProvidedParameter[]
            {
                new ProvidedParameter(OriginalName + "_IsGrabbed", ParameterNamespace.Animator, Source, Plugin, AnimatorControllerParameterType.Bool) { IsHidden = IsHidden, WantSynced = WantSynced },
                new ProvidedParameter(OriginalName + "_IsPosed", ParameterNamespace.Animator, Source, Plugin, AnimatorControllerParameterType.Bool) { IsHidden = IsHidden, WantSynced = WantSynced },
                new ProvidedParameter(OriginalName + "_Angle", ParameterNamespace.Animator, Source, Plugin, AnimatorControllerParameterType.Float) { IsHidden = IsHidden, WantSynced = WantSynced },
                new ProvidedParameter(OriginalName + "_Stretch", ParameterNamespace.Animator, Source, Plugin, AnimatorControllerParameterType.Float) { IsHidden = IsHidden, WantSynced = WantSynced },
                new ProvidedParameter(OriginalName + "_Squish", ParameterNamespace.Animator, Source, Plugin, AnimatorControllerParameterType.Float) { IsHidden = IsHidden, WantSynced = WantSynced },
            };
        }

        public ProvidedParameter Clone()
        {
            return (ProvidedParameter)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }

    /// <summary>
    /// Annotates a class as a provider of parameters for a specific component type. The class should implement
    /// IParameterProvider, and expose a public constructor taking an argument with the type of the ForType argument
    /// here. This constructor will be invoked when querying a component's parameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    public sealed class ParameterProviderFor : Attribute
    {
        public Type ForType { get; private set; }

        public ParameterProviderFor(Type forType)
        {
            ForType = forType;
        }
    }

    /// <summary>
    /// Provides information about parameters supplied by a (custom) component. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IParameterProvider
    {
        /// <summary>
        /// Returns the set of VRChat expression parameters supplied by this specific component.
        ///
        /// When processing from within the context of a build, a BuildContext parameter will be supplied.
        /// This can be used to ensure that generated parameter names are consistent within a single build. However,
        /// you should avoid changing the number or type of synced parameters, to ensure that parameter usage estimates
        /// are correct.
        ///
        /// This method should not consider parameter renames. If you need to remap names, do that either in your own
        /// build pass logic, or by converting to e.g. Modular Avatar Parameters objects at build time.
        ///
        /// Finally, the parameter objects that are returned from this function <i>must</i> be newly created on each
        /// call. This is because subsequent processing may modify these objects (e.g. remapping names).
        /// </summary>
        /// <param name="context">The build context, if available. Note that this method may be called outside of
        /// a build (e.g. to query parameter usage from editor UI)</param>
        /// <returns></returns>
        IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null);

        /// <summary>
        /// Remap the names of parameters within this GameObject and its children. On entry, nameMap will contain the
        /// mappings effective at the parent object (or prior component within this game object). This method should
        /// update nameMap with any remappings it would like to apply. nameMap should be a mapping of "internal name"
        /// (the name used within children and components at or after this component) to "external name" (the name
        /// used at the parent game object, or possibly at the final built avatar level).
        /// </summary>
        /// <param name="nameMap"></param>
        /// <param name="context"></param>
        void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap,
                BuildContext context = null)
#if UNITY_2021_1_OR_NEWER
        {
            // default is a no-op
        }
#else
            // Sorry, if you're maintaining compatibility with Unity 2019, you need to explicitly declare this member.
            ;
#endif
    }
}
