using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml
{
    public interface IXmlnsProvider
    {
        XamlXmlns GetByAlias(string alias);
        XamlXmlns? TryGetByNamespace(string @namespace);
        XamlX GetXPrefix();
    }
}
