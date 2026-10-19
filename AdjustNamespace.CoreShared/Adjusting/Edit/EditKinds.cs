using System;

namespace AdjustNamespace.Adjusting.Edit
{
    /// <summary>
    /// The report side of the edits: which <see cref="EditKind"/> an edit is, and how it is named.
    /// </summary>
    public static class EditKinds
    {
        /// <summary>
        /// The kind of the given edit.
        /// </summary>
        public static EditKind KindOf(FileEdit edit)
        {
            if (edit is null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            switch (edit)
            {
                case AddUsingEdit:
                    return EditKind.UsingAdded;

                case MoveNamespaceEdit:
                    return EditKind.NamespaceMoved;

                case ReplaceFileTextEdit:
                    return EditKind.TextReference;

                case ReplaceTextEdit replace:
                    if (replace.Kind == EditKind.NameQualified
                        && replace.Text.StartsWith("global::", StringComparison.Ordinal))
                    {
                        return EditKind.GlobalAdded;
                    }

                    return replace.Kind;

                default:
                    return EditKind.NameQualified;
            }
        }

        /// <summary>
        /// The words the report uses for the kind, in the singular.
        /// </summary>
        public static string Describe(EditKind kind)
        {
            switch (kind)
            {
                case EditKind.UsingAdded:
                    return "using added";

                case EditKind.NameQualified:
                    return "name qualified";

                case EditKind.GlobalAdded:
                    return "global:: added";

                case EditKind.NamespaceMoved:
                    return "namespace declaration moved";

                case EditKind.XmlnsDefinition:
                    return "XmlnsDefinition changed";

                case EditKind.XamlClassMoved:
                    return "x:Class changed";

                case EditKind.XamlReferencesUpdated:
                    return "xaml references and xmlns changed";

                case EditKind.NameofReplaced:
                    return "nameof replaced";

                case EditKind.UsingRemoved:
                    return "using removed by the cleanup";

                case EditKind.TextReference:
                    return "text reference changed";

                default:
                    return kind.ToString();
            }
        }
    }
}
