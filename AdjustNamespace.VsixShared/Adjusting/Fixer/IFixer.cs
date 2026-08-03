using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
{
    /// <summary>
    /// A fixer of a single kind of regression in a single file.
    /// A fixer is filled with its subjects (`AddSubject` method of the specific implementation)
    /// during the analysis and is applied later, when the whole picture is known.
    /// </summary>
    public interface IFixer
    {
        /// <summary>
        /// Full path to the file this fixer works with.
        /// </summary>
        public string FilePath
        {
            get;
        }

        /// <summary>
        /// Apply all the accumulated subjects to the file.
        /// </summary>
        Task FixAsync();
    }
}
