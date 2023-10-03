#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using nadena.dev.ndmf.model;

#endregion

namespace nadena.dev.ndmf.fluent
{
    public sealed partial class Sequence
    {
        private List<Action<SolverPass>> _pendingDependencies = new List<Action<SolverPass>>();

        private void OnNewPass(SolverPass pass)
        {
            foreach (var dep in _pendingDependencies)
            {
                dep(pass);
            }

            _pendingDependencies.Clear();
        }

        public Sequence BeforePlugin(string QualifiedName, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0)
        {
            _solverContext.Constraints.Add(new Constraint()
            {
                First = _sequenceEnd.PassKey,
                Second = _solverContext.GetPluginPhases(_phase, QualifiedName).PluginStart.PassKey,
                Type = ConstraintType.WeakOrder,
                DeclaredFile = sourceFile,
                DeclaredLine = sourceLine,
            });

            return this;
        }

        public Sequence BeforePlugin<T>(T plugin, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Plugin<T>, new()
        {
            return BeforePlugin(plugin.QualifiedName, sourceFile, sourceLine);
        }
        
        public Sequence BeforePlugin<T>([CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Plugin<T>, new()
        {
            return BeforePlugin(Plugin<T>.Instance.QualifiedName, sourceFile, sourceLine);
        }

        public Sequence AfterPlugin(string qualifiedName, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0)
        {
            _solverContext.Constraints.Add(new Constraint()
            {
                First = _solverContext.GetPluginPhases(_phase, qualifiedName).PluginEnd.PassKey,
                Second = _sequenceStart.PassKey,
                Type = ConstraintType.WeakOrder,
                DeclaredFile = sourceFile,
                DeclaredLine = sourceLine,
            });

            return this;
        }

        public Sequence AfterPlugin<T>(T plugin, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Plugin<T>, new()
        {
            return AfterPlugin(plugin.QualifiedName, sourceFile, sourceLine);
        }
        
        public Sequence AfterPlugin<T>([CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Plugin<T>, new()
        {
            return AfterPlugin(Plugin<T>.Instance.QualifiedName, sourceFile, sourceLine);
        }

        public Sequence WaitFor<T>(T pass, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Pass<T>, new()
        {
            _pendingDependencies.Add(nextPass =>
            {
                _solverContext.Constraints.Add(new Constraint()
                {
                    First = ((IPass) pass).PassKey,
                    Second = nextPass.PassKey,
                    Type = ConstraintType.WaitFor,
                    DeclaredFile = sourceFile,
                    DeclaredLine = sourceLine,
                });
            });

            return this;
        }

        public Sequence AfterPass(string qualifiedName, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0)
        {
            _pendingDependencies.Add(nextPass =>
            {
                _solverContext.Constraints.Add(new Constraint()
                {
                    First = nextPass.PassKey,
                    Second = _solverContext.Passes.Find(p => p.PassKey.QualifiedName == qualifiedName).PassKey,
                    Type = ConstraintType.WeakOrder,
                    DeclaredFile = sourceFile,
                    DeclaredLine = sourceLine,
                });
            });

            return this;
        }

        public Sequence AfterPass<T>(T pass, [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0) where T : Pass<T>, new()
        {
            return AfterPass(pass.QualifiedName, sourceFile, sourceLine);
        }
    }
}