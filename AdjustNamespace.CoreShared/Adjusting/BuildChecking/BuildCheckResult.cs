using System;
using System.Collections.Generic;

namespace AdjustNamespace.Adjusting.BuildChecking
{
    /// <summary>
    /// What the build check has found: the compilation errors of the projects the run has
    /// touched (and of the projects that depend on them) before and after the run, and the
    /// errors that are new.
    /// </summary>
    public sealed class BuildCheckResult
    {
        /// <summary>
        /// The projects that were checked.
        /// </summary>
        public IReadOnlyList<string> ProjectNames
        {
            get;
        }

        /// <summary>
        /// How many errors those projects had before the run.
        /// </summary>
        public int ErrorsBefore
        {
            get;
        }

        /// <summary>
        /// How many errors they have after the run.
        /// </summary>
        public int ErrorsAfter
        {
            get;
        }

        /// <summary>
        /// The errors after the run that were not there before it.
        /// </summary>
        public IReadOnlyList<CompilationErrorInfo> NewErrors
        {
            get;
        }

        public BuildCheckResult(
            IReadOnlyList<string> projectNames,
            int errorsBefore,
            int errorsAfter,
            IReadOnlyList<CompilationErrorInfo> newErrors
            )
        {
            if (projectNames is null)
            {
                throw new ArgumentNullException(nameof(projectNames));
            }

            if (newErrors is null)
            {
                throw new ArgumentNullException(nameof(newErrors));
            }

            ProjectNames = projectNames;
            ErrorsBefore = errorsBefore;
            ErrorsAfter = errorsAfter;
            NewErrors = newErrors;
        }
    }
}
