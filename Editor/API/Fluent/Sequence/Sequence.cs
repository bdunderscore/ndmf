#region

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using nadena.dev.ndmf.model;
using nadena.dev.ndmf.preview;

#endregion

namespace nadena.dev.ndmf.fluent
{
    /// <summary>
    /// Callback invoked when an anonymous pass is executed.
    /// </summary>
    public delegate void InlinePass(BuildContext context);

    /// <summary>
    /// Fluent context type used to declare constraints on the execution order of passes.
    ///
    /// <code>
    /// sequence.Run(typeof(MyPass))      // returns DeclaringPass
    ///   .BeforePass(typeof(OtherPass)); // valid only on DeclaringPass
    ///   .Then.Run(typeof(OtherPass));
    /// </code>
    /// </summary>
    public sealed class DeclaringPass
    {
        private readonly SolverContext _solverContext;
        private readonly BuildPhase _phase;
        private readonly SolverPass _pass;
        private readonly Sequence _seq;

        /// <summary>
        /// Returns the original sequence that returned this DeclaringPass. This is useful for chaining multiple
        /// pass declarations, like so:
        ///
        /// <code>
        /// InPhase(Generating)
        ///   .Run(typeof(PassOne))
        ///   .Then.Run(typeof(PassTwo));
        /// </code>
        /// </summary>
        [SuppressMessage("ReSharper", "ConvertToAutoProperty")]
        public Sequence Then => _seq;

        internal DeclaringPass(SolverPass pass, SolverContext solverContext, BuildPhase phase, Sequence seq)
        {
            _pass = pass;
            _solverContext = solverContext;
            _phase = phase;
            _seq = seq;
        }

        public DeclaringPass PreviewingWith(params IRenderFilter[] filters)
        {
            foreach (var filter in filters)
            {
                _pass.RenderFilters.Add(filter);
            }

            return this;
        }

        /// <summary>
        /// Declares that the pass you just declared must run before a particular other plugin.
        ///
        /// It's recommended to avoid using this to refer to passes in other plugins, as the internal class names
        /// of those plugins may not be a stable API.
        /// </summary>
        /// <param name="QualifiedName">The qualified name of the other plugin</param>
        /// <returns>This DeclaringPass context</returns>
        public DeclaringPass BeforePlugin(string QualifiedName, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0)
        {
            _solverContext.Constraints.Add(new Constraint()
            {
                First = _pass.PassKey,
                Second = _solverContext.GetPluginPhases(_phase, QualifiedName).PluginStart.PassKey,
                Type = ConstraintType.WeakOrder,
                DeclaredFile = sourceFile,
                DeclaredLine = sourceLine,
            });

            return this;
        }

