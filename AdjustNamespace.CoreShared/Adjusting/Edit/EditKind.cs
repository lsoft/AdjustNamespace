namespace AdjustNamespace.Adjusting.Edit
{
    /// <summary>
    /// What an edit means to the user, as the run report counts and names it.
    ///
    /// This is not the same as the class of an edit: a <see cref="ReplaceTextEdit"/> is a
    /// qualified name, a <c>global::</c> prefix or the string of an
    /// <c>XmlnsDefinition</c> attribute, and the report tells them apart.
    /// </summary>
    public enum EditKind
    {
        /// <summary>
        /// A <c>using</c> clause has been added.
        /// </summary>
        UsingAdded,

        /// <summary>
        /// A fully qualified name (or a name qualified with its namespace) has been rewritten.
        /// </summary>
        NameQualified,

        /// <summary>
        /// A name has got the <c>global::</c> prefix because the new namespace hides its head.
        /// </summary>
        GlobalAdded,

        /// <summary>
        /// A namespace declaration has been renamed.
        /// </summary>
        NamespaceMoved,

        /// <summary>
        /// The namespace of an <c>[assembly: XmlnsDefinition]</c> attribute has been changed.
        /// </summary>
        XmlnsDefinition,

        /// <summary>
        /// The <c>x:Class</c> of a xaml file has been changed.
        /// </summary>
        XamlClassMoved,

        /// <summary>
        /// The references to a moved class (and the xmlns of them) have been changed in a xaml file.
        /// </summary>
        XamlReferencesUpdated,

        /// <summary>
        /// A <c>nameof</c> of an emptied namespace has been replaced with the string it gives.
        /// </summary>
        NameofReplaced,

        /// <summary>
        /// A <c>using</c> clause of an emptied namespace has been removed by the cleanup.
        /// </summary>
        UsingRemoved,

        /// <summary>
        /// A reference to a moved type which is written as a text (a string, a comment, a
        /// config file) has been changed.
        /// </summary>
        TextReference
    }
}
