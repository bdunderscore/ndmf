namespace nadena.dev.ndmf.preview
{
    /// <summary>
    /// Describes the time at which a particular mesh operation should be performed. Sequence points are created
    /// relative to each other; unrelated sequence points cannot be used in the same PreviewSession. 
    /// </summary>
    public sealed class SequencePoint
    {
        private static int _creationOrder = 0;

        public string DebugString { get; set; }

        public SequencePoint()
        {
            DebugString = "#" + (_creationOrder++);
        }

        public override string ToString()
        {
            return "[SequencePoint " + DebugString + "]";
        }
    }
}