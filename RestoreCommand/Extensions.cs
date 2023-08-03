using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace RestoreCommand
{
    internal static class Extensions
    {
        internal static bool IsLoaded(this IVsHierarchy hier)
            => !hier.IsUnloaded();
        internal static bool IsUnloaded(this IVsHierarchy hier)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ErrorHandler.ThrowOnFailure(hier
                .GetProperty((uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID.VSHPROPID_ProjectName,
                out var statusO));

            var dte = ServiceProvider.GlobalProvider.GetService<SDTE, DTE>();
            var status = (string)statusO;
            foreach(Project item in dte.Solution.Projects)
            {
                if (string.Equals(item.Name, status, System.StringComparison.Ordinal))
                {
                    return item.Kind == EnvDTE.Constants.vsProjectKindUnmodeled;
                }
            }

            throw new System.NotImplementedException();
            //if (!(status is Project p))
            //    throw new System.NotImplementedException();

            //var unloaded = p.Kind == EnvDTE.Constants.vsProjectKindUnmodeled;
            //return unloaded;
        }

        internal static string GetProjectPathSafe(this IVsHierarchy hier)
        {

            ErrorHandler.ThrowOnFailure(hier
                .GetProperty((uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID.VSHPROPID_ProjectName,
                out var statusO));

            var dte = ServiceProvider.GlobalProvider.GetService<SDTE, DTE>();
            var status = (string)statusO;
            var dir = Path.GetDirectoryName(dte.Solution.FullName);
            foreach(Project item in dte.Solution.Projects)
            {
                if (string.Equals(item.Name, status, StringComparison.Ordinal))
                {
                    return Path.Combine(dir, item.UniqueName);
                }
            }

            return null;
        }

        internal static IEnumerable<VSITEMSELECTION> GetSelectedItems(this IVsMonitorSelection monitorSelection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //AsyncServiceProvider
            //    .GlobalProvider
            //    .GetServiceAsync<SVsSolution,  IVsSolution>()
            //    .ConfigureAwait(false);
            ErrorHandler.ThrowOnFailure(
            monitorSelection.GetCurrentSelection(out var ppHier,
                                                      out var itemid,
                                                      out var mcs,
                                                      out var ppsc));

            if (ppsc != IntPtr.Zero)
                Marshal.Release(ppsc);
            switch ((VSConstants.VSITEMID)itemid)
            {
                case VSConstants.VSITEMID.Nil:
                    yield break;
                case VSConstants.VSITEMID.Selection:
                    ErrorHandler.ThrowOnFailure(
                    mcs.GetSelectionInfo(out var nbItems, out _));
                    var items = new VSITEMSELECTION[nbItems];
                    ErrorHandler.ThrowOnFailure(
                    mcs.GetSelectedItems(0, (uint)items.Length, items));
                    foreach (var item in items)
                        yield return item;
                    break;
                case VSConstants.VSITEMID.Root:
                    var hier = (IVsHierarchy)Marshal.GetObjectForIUnknown(ppHier);
                    yield return new VSITEMSELECTION
                    {
                        itemid = itemid,
                        pHier = hier
                    };
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

    }
}
