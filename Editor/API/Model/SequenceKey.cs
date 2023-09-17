#region

using System;
using JetBrains.Annotations;

#endregion

namespace nadena.dev.ndmf.model
{
    /// <summary>
    /// A SequenceKey identifies a single Sequenceable object. SequenceKeys with QualifiedNames compare equal if their
    /// QualifiedNames are the same. For anonymous/inline sequencables, you can also construct a sequence key with a
    /// null qualified name, in which case a random GUID will be assigned.
    /// </summary>
    internal sealed class SequenceKey
    {
        public readonly string QualifiedName;

        public SequenceKey(string qualifiedName)
        {
            QualifiedName = qualifiedName ?? Guid.NewGuid().ToString();
        }

        public static SequenceKey PassKey(Type t)
        {
            return new SequenceKey(t.FullName);
        }

        public static SequenceKey BeforePlugin(string qualifiedName)
        {
            return new SequenceKey(qualifiedName + "$BeforePlugin");
        }

        public static SequenceKey AfterPlugin(string qualifiedName)
        {
            return new SequenceKey(qualifiedName + "$AfterPlugin");
        }

        public override string ToString()
        {
            return QualifiedName;
        }

        private bool Equals([CanBeNull] SequenceKey other)
        {
            return QualifiedName == other?.QualifiedName;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is SequenceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (QualifiedName != null ? QualifiedName.GetHashCode() : 0);
        }

        public static bool operator ==(SequenceKey left, SequenceKey right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SequenceKey left, SequenceKey right)
        {
            return !Equals(left, right);
        }
    }
}