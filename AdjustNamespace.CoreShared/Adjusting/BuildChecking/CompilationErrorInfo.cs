using System;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.BuildChecking
{
    /// <summary>
    /// A compilation error of a project, as the build check and the report know it.
    /// </summary>
    [DebuggerDisplay("{ProjectName}: {Id} {Message}")]
    public sealed class CompilationErrorInfo
    {
        /// <summary>
        /// The project that reports the error.
        /// </summary>
        public string ProjectName
        {
            get;
        }

        /// <summary>
        /// The id of the diagnostic (<c>CS0246</c>).
        /// </summary>
        public string Id
        {
            get;
        }

        /// <summary>
        /// The message of the diagnostic.
        /// </summary>
        public string Message
        {
            get;
        }

        /// <summary>
        /// Full path to the file the error is in, or <c>null</c> if it is in none.
        /// </summary>
        public string? FilePath
        {
            get;
        }

        /// <summary>
        /// The line of the error, starting with 1; 0 if it has none.
        /// </summary>
        public int Line
        {
            get;
        }

        /// <summary>
        /// The column of the error, starting with 0.
        /// </summary>
        public int Column
        {
            get;
        }

        /// <summary>
        /// What makes two errors the same error before and after the run: the position is not
        /// among it, because the run moves the lines.
        /// </summary>
        public string Key => $"{ProjectName}|{Id}|{Message}";

        public CompilationErrorInfo(
            string projectName,
            string id,
            string message,
            string? filePath,
            int line,
            int column
            )
        {
            if (projectName is null)
            {
                throw new ArgumentNullException(nameof(projectName));
            }

            if (id is null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            ProjectName = projectName;
            Id = id;
            Message = message;
            FilePath = filePath;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"{Id}: {Message}";
        }
    }
}
