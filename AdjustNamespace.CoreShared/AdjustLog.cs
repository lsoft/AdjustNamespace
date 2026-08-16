using System;
using System.Diagnostics;
using System.IO;

namespace AdjustNamespace
{
    /// <summary>
    /// The detailed diagnostics of the adjusting (<c>[Adjust] ...</c> lines).
    ///
    /// <see cref="Debug.WriteLine(string)"/> alone is invisible to the console utility
    /// (and is compiled out of a Release build), so the same messages go through here:
    /// they still reach a debugger, and once <see cref="Enable"/> has been called they
    /// also reach a file / a writer the host has chosen.
    /// </summary>
    public static class AdjustLog
    {
        private static readonly object Gate = new object();

        private static TextWriter? _writer;

        /// <summary>
        /// The path of the log file opened by <see cref="EnableToFile"/>, or <c>null</c>.
        /// </summary>
        public static string? LogFilePath
        {
            get;
            private set;
        }

        /// <summary>
        /// Send every further <see cref="WriteLine"/> into the given writer
        /// (in addition to <see cref="Debug"/>).
        /// </summary>
        public static void Enable(
            TextWriter writer
            )
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            lock (Gate)
            {
                _writer = writer;
            }
        }

        /// <summary>
        /// Open (or recreate) a log file and send every further <see cref="WriteLine"/> there.
        /// </summary>
        public static void EnableToFile(
            string path
            )
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            //a fresh file per run: a previous session must not mix with this one
            var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read
                );
            var writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };

            lock (Gate)
            {
                _writer?.Dispose();
                _writer = writer;
                LogFilePath = path;
            }
        }

        /// <summary>
        /// Write one diagnostic line.
        /// </summary>
        public static void WriteLine(
            string message
            )
        {
            Debug.WriteLine(message);

            lock (Gate)
            {
                _writer?.WriteLine(message);
            }
        }
    }
}
