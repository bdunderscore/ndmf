using System;

namespace nadena.dev.Av3BuildFramework
{
    public enum Phase
    {
        Check,
        Generate,
        Transform,
        Optimize
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginPhase : System.Attribute
    {
        private readonly Phase _phase;
        
        public PluginPhase(Phase phase)
        {
            _phase = phase;
        }

        public Phase Phase => _phase;
    }
}