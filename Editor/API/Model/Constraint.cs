namespace nadena.dev.ndmf.model
{
    internal enum ConstraintType
    {
        WeakOrder,
        Sequence,
        WaitFor,
    }

    internal struct Constraint
    {
        public string DeclaredFile;
        public int DeclaredLine;
        public PassKey First, Second;
        public ConstraintType Type;
        
        public override string ToString()
        {
            return $"{First} {Type} {Second}";
        }
    }
}