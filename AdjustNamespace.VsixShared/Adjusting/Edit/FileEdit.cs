using System;

namespace AdjustNamespace.Adjusting.Edit
{
    /// <summary>
    /// A single change of a single file: what has to happen with that file and nothing about
    /// how it happens.
    ///
    /// An edit is a plain description and knows neither the Roslyn workspace nor the documents,
    /// so the whole set of the edits of an adjusting may be built and inspected without
    /// touching the solution. The mutation lives in <c>Apply.EditApplier</c>.
    /// </summary>
    public abstract class FileEdit
    {
        /// <summary>
        /// Full path to the file this edit changes.
        /// </summary>
        public string FilePath
        {
            get;
        }

        protected FileEdit(
            string filePath
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
        }
    }
}
