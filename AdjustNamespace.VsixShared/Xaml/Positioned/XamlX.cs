namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// The declaration of the xaml language namespace
    /// (<c>xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"</c>).
    /// Its alias is required to build the <c>x:Class</c> / <c>x:Type</c> / <c>x:Static</c> clauses.
    /// </summary>
    public class XamlX : IXamlPositioned
    {
        /// <inheritdoc/>
        public int Index
        {
            get;
        }

        /// <inheritdoc/>
        public int Length
        {
            get;
        }

        /// <summary>
        /// The alias itself (usually `x`).
        /// </summary>
        public string Alias
        {
            get;
        }

        public XamlX(int index, int length, string alias)
        {
            Index = index;
            Length = length;
            Alias = alias;
        }
    }
}
