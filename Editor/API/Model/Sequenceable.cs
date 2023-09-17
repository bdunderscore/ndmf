namespace nadena.dev.ndmf.model
{
    internal abstract class Sequenceable
    {
        protected abstract SequenceKey Key { get; }
    }

    internal class PluginSequencer : Sequenceable
    {
        private SequenceKey _key;
        protected override SequenceKey Key => _key;

        public PluginSequencer(string qualifiedName)
        {
            _key = new SequenceKey(qualifiedName);
        }
    }
}