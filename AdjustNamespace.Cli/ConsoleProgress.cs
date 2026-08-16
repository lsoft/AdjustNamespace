using AdjustNamespace.Adjusting.Session;
using System.IO;

namespace AdjustNamespace.Cli
{
    /// <summary>
    /// The progress of a session as the console shows it: every adjusted file is worth a line,
    /// the cleanup walks through the whole solution and is worth a single one.
    /// </summary>
    public sealed class ConsoleProgress : IProgress<AdjustProgress>
    {
        private readonly TextWriter _output;
        private readonly string _rootFolder;
        private readonly bool _verbose;

        private bool _cleanupAnnounced;

        /// <param name="output">Where the lines go.</param>
        /// <param name="rootFolder">Folder the reported paths are shown relative to.</param>
        /// <param name="verbose">Report every file of the cleanup as well.</param>
        public ConsoleProgress(
            TextWriter output,
            string rootFolder,
            bool verbose
            )
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (rootFolder is null)
            {
                throw new ArgumentNullException(nameof(rootFolder));
            }

            _output = output;
            _rootFolder = rootFolder;
            _verbose = verbose;
        }

        /// <inheritdoc/>
        public void Report(AdjustProgress value)
        {
            if (value.Stage == AdjustStage.Adjusting)
            {
                _output.WriteLine(
                    $"  [{value.Current}/{value.Total}] {RelativePath.Of(value.FilePath, _rootFolder)}"
                    );

                return;
            }

            if (!_cleanupAnnounced)
            {
                _cleanupAnnounced = true;

                _output.WriteLine(
                    $"Removing the using clauses of the emptied namespaces ({value.Total} file(s))"
                    );
            }

            if (_verbose)
            {
                _output.WriteLine(
                    $"  [{value.Current}/{value.Total}] {RelativePath.Of(value.FilePath, _rootFolder)}"
                    );
            }
        }
    }
}
