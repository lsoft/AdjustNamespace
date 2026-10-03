namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// A fragment of the xaml body which may reference a class and hence
    /// may require a modification when that class is moved to another namespace.
    /// </summary>
    public interface IXamlPerformable : IXamlPositioned
    {
        /// <summary>
        /// Rewrite this fragment if it references the given class.
        /// </summary>
        /// <param name="structure">Structure of the document (to resolve and to create the xmlns aliases).</param>
        /// <param name="move">What moves where.</param>
        /// <param name="xaml">(in/out) Body of the xaml document.</param>
        /// <param name="newXmlns">
        /// (out) A new clr-namespace declaration which has to be added to the document,
        /// or <c>null</c> if no new declaration is required.
        /// </param>
        /// <returns><c>true</c> if the fragment has been rewritten.</returns>
        bool Perform(
            XamlStructure structure,
            in XamlMove move,
            ref string xaml,
            out XamlXmlns? newXmlns
            );
    }
}
