using System.IO;
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

    /// <summary>
    /// Returns whether a specific diagnostic has been suppressed on the server
    /// </summary>
    internal sealed class SuppressIfInFirstTenLines : ISuppressionChecker
    {
        public bool IsSuppressed(Diagnostic diagnostic)
        {
            // TODO: use the current SLVS logic to match based on file, line hash, etc.
            // For now, we'll simply suppress issues that occur in the first ten lines of the file.

            if (diagnostic.Location == Location.None || !diagnostic.Location.IsInSource)
            {
                this.Log($"IsSuppressed check: no location/not in source. DiagId: {diagnostic.Id}}}");
                return false;
            }

            var fileLinePosSpan = diagnostic.Location.GetLineSpan();
            var startLineNumber = fileLinePosSpan.StartLinePosition.Line;
            var fileName = Path.GetFileName(fileLinePosSpan.Path);
            var isSuppressed = startLineNumber <= 10;

            this.Log($"IsSuppressed check: diagId: {diagnostic.Id}, start line: {startLineNumber}, isSuppressed: {isSuppressed}, file: {fileName}");

            return isSuppressed;
        }

    }
}
