using System.Collections.Generic;
using SonarQube.Client.Models;

namespace SonarLint.VisualStudio.Roslyn.Suppressions.NewCommon
{
    public class RoslynAnalysisSettings
    {
        public IList<ProjectInfo> Projects { get; set; }

        public IList<SonarQubeIssue> SonarQubeIssues { get; set; }
    }

    public class ProjectInfo
    {
        public string FilePath { get; set; }
        public string ProjectGuid { get; set; }
    }
}
