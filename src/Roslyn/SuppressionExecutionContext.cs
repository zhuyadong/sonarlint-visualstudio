using Microsoft.CodeAnalysis.Diagnostics;
using System.Text.RegularExpressions;

namespace SonarLint.VisualStudio.Roslyn.Suppressions
{
    internal interface IExecutionContext
    {
        /// <summary>
        /// Returns true if the code is being executed in process in which Roslyn analyzers
        /// are being exectued, otherwise false.
        /// </summary>
        bool IsCodeAnalysisProcess { get;}

        /// <summary>
        /// Returns true if the current solution is in Connected Mode, otherwise false.
        /// </summary>
        bool IsInConnectedMode { get; }

        /// <summary>
        /// Returns the Sonar project key if the solution is in Connected Mode, otherwise returns null.
        /// </summary>
        string SonarProjectKey { get; }
    }

    internal class SuppressionExecutionContext : IExecutionContext
    {
        // https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference
        private const string Exp = @"\\.sonarlint\\(?<sonarkey>[^\\/]+)\\(CSharp|VisualBasic)\\SonarLint.xml$";
        private static readonly Regex SonarLintFileRegEx = new Regex(Exp, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        public static SuppressionExecutionContext Create(SuppressionAnalysisContext context)
        {
            // If the solution is in Connected Mode then there will be a generated SonarLint.xml file
            // in a predicable path
            // e.g.  D:\repos\sq\slvs\.sonarlint\sonarlint-visualstudio\CSharp\SonarLint.xml
            // where "slvs" is the solution directory, and "sonarlint-visualstudio" is the Sonar project key.

            // ... drive and initial directories ... \.sonarlint\{Sonar project key}\["CSharp|VisualBasic"]\SonarLint.xml

            foreach (var item in context.Options.AdditionalFiles)
            {
                var match = SonarLintFileRegEx.Match(item.Path);
                if (match.Success)
                {
                    return new SuppressionExecutionContext(true, true, match.Groups["sonarkey"].Value);
                }
            }

            return new SuppressionExecutionContext(false, false, null);
        }

        private SuppressionExecutionContext(bool isCodeAnalysisProcess, bool isInConnectedMode, string sonarProjectKey)
        {
            IsCodeAnalysisProcess = isCodeAnalysisProcess;
            IsInConnectedMode = isInConnectedMode;
            SonarProjectKey = sonarProjectKey;
        }

        #region IExecutionContext implementation

        public bool IsCodeAnalysisProcess { get; }
        public bool IsInConnectedMode { get; }
        public string SonarProjectKey { get; }

        #endregion IExecutionContext
    }
}
