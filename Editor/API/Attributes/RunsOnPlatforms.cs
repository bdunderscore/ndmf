using System;
using JetBrains.Annotations;

namespace nadena.dev.ndmf
{
    /// <summary>
    ///     Declares that the attached Pass or Plugin runs on all NDMF platforms. Must not be used in conjunction with RunsOnPlatform.
    ///     <p />
    ///     If this attribute is attached to a Pass class, any configuration performed on the `Sequence` class will be ignored
    ///     for this class. If this attribute is attached to a Plugin class, Sequences will start with all platforms enabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [PublicAPI]
    public sealed class RunsOnAllPlatforms : Attribute
    {
    }

    /// <summary>
    ///     Declares that the attached Pass or Plugin runs on all NDMF platforms. Must not be used in conjunction with
    ///     RunsOnAllPlatforms.
    ///     If your pass supports multiple platforms, this attribute can be attached multiple times to declares this.
    ///     <p />
    ///     If this attribute is attached to a Pass class, any configuration performed on the `Sequence` class will be ignored
    ///     for this class. If this attribute is attached to a Plugin class, Sequences will start with the specified
    ///     platforms enabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    [PublicAPI]
    public sealed class RunsOnPlatform : Attribute
    {
        public string Platform { get; }

        public RunsOnPlatform(string platform)
        {
            Platform = platform;
        }
    }
}