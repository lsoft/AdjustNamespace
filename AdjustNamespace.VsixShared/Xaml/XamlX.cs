namespace AdjustNamespace.Xaml
{
    public class XamlX : IXamlPositioned
    {
        public int Index
        {
            get;
        }
        public int Length
        {
            get;
        }
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
