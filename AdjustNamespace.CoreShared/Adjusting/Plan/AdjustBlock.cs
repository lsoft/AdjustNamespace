using System;

namespace AdjustNamespace.Adjusting.Plan
{
    /// <summary>
    /// A file the adjusting has to leave alone, with the reason shown to the user
    /// (the wizard and the console utility).
    /// </summary>
    public readonly struct AdjustBlock
    {
        /// <summary>
        /// Full path to the file.
        /// </summary>
        public string FilePath
        {
            get;
        }

        /// <summary>
        /// Why the file cannot be adjusted.
        /// </summary>
        public AdjustBlockKind Kind
        {
            get;
        }

        /// <summary>
        /// One-line explanation shown in the UI and on the console.
        /// </summary>
        public string Message
        {
            get;
        }

        public AdjustBlock(
            string filePath,
            AdjustBlockKind kind,
            string message
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            FilePath = filePath;
            Kind = kind;
            Message = message;
        }

        public static AdjustBlock Create(
            string filePath,
            AdjustBlockKind kind
            )
        {
            return new AdjustBlock(filePath, kind, MessageOf(kind));
        }

        public static AdjustBlock TypeNameConflict(
            string filePath,
            string namespaceName,
            string typeName
            )
        {
            return new AdjustBlock(
                filePath,
                AdjustBlockKind.TypeNameConflict,
                $"'{namespaceName}' already contains a type '{typeName}'"
                );
        }

        public static AdjustBlock NamespaceImportedByProjectFile(
            string filePath,
            string namespaceName,
            string projectName
            )
        {
            return new AdjustBlock(
                filePath,
                AdjustBlockKind.NamespaceImportedByProjectFile,
                $"'{namespaceName}' would become empty, but the project '{projectName}' imports it "
                + $"(<Using Include=\"{namespaceName}\" />); remove that item from the project file first"
                );
        }

        public static AdjustBlock PartialTypeSplit(
            string filePath,
            string message
            )
        {
            return new AdjustBlock(
                filePath,
                AdjustBlockKind.PartialTypeSplit,
                message
                );
        }

        private static string MessageOf(
            AdjustBlockKind kind
            )
        {
            switch (kind)
            {
                case AdjustBlockKind.NoProject:
                    return "The file belongs to no project of the solution.";

                case AdjustBlockKind.TargetNamespaceUnknown:
                    return "The target namespace cannot be determined (the file is outside of the project folder).";

                case AdjustBlockKind.NotAProcessableDocument:
                    return "The file is not a processable C# document.";

                case AdjustBlockKind.CompiledBySeveralProjects:
                    return "Several projects compile the file; there is no single target namespace.";

                case AdjustBlockKind.NamespaceStateContradictory:
                    return "The target frameworks disagree whether the old namespace stays alive.";

                case AdjustBlockKind.XamlCodeBehindMultiProject:
                    return "The code behind of the xaml is compiled by several projects.";

                case AdjustBlockKind.TypeNameConflict:
                    return "The target namespace already contains a type of the same name.";

                case AdjustBlockKind.NamespaceImportedByProjectFile:
                    return "The old namespace would become empty, but a project file imports it.";

                case AdjustBlockKind.PartialTypeSplit:
                    return "Another part of a partial type of the file is not adjusted together with it.";

                default:
                    return "The file cannot be adjusted.";
            }
        }
    }
}
