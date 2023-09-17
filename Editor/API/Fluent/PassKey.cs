namespace nadena.dev.ndmf
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