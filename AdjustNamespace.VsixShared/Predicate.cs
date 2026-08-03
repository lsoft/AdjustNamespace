using Document = Microsoft.CodeAnalysis.Document;

namespace AdjustNamespace
{
    /// <summary>
    /// Filters which determine what the extension is able to process.
    /// </summary>
    public static class Predicate
    {
        /// <summary>
        /// The project can be processed: it is a C# project with the Roslyn compilation support.
        /// </summary>
        public static bool IsProjectInScope(this Microsoft.CodeAnalysis.Project? project)
        {
            if (project == null)
            {
                return false;
            }
            if (!project.SupportsCompilation)
            {
                return false;
            }
            if (project.Language != "C#")
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// The document can be processed: it exists on the disk and both
        /// its syntax tree and its semantic model are available.
        /// </summary>
        public static bool IsDocumentInScope(this Document? document)
        {
            if (document is null)
            {
                return false;
            }
            if (document.FilePath is null)
            {
                return false;
            }
            if (!document.SupportsSyntaxTree)
            {
                return false;
            }
            if (!document.SupportsSemanticModel)
            {
                return false;
            }

            return true;
        }
    }
}