        /// <summary>
        /// Declares that the pass you just declared must run before a particular other plugin.
        /// </summary>
        /// <param name="QualifiedName">The singleton of the other plugin</param>
        /// <returns>This DeclaringPass context</returns>
        public DeclaringPass BeforePlugin<T>(T plugin, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Plugin<T>, new()
        {
            return BeforePlugin(plugin.QualifiedName, sourceFile, sourceLine);
        }


        /// <summary>
        /// Declares that the pass you just declared must run after a particular other pass.
        ///
        /// It's recommended to avoid using this to refer to passes in other plugins, as the internal class names
        /// of those plugins may not be a stable API.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <param name="sourceFile"></param>
        /// <param name="sourceLine"></param>
        /// <returns></returns>
        public DeclaringPass BeforePass(string qualifiedName, string sourceFile = "", int sourceLine = 0)
        {
            _solverContext.Constraints.Add(new Constraint()
            {
                First = _pass.PassKey,
                Second = new PassKey(qualifiedName),
                Type = ConstraintType.WeakOrder,
                DeclaredFile = sourceFile,
                DeclaredLine = sourceLine,
            });

            return this;
        }

        /// <summary>
        /// Declares that the pass you just declared must run before a particular other pass.
        /// </summary>
        /// <param name="QualifiedName">The singleton of the other plugin</param>
        /// <returns>This DeclaringPass context</returns>
        public DeclaringPass BeforePass<T>(T pass, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Pass<T>, new()
        {
            return BeforePass(pass.QualifiedName, sourceFile, sourceLine);
        }
    }

    /// <summary>
    /// Represents a sequence of passes that will execute in order (but not necessarily directly after one another),
    /// and allows this sequence to be built up.
    /// </summary>
    public sealed partial class Sequence
    {
        private readonly IPluginInternal _plugin;
        private readonly string _sequenceBaseName;
        private readonly SolverContext _solverContext;
        private readonly BuildPhase _phase;
        private readonly SolverPass _sequenceStart, _sequenceEnd;

        private SolverPass _priorPass = null;

        private int inlinePassIndex = 0;

        internal Sequence(BuildPhase phase, SolverContext solverContext, IPluginInternal plugin,
            string sequenceBaseName)
        {
            _phase = phase;
            _solverContext = solverContext;
            _plugin = plugin;
            _sequenceBaseName = sequenceBaseName;

            var innate = _solverContext.GetPluginPhases(_phase, plugin.QualifiedName);
            _sequenceStart = CreateSequencingPass("<sequence start>", _ignored => { }, "", 0);
            _sequenceEnd = CreateSequencingPass("<sequence end>", _ignored => { }, "", 0);

            _solverContext.Constraints.Add(
                new Constraint()
                {
                    First = innate.PluginStart.PassKey,
                    Second = _sequenceStart.PassKey,
                    Type = ConstraintType.WeakOrder,
                }
            );
            _solverContext.Constraints.Add(
                new Constraint()
                {
                    First = _sequenceEnd.PassKey,
                    Second = innate.PluginEnd.PassKey,
                    Type = ConstraintType.WeakOrder,
                }
            );
            _solverContext.Constraints.Add(
                new Constraint()
                {
                    First = _sequenceStart.PassKey,
                    Second = _sequenceEnd.PassKey,
                    Type = ConstraintType.WeakOrder,
                }
            );

            _solverContext.AddPass(_sequenceStart);
            _solverContext.AddPass(_sequenceEnd);
        }

        private SolverPass CreateSequencingPass(string displayName, InlinePass callback, string sourceFile,
            int sourceLine)
        {
            var anonPass = new AnonymousPass(_sequenceBaseName + "/" + displayName + "#" + inlinePassIndex++,
                displayName,
                callback);
            var pass = new SolverPass(_plugin, anonPass, _phase, _compatibleExtensions, _requiredExtensions);
            anonPass.IsPhantom = true;

            return pass;
        }

        /// <summary>
        /// Registers a pass to run in this sequence.
        /// </summary>
        /// <param name="pass">The pass to run</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>A DeclaringPass object that can be used to set BeforePass/BeforePlugin constraints</returns>
        public DeclaringPass Run<T>(T pass, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Pass<T>, new()
        {
            return InternalRun(pass, sourceFile, sourceLine);
        }

        private DeclaringPass InternalRun(IPass pass, string sourceFile, int sourceLine)
        {
            var solverPass = new SolverPass(_plugin, pass, _phase, _compatibleExtensions, _requiredExtensions);
            _solverContext.AddPass(solverPass);

            _solverContext.Constraints.Add(
                new Constraint()
                {
                    First = _sequenceStart.PassKey,
                    Second = solverPass.PassKey,
                    Type = ConstraintType.WeakOrder,
                }
            );

            _solverContext.Constraints.Add(
                new Constraint()
                {
                    First = solverPass.PassKey,
                    Second = _sequenceEnd.PassKey,
                    Type = ConstraintType.WeakOrder,
                }
            );

            if (_priorPass != null)
            {
                _solverContext.Constraints.Add(
                    new Constraint()
                    {
                        First = _priorPass.PassKey,
                        Second = solverPass.PassKey,
                        Type = ConstraintType.Sequence,
                    }
                );
            }

            _priorPass = solverPass;
            OnNewPass(solverPass);

            return new DeclaringPass(solverPass, _solverContext, _phase, this);
        }

        /// <summary>
        /// Declares a pass using an inline callback. This pass cannot be referenced by other plugins for the purpose
        /// of setting BeforePass/AfterPass constraints.
        /// </summary>
        /// <param name="displayName">The name of the pass to show in debug output</param>
        /// <param name="inlinePass">A callback to invoke when the pass is executed</param>
        /// <returns></returns>
        public DeclaringPass Run(string displayName, InlinePass inlinePass, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0)
        {
            var anonPass = new AnonymousPass(_sequenceBaseName + "/anonymous#" + inlinePassIndex++, displayName,
                inlinePass);
            return InternalRun(anonPass, sourceFile, sourceLine);
        }
    }
}