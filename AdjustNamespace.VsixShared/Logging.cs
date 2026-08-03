using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AdjustNamespace
{
    /// <summary>
    /// Logging helpers.
    /// Taken from  https://github.com/bert2/microscope completely.
    /// Take a look to that repo, it's amazing!
    /// </summary>
    public static class Logging
    {
        private static readonly object @lock = new object();

        // Logs go to: C:\Users\<user>\AppData\Local\Temp\AdjustNamespace.vs.log
        private static readonly string vsLogFile = $"{Path.GetTempPath()}/AdjustNamespace.vs.log";

        /// <summary>
        /// Write a line into the log file. Debug builds only.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogVS(
            object? data = null,
            [CallerFilePath] string? file = null,
            [CallerMemberName] string? method = null)
            => Log(vsLogFile, file!, method!, data);

        /// <summary>
        /// Append a line (with a timestamp, a process id, a thread id and a caller)
        /// into the given log file.
        /// </summary>
        public static void Log(
            string logFile,
            string callingFile,
            string callingMethod,
            object? data = null)
        {
            lock (@lock)
            {
                File.AppendAllText(
                    logFile,
                    $"{DateTime.Now:HH:mm:ss.fff} "
                    + $"{Process.GetCurrentProcess().Id,5} "
                    + $"{Thread.CurrentThread.ManagedThreadId,3} "
                    + $"{Path.GetFileNameWithoutExtension(callingFile)}.{callingMethod}()"
                    + $"{(data == null ? "" : $": {data}")}\n");
            }
        }
    }
}
