﻿using System;
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

        // Logs go to: C:\Users\<user>\AppData\Local\Temp\dpdt_extension.*.log
        private static readonly string vsLogFile = $"{Path.GetTempPath()}/AdjustNamespace.vs.log";

        [Conditional("DEBUG")]
        public static void LogVS(
            object? data = null,
            [CallerFilePath] string? file = null,
            [CallerMemberName] string? method = null)
            => Log(vsLogFile, file!, method!, data);

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
