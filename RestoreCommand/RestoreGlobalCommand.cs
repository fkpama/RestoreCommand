using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace RestoreCommand
{
    internal class RestoreGlobalCommand
    {
        internal static RestoreGlobalCommand Instance;
        private IAsyncServiceProvider package;
        private readonly IVsMonitorSelection monitorSelection;

        public RestoreGlobalCommand(IAsyncServiceProvider package,
                                    IVsMonitorSelection monitorSelection,
                                    OleMenuCommandService commandService)
        {
            this.package = package;
            this.monitorSelection = monitorSelection;
            var cmdid = new CommandID(Guids.RestoreCommandGuid, Guids.RestoreCommandId);
            var cmd = new OleMenuCommand(this.Execute, cmdid);
            cmd.BeforeQueryStatus += QueryStatus;
            commandService.AddCommand(cmd);
        }

        private void QueryStatus(object sender, EventArgs e)
        {
            var cmd = (OleMenuCommand)sender;
            var items = this.monitorSelection.GetSelectedItems().ToArray();
            cmd.Visible = false;
            if (items.All(x => x.itemid == (uint)VSConstants.VSITEMID.Root))
            {
                if (items.Any(x => Utils.CanRestore(hier: x.pHier)))
                {
                    cmd.Visible = true;
                    cmd.Supported = true;
                }
            }
        }

        internal static void Initialize(IAsyncServiceProvider package, IVsMonitorSelection monitorSelection, OleMenuCommandService commandService, CancellationToken cancellationToken)
        {
            Instance = new RestoreGlobalCommand(package, monitorSelection, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            var items = this.monitorSelection
                .GetSelectedItems()
                .Where(x => x.itemid == (uint)VSConstants.VSITEMID.Root)
                .ToArray();

            foreach(var item in items)
            {
                var projectPath = item.pHier.GetProjectPathSafe();
                if (string.IsNullOrWhiteSpace(projectPath)
                    || !File.Exists(projectPath))
                    continue;

                var dir = Path.GetDirectoryName(projectPath);
                var objDir = Path.Combine(dir, "obj");
                var binDir = Path.Combine(dir, "bin");

                Execute(objDir, binDir, projectPath);
            }
        }

        internal void Execute(string baseIntPath,
                              string baseOutputPath,
                              string projectFullPath)
        {
            var dir = Path.GetDirectoryName(projectFullPath);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"restore -f --no-cache \"{projectFullPath}\"",
                UseShellExecute = false,
                WorkingDirectory = dir,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            //await this.project.ProjectService
            //    .UnloadProjectAsync(this.project)
            //    .ConfigureAwait(false);
            IVsOutputWindowPane pane = null;
            try
            {
                if (Directory.Exists(baseIntPath))
                    Directory.Delete(baseIntPath, true);
                if (Directory.Exists(baseOutputPath))
                    Directory.Delete(baseOutputPath, true);


                using (var process = Process.Start(psi))
                {
                    process.OutputDataReceived += (o, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(e.Data))
                            return;
                        _ = doOutput(e.Data.Trim());
                    };
                    process.ErrorDataReceived += (o, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(e.Data))
                            return;
                        _ = doOutput(e.Data.Trim());
                    };
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    process.WaitForExit();
                    _ = doOutput($"Restore command exit code: {process.ExitCode}");
                }
            }
            finally
            {
                //await this.project.ProjectService
                //    .LoadProjectAsync(this.project.FullPath)
                //    .ConfigureAwait(false);
            }

            async Task doOutput(string data)
            {
                //var threadingService = project.Services.ThreadingPolicy;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (pane is null)
                {
                    pane = VsShellUtilities.GetOutputWindowPane(ServiceProvider.GlobalProvider, VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid);
                    ErrorHandler.ThrowOnFailure(pane.Clear());
                    ErrorHandler.ThrowOnFailure(pane.Activate());
                }
                pane.OutputStringThreadSafe($"{data}\n");
            }
        }
    }
}
