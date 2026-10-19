using System;
using System.Collections.Generic;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Finds the parts of a project file (<c>.csproj</c>) and of a <c>.settings</c> file that name
    /// a type or a namespace.
    ///
    /// A project file: <c>&lt;StartupObject&gt;A.B.Program&lt;/StartupObject&gt;</c> (a build error
    /// once the class has moved, CS1555) and the <c>CustomToolNamespace</c> of a project item,
    /// which says the namespace a file is generated into (an element or an attribute).
    ///
    /// A settings file: the <c>GeneratedClassNamespace</c> of the generated settings class and
    /// the <c>Type</c> of a setting.
    /// </summary>
    internal static class ProjectTextScanner
    {
        public static List<TextCandidate> ScanProject(string text)
        {
            var result = new List<TextCandidate>();

            foreach (var piece in XmlTokenizer.Tokenize(text))
            {
                if (piece.Kind == XmlPieceKind.ElementText && Is(piece.ElementName, "StartupObject"))
                {
                    result.Add(new TextCandidate(TextReferencePlace.ProjectStartupObject, piece.Start, piece.Length));
                }
                else if (IsCustomToolNamespace(piece))
                {
                    result.Add(
                        new TextCandidate(
                            TextReferencePlace.CustomToolNamespace,
                            piece.Start,
                            piece.Length,
                            expectsNamespace: true,
                            alsoSearchTypes: false
                            )
                        );
                }
            }

            return result;
        }

        public static List<TextCandidate> ScanSettings(string text)
        {
            var result = new List<TextCandidate>();

            foreach (var piece in XmlTokenizer.Tokenize(text))
            {
                if (piece.Kind != XmlPieceKind.AttributeValue)
                {
                    continue;
                }

                if (Is(piece.AttributeName, "GeneratedClassNamespace"))
                {
                    result.Add(
                        new TextCandidate(
                            TextReferencePlace.SettingsNamespace,
                            piece.Start,
                            piece.Length,
                            expectsNamespace: true,
                            alsoSearchTypes: false
                            )
                        );
                }
                else if (Is(piece.ElementName, "Setting") && Is(piece.AttributeName, "Type"))
                {
                    result.Add(new TextCandidate(TextReferencePlace.XmlTypeAttribute, piece.Start, piece.Length));
                }
            }

            return result;
        }

        private static bool IsCustomToolNamespace(XmlPiece piece)
        {
            return (piece.Kind == XmlPieceKind.ElementText && Is(piece.ElementName, "CustomToolNamespace"))
                || (piece.Kind == XmlPieceKind.AttributeValue && Is(piece.AttributeName, "CustomToolNamespace"));
        }

        private static bool Is(string? name, string expected)
        {
            return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
