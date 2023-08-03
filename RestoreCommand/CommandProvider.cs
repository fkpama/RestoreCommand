using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace RestoreCommand
{
    [AppliesTo(ProjectCapabilities.Cps)]
    [ExportCommandGroup(Guids.RestoreCommandGuidString)]
    internal class CommandProvider : IAsyncCommandGroupHandler
    {
        private static Dictionary<long, ICommand> s_commands = new Dictionary<long, ICommand>()
        {
            [Guids.RestoreCommandId] = new RestoreCommand(),
            [Guids.UnloadDependencyTreeCommandId] = new UnloadDependencyTreeCommand()
        };
        private readonly UnconfiguredProject project;

        [ImportingConstructor]
        public CommandProvider(UnconfiguredProject project)
        {
            this.project = project;
            RestoreCommandPackage
                .Initialize(AsyncServiceProvider.GlobalProvider,
                VsShellUtilities.ShutdownToken);
        }

        public Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes,
                                                               long commandId,
                                                               bool focused,
                                                               string commandText,
                                                               CommandStatus progressiveStatus)
        {
            if (s_commands.TryGetValue(commandId, out var val))
            {
                return val.GetCommandStatusAsync(this.project, nodes);
            }
            return TaskResults.Unhandled;
        }

        public async Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> nodes,
                                                long commandId,
                                                bool focused,
                                                long commandExecuteOptions,
                                                IntPtr variantArgIn,
                                                IntPtr variantArgOut)
        {
            if (!s_commands.TryGetValue(commandId, out var command))
            {
                return false;
            }

            return await command
                .ExecuteAsync(this.project, VsShellUtilities.ShutdownToken);
        }
    }
}
