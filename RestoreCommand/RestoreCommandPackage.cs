using System;
using System.ComponentModel.Design;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace RestoreCommand
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(RestoreCommandPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.CSharpProject_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.VBProject_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class RestoreCommandPackage : AsyncPackage
    {
        /// <summary>
        /// RestoreCommandPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "b8363c08-d258-4534-b862-f78266608d3e";
        private static bool initialized;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress)
                .ConfigureAwait(false);
            var monitorSelection = await this
                .GetServiceAsync<SVsShellMonitorSelection, IVsMonitorSelection>()
                .ConfigureAwait(false);
            var commandService = await this
                .GetServiceAsync<IMenuCommandService, OleMenuCommandService>()
                .ConfigureAwait(false);
            var solution = await this
                .GetServiceAsync<SVsSolution, IVsSolution4>()
                .ConfigureAwait(false);
            // Switch to the main thread - the call to AddCommand in ReloadDependencyTreeCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            ReloadDependencyTreeCommand.Initialize(this,
                                                   monitorSelection,
                                                   commandService,
                                                   solution);
            RestoreGlobalCommand.Initialize(this,
                                            monitorSelection,
                                            commandService,
                                            this.DisposalToken);
        }
        internal static void Initialize(IAsyncServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                lock(PackageGuidString)
                {
                    if (initialized)
                        return;
                    initialized = true;
                }
                var shell = await serviceProvider
                .GetServiceAsync<SVsShell, IVsShell7>()
                .ConfigureAwait(false);
                //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                var guid = new Guid(PackageGuidString);
                //ErrorHandler.ThrowOnFailure(shell.LoadPackage(ref guid, out var pkg));
                var task = shell.LoadPackageAsync(ref guid);
                task.Start();
                await task;
                // When initialized asynchronously, the current thread may be a background thread at this point.
                // Do any initialization that requires the UI thread after switching to the UI thread.
                //await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            }, cancellationToken);
        }

    }
}
