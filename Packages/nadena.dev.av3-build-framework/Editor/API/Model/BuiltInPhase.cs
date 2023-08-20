namespace nadena.dev.build_framework
{
    public enum BuiltInPhase
    {
        /// <summary>
        /// This phase is the first built-in phase to run, and is intended to be used for initialization routines
        /// which must run before the avatar is manipulated. For example, it can be used to resolve serialized string
        /// paths to objects.
        /// </summary>
        Resolving,
        
        /// <summary>
        /// This phase is intended to be used to generate objects that will be processed by other plugins. For example,
        /// you could generate components used in Modular Avatar here. The intent is that, generally, you will not
        /// manipulate existing objects in this phase.
        /// </summary>
        Generating,
        
        /// <summary>
        /// This phase is where transformations are applied to the avatar - for example, you could use this phase to
        /// merge outfits and other such large-scale manipulations.
        /// </summary>
        Transforming,
        
        /// <summary>
        /// This phase is where optimizations are applied to the avatar - for example, you could use this phase to
        /// remove unused game objects.
        /// </summary>
        Optimization
    }
}