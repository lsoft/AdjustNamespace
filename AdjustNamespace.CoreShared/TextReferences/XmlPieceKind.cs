namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// What a <see cref="XmlTokenizer"/> finds in an xml text.
    /// </summary>
    internal enum XmlPieceKind
    {
        /// <summary>
        /// The value of an attribute, without the quotes.
        /// </summary>
        AttributeValue,

        /// <summary>
        /// A piece of the text of an element.
        /// </summary>
        ElementText,

        /// <summary>
        /// The content of a comment.
        /// </summary>
        Comment
    }
}
