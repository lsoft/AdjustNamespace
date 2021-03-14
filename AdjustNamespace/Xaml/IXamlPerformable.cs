using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
