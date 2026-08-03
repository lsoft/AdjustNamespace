using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Adjuster
{
    /// <summary>
    /// An object which knows how to bring the namespaces of a single file
    /// in accordance with its location, and how to fix the references to
    /// the moved types across the solution.
    /// Instances are produced by <see cref="AdjusterFactory"/>.
    /// </summary>
    public interface IAdjuster
    {
        /// <summary>
        /// Perform the adjusting of the file this adjuster has been created for.
        /// </summary>
        /// <returns><c>true</c> if the file has been processed, <c>false</c> if it has been skipped.</returns>
        Task<bool> AdjustAsync();
    }
}
