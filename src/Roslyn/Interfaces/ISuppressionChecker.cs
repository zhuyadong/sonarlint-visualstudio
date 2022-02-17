using Microsoft.CodeAnalysis;

namespace SonarLint.VisualStudio.Roslyn.Suppressions.Interfaces
{
    internal interface ISuppressionChecker
    {
        /// <summary>
        /// Returns true if the diagnostic should be suppressed, otherwise false
        /// </summary>
        bool IsSuppressed(Diagnostic diagnostic);
    }


    internal sealed class NoOpSuppressionChecker : ISuppressionChecker
    {
        public bool IsSuppressed(Diagnostic diagnostic) => false;
    }

}
