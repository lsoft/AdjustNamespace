using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdjustNamespace.Xaml
{
    /// <summary>
    /// Which files are treated as xaml documents: classic <c>.xaml</c> (WPF, MAUI, …)
    /// and Avalonia's <c>.axaml</c>.
    /// </summary>
    public static class XamlPathHelper
    {
        /// <summary>
        /// <c>true</c> when the path is a xaml (or Avalonia axaml) file by its extension.
        /// </summary>
        public static bool IsXamlFile(
            string? filePath
            )
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            return filePath!.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                || filePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Every <c>.xaml</c> and <c>.axaml</c> file under the folder, recursively.
        /// </summary>
        public static IEnumerable<string> EnumerateXamlFiles(
            string folder
            )
        {
            if (!Directory.Exists(folder))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(IsXamlFile);
        }
    }
}
