using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using SonarLint.VisualStudio.Core.Binding;
using SonarLint.VisualStudio.Core.Suppression;
using SonarLint.VisualStudio.Roslyn.Suppressions.NewCommon;

namespace SonarLint.VisualStudio.Integration.Vsix.Delegates
{
    internal class NewCSharpSuppressionsManager
    {
        private readonly IActiveSolutionBoundTracker activeSolutionBoundTracker;
        private readonly ISonarQubeIssuesProvider sonarQubeIssuesProvider;
        private readonly IVsSolution vsSolution;
        private readonly ILogger logger;

        public NewCSharpSuppressionsManager(IActiveSolutionBoundTracker activeSolutionBoundTracker,
            IVsSolution vsSolution,
            ILogger logger,
            ISonarQubeIssuesProvider sonarQubeIssuesProvider)
        {
            this.activeSolutionBoundTracker = activeSolutionBoundTracker ?? throw new ArgumentNullException(nameof(activeSolutionBoundTracker));
            this.vsSolution = vsSolution ?? throw new ArgumentNullException(nameof(vsSolution));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.sonarQubeIssuesProvider = sonarQubeIssuesProvider ?? throw new ArgumentNullException(nameof(sonarQubeIssuesProvider));

            activeSolutionBoundTracker.SolutionBindingChanged += OnSolutionBindingChanged;
            activeSolutionBoundTracker.SolutionBindingUpdated += OnSolutionBindingUpdated;

            RefreshWorkflow(this.activeSolutionBoundTracker.CurrentConfiguration);
        }

        private void OnSolutionBindingChanged(object sender, ActiveSolutionBindingEventArgs e)
        {
            RefreshWorkflow(e.Configuration);
        }

        private void OnSolutionBindingUpdated(object sender, EventArgs e)
        {
            Debug.Assert(this.activeSolutionBoundTracker.CurrentConfiguration.Mode != SonarLintMode.Standalone,
                "Not expecting to received the solution binding update event in standalone mode.");

            RefreshWorkflow(this.activeSolutionBoundTracker.CurrentConfiguration);
        }

        private void RefreshWorkflow(BindingConfiguration configuration)
        {
            switch (configuration?.Mode)
            {
                case SonarLintMode.LegacyConnected:
                case SonarLintMode.Connected:
                    this.logger.WriteLine(Resources.Strings.AnalyzerManager_InConnectedMode);

                    break;
                default:
                    // no-op 
                    break;
            }
        }

        internal /* for testing */ static IDictionary<string, string> BuildProjectPathToIdMap(IVsSolution solution)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. Call with nulls to get the number of files
            const uint grfGetOpsIncludeUnloadedFiles = 0; // required since the projects might not have finished loading
            uint fileCount;
            var result = solution.GetProjectFilesInSolution(grfGetOpsIncludeUnloadedFiles, 0, null, out fileCount);
            if (ErrorHandler.Failed(result))
            {
                return map;
            }

            // 2. Size array and call again to get the data
            string[] fileNames = new string[fileCount];
            result = solution.GetProjectFilesInSolution(grfGetOpsIncludeUnloadedFiles, fileCount, fileNames, out _);
            if (ErrorHandler.Failed(result))
            {
                return map;
            }

            IVsSolution5 soln5 = (IVsSolution5)solution;

            foreach (string projectFile in fileNames)
            {
                // We need to use the same project id that is used by the Scanner for MSBuild.
                // For non-.Net Core projects, the scanner will use the <ProjectGuid> value in the project.
                // .Net Core projects don't have a <ProjectGuid> property so the scanner uses the GUID allocated
                // to the project in the solution file.
                // Fortunately, this is the logic used by soln5.GetGuidOfProjectFile... so we can just use that.
                Guid projectGuid = soln5.GetGuidOfProjectFile(projectFile);

                // Overwrite duplicate entries. This won't happen for real project files: it will only happen
                // for solution folders with duplicate names, which we don't care about.
                map[projectFile] = projectGuid.ToString().Replace("{", "").Replace("}", "");
            }
            return map;
        }

    }
}
