using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

namespace AdjustNamespace.Helper
{
    /// <summary>
    /// Helpers for the resources embedded into the extension assembly.
    /// </summary>
    public static class EmbeddedResourceHelper
    {
        /// <summary>
        /// Load an embedded xaml resource dictionary and merge it into the application resources,
        /// so its styles become available for the windows of the extension.
        /// </summary>
        /// <param name="resourceName">Name of the embedded resource.</param>
        /// <exception cref="InvalidOperationException">There is no such resource in the assembly.</exception>
        public static void LoadXamlEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Ресурс {resourceName} не найден.");
                }

                var resourceDict = (ResourceDictionary)XamlReader.Load(stream);

                foreach (DictionaryEntry entry in resourceDict)
                {
                    Application.Current.Resources[entry.Key] = entry.Value;
                }
            }
        }
    }
}
