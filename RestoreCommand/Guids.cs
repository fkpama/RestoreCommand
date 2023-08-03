using System;

namespace RestoreCommand
{
    internal class Guids
    {
        public const string RestoreCommandGuidString = "25B53EE2-3154-45FA-A0A6-F0DF6E84D2EA";
        public static Guid RestoreCommandGuid = new Guid(RestoreCommandGuidString);
        internal static int RestoreCommandId = 0x0100;
        internal static int UnloadDependencyTreeCommandId = 0x0101;
    }
}