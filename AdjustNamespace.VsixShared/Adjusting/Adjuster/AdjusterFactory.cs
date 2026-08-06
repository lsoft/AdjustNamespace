using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Namespace;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Adjuster
{
    /// <summary>
    /// Factory which creates a suitable <see cref="IAdjuster"/> for the given file:
    /// <see cref="XamlAdjuster"/> for a xaml file and <see cref="CsAdjuster"/> for a C# file.
    ///
    /// It decides nothing by itself: <see cref="AdjustPlanner"/> says whether the file is
    /// a subject to change and what has to happen with it, and this factory builds the
    /// adjuster which performs exactly that.
    /// </summary>
    public class AdjusterFactory
    {
        private readonly AdjustContext _context;
        private readonly AdjustPlanner _planner;
        private readonly bool _openFilesToEnableUndo;
        private readonly NamespaceCenter _namespaceCenter;

        /// <summary>
        /// All the xaml files of the solution. A C# type moved to another namespace
        /// may be referenced from any of them, so all of them are the subject to check.
        /// </summary>
        private readonly List<string> _xamlFilePaths;

        /// <summary>
        /// Create a factory. Performs an expensive scan of the solution for xaml files,
        /// so it is intended to be created once per adjusting session.
        /// </summary>
        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        /// <param name="openFilesToEnableUndo">Open the changed files in the editor (this allows the user to undo the changes).</param>
        /// <param name="namespaceCenter">Namespace state container shared across the whole adjusting session.</param>
        public static async Task<AdjusterFactory> CreateAsync(
            AdjustContext context,
            NamespaceReplaceRegex replaceRegex,
            bool openFilesToEnableUndo,
            NamespaceCenter namespaceCenter
            )
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            //get all xaml files in current solution
            var filePaths = await context.Solution.GetAllFilePathsAsync();
            var xamlFilePaths = new List<string>();
            foreach (var filePath in filePaths)
            {
                if (filePath.EndsWith(".xaml"))
                {
                    xamlFilePaths.Add(filePath);
                }
            }

            return new AdjusterFactory(
                context,
                new AdjustPlanner(context, replaceRegex),
                openFilesToEnableUndo,
                namespaceCenter,
                xamlFilePaths
                );

        }

        private AdjusterFactory(
            AdjustContext context,
            AdjustPlanner planner,
            bool openFilesToEnableUndo,
            NamespaceCenter namespaceCenter,
            List<string> xamlFilePaths
            )
        {
            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            if (xamlFilePaths is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePaths));
            }

            _context = context;
            _planner = planner;
            _openFilesToEnableUndo = openFilesToEnableUndo;
            _namespaceCenter = namespaceCenter;
            _xamlFilePaths = xamlFilePaths;
        }

        /// <summary>
        /// Create an adjuster for the given file.
        /// </summary>
        /// <param name="subjectFilePath">Full path to the file to adjust.</param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        /// <returns>
        /// An adjuster, or <c>null</c> if the file is no subject to change,
        /// see <see cref="AdjustPlanner.TryPlanAsync(string, CancellationToken)"/>.
        /// </returns>
        public async Task<IAdjuster?> CreateAsync(
            string subjectFilePath,
            CancellationToken cancellationToken = default
            )
        {
            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            var plan = await _planner.TryPlanAsync(subjectFilePath, cancellationToken);
            if (!plan.HasValue)
            {
                return null;
            }

            return Create(plan.Value);
        }

        /// <summary>
        /// The adjuster which performs the given plan.
        /// </summary>
        public IAdjuster Create(
            AdjustPlanItem plan
            )
        {
            if (plan.IsXaml)
            {
                return new XamlAdjuster(
                    _openFilesToEnableUndo,
                    plan
                    );
            }

            return new CsAdjuster(
                _context.Workspace,
                _context.DocumentOpener,
                _openFilesToEnableUndo,
                _namespaceCenter,
                plan,
                _xamlFilePaths
                );
        }
    }
}
