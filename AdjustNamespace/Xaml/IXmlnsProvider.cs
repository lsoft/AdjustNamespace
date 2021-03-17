namespace AdjustNamespace.Xaml
{
    public interface IXmlnsProvider
    {
        XamlXmlns GetByAlias(string alias);
        XamlXmlns? TryGetByNamespace(string @namespace);
        XamlX GetXPrefix();
    }
}
