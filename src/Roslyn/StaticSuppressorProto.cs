using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SonarLint.VisualStudio.Roslyn.Suppressions
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal class StaticSuppressorProto : DiagnosticSuppressor
    {
        private static readonly ImmutableArray<SuppressionDescriptor> GenDescriptors = GeneratedSuppressorsDescriptors.GetDescriptors();

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions
        {
            get
            {
                this.Log($"Generated descriptors requested: {GenDescriptors.Length}");
                return GenDescriptors;
            }
        }

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
             this.Log($"In ReportSuppresions. ReportedDiagnostics: {context.ReportedDiagnostics.Length}");
            var timer = Stopwatch.StartNew();

            // With suppressions, this is the first point we get access to the code being compiled
            // i.e. the earliest we can work out whether solution is in Connected Mode and if so
            // which suppressions are relevant.

            var executionContext = SuppressionExecutionContext.Create(context);
            if (!executionContext.IsInConnectedMode)
            {
                this.Log($"Solution is not in connected mode");
                return;
            }

            foreach (var diag in context.ReportedDiagnostics)
            {
                if (Shared.SuppressionChecker.IsSuppressed(diag))
                {
                    // Find the appropriate suppression
                    var suppressionDesc = SupportedSuppressions.First(x => x.SuppressedDiagnosticId == diag.Id);
                    context.ReportSuppression(Suppression.Create(suppressionDesc, diag));
                }
            }

            timer.Stop();
            this.Log($"     ReportSuppressions elapsed time (ms): {timer.ElapsedMilliseconds}");
        }
    }

}
