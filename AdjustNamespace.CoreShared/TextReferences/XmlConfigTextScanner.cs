using System;
using System.Collections.Generic;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Finds the parts of an xml config (<c>app.config</c>, <c>web.config</c>, <c>nlog.config</c>,
    /// <c>log4net.config</c>, the Unity and WCF configs, any other xml the settings of the solution
    /// name) that may name a type.
    ///
    /// The attributes that are known to hold a type name are worth more than the other values:
    /// <c>type</c>, <c>typeName</c>, <c>mapTo</c>, <c>contract</c>, <c>factory</c> and the
    /// <c>name</c> of a <c>service</c> (its implementation). The text of an element called so
    /// counts as well. Every other attribute value and every other text is a candidate too, as
    /// a value that is the name as a whole (<c>&lt;add key="Handler" value="A.B.C" /&gt;</c>).
    /// </summary>
    internal static class XmlConfigTextScanner
    {
        private static readonly HashSet<string> TypeNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "type",
            "typeName",
            "mapTo",
            "contract",
            "factory"
        };

        public static List<TextCandidate> Scan(string text, bool includeComments)
        {
            var result = new List<TextCandidate>();

            foreach (var piece in XmlTokenizer.Tokenize(text))
            {
                switch (piece.Kind)
                {
                    case XmlPieceKind.Comment:
                        if (includeComments)
                        {
                            result.Add(new TextCandidate(TextReferencePlace.Comment, piece.Start, piece.Length));
                        }

                        break;

                    case XmlPieceKind.AttributeValue:
                        result.Add(
                            new TextCandidate(
                                IsTypeAttribute(piece) ? TextReferencePlace.XmlTypeAttribute : TextReferencePlace.OtherValue,
                                piece.Start,
                                piece.Length
                                )
                            );
                        break;

                    default:
                        result.Add(
                            new TextCandidate(
                                TypeNames.Contains(LocalNameOf(piece.ElementName)) ? TextReferencePlace.XmlTypeAttribute : TextReferencePlace.OtherValue,
                                piece.Start,
                                piece.Length
                                )
                            );
                        break;
                }
            }

            return result;
        }

        private static bool IsTypeAttribute(XmlPiece piece)
        {
            if (piece.AttributeName is null)
            {
                return false;
            }

            var attribute = LocalNameOf(piece.AttributeName);

            if (TypeNames.Contains(attribute))
            {
                return true;
            }

            //<service name="A.B.C"> is the implementation of a WCF service
            return string.Equals(LocalNameOf(piece.ElementName), "service", StringComparison.OrdinalIgnoreCase)
                && string.Equals(attribute, "name", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The name without its prefix: <c>xsi:type</c> says a type as well as <c>type</c> does.
        /// </summary>
        private static string LocalNameOf(string name)
        {
            return name.Substring(name.LastIndexOf(':') + 1);
        }
    }
}
