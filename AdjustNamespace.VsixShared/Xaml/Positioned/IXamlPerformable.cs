namespace AdjustNamespace.Xaml.Positioned
{
    public interface IXamlPerformable : IXamlPositioned
    {
        bool Perform(
            XamlStructure structure,
            string sourceNamespace,
            string objectClassName,
            string targetNamespace,
            ref string xaml,
            out XamlXmlns? newXmlns
            );
    }
}
