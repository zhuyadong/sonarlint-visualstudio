using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging.Interop;

namespace SonarLint.VisualStudio.Integration.Vsix
{
    public static class ImagesMonikers
    {
        private static readonly Guid ManifestGuid = new Guid("7eb86d9a-3e73-4fa9-a109-0d39ba31678e");

        private const int ProjectIcon = 0;

        public static ImageMoniker Major
        {
            get
            {
                return new ImageMoniker { Guid = ManifestGuid, Id = ProjectIcon };
            }
        }
    }
}
