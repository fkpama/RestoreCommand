using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace RestoreCommand
{
    internal interface ICommand
    {
        Task<bool> ExecuteAsync(UnconfiguredProject project, CancellationToken cancellationToken);
        Task<CommandStatusResult> GetCommandStatusAsync(UnconfiguredProject project, IImmutableSet<IProjectTree> nodes);
    }

    internal abstract class CommandBase : ICommand
    {
        public abstract Task<bool> ExecuteAsync(UnconfiguredProject project, CancellationToken cancellationToken);

        public virtual Task<CommandStatusResult> GetCommandStatusAsync(UnconfiguredProject project, IImmutableSet<IProjectTree> nodes)
            => nodes.All(x => x.IsRoot()) ? TaskResults.Handled : TaskResults.Unhandled;
    }

    internal class UnloadDependencyTreeCommand : CommandBase
    {
        public override Task<bool> ExecuteAsync(UnconfiguredProject project, CancellationToken cancellationToken)
        {
            return TaskResults.False;
        }

        public override Task<CommandStatusResult> GetCommandStatusAsync(UnconfiguredProject project, IImmutableSet<IProjectTree> nodes)
        {
            if (nodes.Count != 1)
                return TaskResults.Unhandled;

            var node = nodes.ElementAt(0);
            return node.IsRoot() ? TaskResults.Handled : TaskResults.Unhandled;
        }
    }

    internal class RestoreCommand : CommandBase
    {
        public override async Task<bool> ExecuteAsync(UnconfiguredProject project, CancellationToken cancellationToken)
        {
            var properties = project
                .Services
                .ActiveConfiguredProjectProvider
                .ActiveConfiguredProject
                .Services
                .ProjectPropertiesProvider
                .GetCommonProperties();

            var baseIntPath = await properties
                .GetEvaluatedPropertyValueAsync("BaseIntermediateOutputPath")
                .ConfigureAwait(false);
            var baseOutputPath = await properties
                .GetEvaluatedPropertyValueAsync("BaseOutputPath")
                .ConfigureAwait(false);
            var dir = Path.GetDirectoryName(project.FullPath);
            baseOutputPath = Path.Combine(dir, baseOutputPath);
            baseIntPath = Path.Combine(dir, baseIntPath);
            RestoreGlobalCommand.Instance
                .Execute(baseIntPath, baseOutputPath, project.FullPath);

            _ = project.Services.ProjectReloader
                .ReloadIfNecessaryAsync(project, false)
                .ConfigureAwait(false);
            return true;
        }
    }
}
