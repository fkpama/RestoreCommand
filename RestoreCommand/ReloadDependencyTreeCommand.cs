using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace RestoreCommand
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class ReloadDependencyTreeCommand
    {
        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly IAsyncServiceProvider package;
        private readonly IVsSolution4 solution;
        private readonly IVsMonitorSelection monitorSelection;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReloadDependencyTreeCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private ReloadDependencyTreeCommand(IAsyncServiceProvider package,
                                            IVsSolution4 solution,
                                            IVsMonitorSelection monitorSelection,
                                            OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            this.solution = solution;
            this.monitorSelection = monitorSelection;
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(Guids.RestoreCommandGuid, Guids.UnloadDependencyTreeCommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += QueryStatus;
            commandService.AddCommand(menuItem);
        }

        private void QueryStatus(object sender, EventArgs e)
        {
            var cmd = (OleMenuCommand)sender;
            var items = this.monitorSelection.GetSelectedItems().ToArray();
            cmd.Visible = false;
            cmd.Supported = false;
            if (items.All(x => x.itemid == (uint)VSConstants.VSITEMID.Root))
            {
                if (items.All(x => x.pHier.IsLoaded()))
                {
                    cmd.Visible = true;
                    cmd.Supported = true;
                }
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static ReloadDependencyTreeCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(IAsyncServiceProvider services,
                                      IVsMonitorSelection monitorSelection,
                                      OleMenuCommandService commandService,
                                      IVsSolution4 solution)
        {

            Instance = new ReloadDependencyTreeCommand(services,
                                                       solution,
                                                       monitorSelection,
                                                       commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //AsyncServiceProvider
            //    .GlobalProvider
            //    .GetServiceAsync<SVsSolution,  IVsSolution>()
            //    .ConfigureAwait(false);
            var lst = new List<DependencyTreeNode>();
            var session = new ReloadSession();
            foreach (var item in this.monitorSelection.GetSelectedItems())
            {
                var node = GetNode(item.pHier, session);
                lst.Add(node);

            }
            foreach (var node in lst)
                Reload(node);
        }

        private void Reload(DependencyTreeNode node)
        {
            foreach(var n in node.EnumDeps())
            {
                if(node.Session.TryProcess(n))
                    _ = Unload(n);
            }
        }
        private async Task Unload(DependencyTreeNode node)
        {
            var guid = node.Guid;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var poj = node.Hier.AsUnconfiguredProject();
            if (await poj.GetIsDirtyAsync())
                await poj.SaveAsync();
            ErrorHandler.ThrowOnFailure(this.solution.UnloadProject(ref guid, 0));
        }
        private DependencyTreeNode GetNode(IVsHierarchy hier, ReloadSession session)
        {
            List<DependencyTreeNode> deps = null;
            const uint itemid = (uint)VSConstants.VSITEMID.Root;
            if (hier is IVsDependencyProvider dependencyProvider)
            {
                ErrorHandler.ThrowOnFailure(hier.GetCanonicalName(itemid, out var projPath));
                var dir = Path.GetDirectoryName(projPath);
                ErrorHandler.ThrowOnFailure(dependencyProvider.EnumDependencies(out var enumDeps));

                var ar = new IVsDependency[1];
                while (ErrorHandler.Succeeded(ErrorHandler.ThrowOnFailure(enumDeps.Next((uint)ar.Length, ar, out var fetched)))
                    && fetched > 0)
                {
                    var dep = ar[0];
                    ErrorHandler.ThrowOnFailure(dep.get_Type(out var tp));
                    if (tp == VSConstants.VsDependencyTypeGuid.BuildProject_guid)
                    {
                        ErrorHandler.ThrowOnFailure(dep.get_CanonicalName(out var pbstrCanonicalName));
                        var fullPath = Path.GetFullPath(Path.Combine(dir, pbstrCanonicalName));
                        var project = VsShellUtilities
                            .GetProject((IServiceProvider)ServiceProvider, fullPath);
                        if (project is null)
                        {
                            continue;
                        }
                        if (deps is null)
                            deps = new List<DependencyTreeNode>();
                        deps.Add(GetNode(project, session));
                    }
                }
            }

            return new DependencyTreeNode(hier, deps, session);
        }
    }

    struct DependencyTreeNode
    {
        public DependencyTreeNode(IVsHierarchy hier,
            List<DependencyTreeNode> deps,
            ReloadSession session)
        {
            this.Hier = hier;
            this.Deps = deps;
            this.Session = session;
            ErrorHandler.ThrowOnFailure(
            this.Hier.GetGuidProperty((uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                out var guid));
            ErrorHandler.ThrowOnFailure(
            this.Hier.GetProperty((uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID.VSHPROPID_ProjectName,
                out var name));
            this.Guid = guid;
            this.Name = name;
        }

        public IVsHierarchy Hier { get; }
        public List<DependencyTreeNode> Deps { get; }
        public ReloadSession Session { get; }

        public bool HasDeps => this.Deps != null;

        public Guid Guid { get; }
        public object Name { get; }

        internal IEnumerable<DependencyTreeNode> EnumDeps()
        {
            if (!Session.CanProcess(this))
                return Enumerable.Empty<DependencyTreeNode>();

            if (this.Deps is null)
                return new[] { this };

            var lst = this.Deps.SelectMany(x => x.EnumDeps()).ToList();
            foreach(var dep in lst.ToArray())
            {
                if (!Session.CanProcess(dep))
                    lst.Remove(dep);
            }
            if (Session.CanProcess(this))
                lst.Add(this);
            return lst;
        }
    }

    class ReloadSession
    {
        private readonly List<Guid> processed = new List<Guid>();
        internal bool CanProcess(DependencyTreeNode node)
        {
            lock(processed)
            {
                if (this.processed.Contains(node.Guid))
                    return false;
            }
            return true;
        }
        internal bool TryProcess(DependencyTreeNode dep)
        {
            lock (processed)
            {
                if (this.processed.Contains(dep.Guid))
                    return false;

                this.processed.Add(dep.Guid);
                return true;
            }
        }
    }
}
