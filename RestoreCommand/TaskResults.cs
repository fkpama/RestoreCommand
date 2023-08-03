using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;

namespace RestoreCommand
{
    internal static class TaskResults
    {
        //
        // Summary:
        //     A compelted task to return empty string.
        internal static readonly Task<string> EmptyString = Task.FromResult(string.Empty);

        //
        // Summary:
        //     A completed task with a true result.
        internal static readonly Task<bool> True = Task.FromResult(true);

        //
        // Summary:
        //     A completed task with a false result.
        internal static readonly Task<bool> False = Task.FromResult(false);

        internal static readonly Task<CommandStatusResult> Unhandled = Task.FromResult(CommandStatusResult.Unhandled);
        internal static readonly Task<CommandStatusResult> Handled = Task.FromResult(new CommandStatusResult
        {
            Handled = true,
            Status = CommandStatus.Enabled | CommandStatus.Supported
        });
    }
}
