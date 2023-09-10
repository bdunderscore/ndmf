namespace nadena.dev.ndmf.fluent
{
    internal class PassKey
    {
        public readonly string QualifiedName;
        
        public PassKey(string qualifiedName)
        {
            QualifiedName = qualifiedName;
        }
        
        public override string ToString()
        {
            return QualifiedName;
        }
    }
}