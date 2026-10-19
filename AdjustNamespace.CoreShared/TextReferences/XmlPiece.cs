namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// A part of an xml text with the position it is at.
    /// </summary>
    internal readonly struct XmlPiece
    {
        public XmlPieceKind Kind
        {
            get;
        }

        /// <summary>
        /// The element the attribute belongs to, or the element the text is in.
        /// </summary>
        public string ElementName
        {
            get;
        }

        /// <summary>
        /// The name of the attribute (an attribute value only).
        /// </summary>
        public string? AttributeName
        {
            get;
        }

        /// <summary>
        /// Where the content starts in the text, as it is written (an entity is not decoded).
        /// </summary>
        public int Start
        {
            get;
        }

        public int Length
        {
            get;
        }

        public XmlPiece(
            XmlPieceKind kind,
            string elementName,
            string? attributeName,
            int start,
            int length
            )
        {
            Kind = kind;
            ElementName = elementName;
            AttributeName = attributeName;
            Start = start;
            Length = length;
        }
    }
}
