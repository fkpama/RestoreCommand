using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace RestoreCommand
{
    internal static class Utils
    {
        internal static bool CanRestore(IVsHierarchy hier)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ErrorHandler.ThrowOnFailure(
            hier.GetCanonicalName((uint)VSConstants.VSITEMID.Root,
                out var name));
            return CanRestore(projFilePath: name);
        }

        private static bool CanRestore(string projFilePath)
        {
            var ext = Path.GetExtension(projFilePath);
            return IsDotnetExtension(ext);
        }

        private static bool IsDotnetExtension(string ext)
            => string.Equals(".csproj", ext, StringComparison.OrdinalIgnoreCase)
            || string.Equals(".vbproj", ext, StringComparison.OrdinalIgnoreCase);

    }
}
