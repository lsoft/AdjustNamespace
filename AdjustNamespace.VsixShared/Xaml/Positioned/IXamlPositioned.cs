namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// A fragment of the xaml body with a known position in it.
    /// </summary>
    public interface IXamlPositioned
    {
        /// <summary>
        /// Index of the first character of the fragment in the xaml body.
        /// </summary>
        int Index
        {
            get;
        }

        /// <summary>
        /// Length of the fragment.
        /// </summary>
        int Length
        {
            get;
        }
    }
}
