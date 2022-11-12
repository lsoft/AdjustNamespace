using Document = Microsoft.CodeAnalysis.Document;

namespace AdjustNamespace
{
    public static class Predicate
    {
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
