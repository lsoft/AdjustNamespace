using System;

namespace AdjustNamespace.Adjusting.Session
{
    /// <summary>
    /// What an <see cref="AdjustSession"/> is busy with right now.
    ///
    /// The session reports the data and not a ready text, so a progress report may be asserted
    /// in a test (the file, the stage and the position in it) and not only be shown to the user.
    /// </summary>
    public readonly struct AdjustProgress
    {
        /// <summary>
        /// The stage the session is performing.
        /// </summary>
        public readonly AdjustStage Stage;

        /// <summary>
        /// The number of the file which is being processed, starting with 1.
        /// </summary>
        public readonly int Current;

        /// <summary>
        /// How many files this stage processes.
        /// </summary>
        public readonly int Total;

        /// <summary>
        /// Full path to the file which is being processed.
        /// </summary>
        public readonly string FilePath;

        /// <summary>
        /// The progress line shown to the user.
        /// </summary>
        public string Message =>
            Stage == AdjustStage.Cleanup
                ? $"{Current}/{Total} Performing cleanup {FilePath}"
                : $"{Current}/{Total}: {FilePath}"
                ;

        public AdjustProgress(
            AdjustStage stage,
            int current,
            int total,
            string filePath
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            Stage = stage;
            Current = current;
            Total = total;
            FilePath = filePath;
        }
    }
}
