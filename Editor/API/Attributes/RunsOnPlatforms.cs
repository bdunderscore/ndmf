using System;
using JetBrains.Annotations;

namespace nadena.dev.ndmf
{
    /// <summary>
    ///     Declares that the attached Pass or Plugin runs on all NDMF platforms. Must not be used in conjunction with RunsOnPlatforms.
    ///     <p />
    ///     If this attribute is attached to a Pass class, any configuration performed on the `Sequence` class will be ignored
    ///     for this class. If this attribute is attached to a Plugin class, Sequences will start with all platforms enabled.
    ///     <p/>
    ///     <see cref="WellKnownPlatforms"/> for information on precedence of different platform declaration methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [PublicAPI]
    public sealed class RunsOnAllPlatforms : Attribute
    {
    }

    /// <summary>
    ///     Declares that the attached Pass or Plugin runs on one or more specified NDMF platforms. Must not be used in conjunction with
    ///     RunsOnAllPlatforms.
    ///     If your pass supports multiple platforms, this attribute can be attached multiple times to declare this.
    ///     <p />
    ///     If this attribute is attached to a Pass class, any configuration performed on the `Sequence` class will be ignored
    ///     for this class. If this attribute is attached to a Plugin class, Sequences will start with the specified
    ///     platforms enabled.
    ///     <p/>
    ///     <see cref="WellKnownPlatforms"/> for information on precedence of different platform declaration methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [PublicAPI]
    public sealed class RunsOnPlatforms : Attribute
    {
        public string[] Platforms { get; }

        public RunsOnPlatforms(params string[] platforms)
        {
            Platforms = platforms;
        }
    }
}