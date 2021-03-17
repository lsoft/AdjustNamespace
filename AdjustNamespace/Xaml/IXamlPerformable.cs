namespace AdjustNamespace.Xaml
{
    public interface IXamlPerformable : IXamlPositioned
    {
        bool Perform(
            string sourceNamespace,
            string objectClassName,
            string targetNamespace,
            ref string xaml,
            out XamlXmlns? newXmlns
            );
    }
}
