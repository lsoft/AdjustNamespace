using AdjustNamespace.Xaml;
using AdjustNamespace.Xaml.BodyProvider;

namespace AdjustNamespace.Tests.Infrastructure
{
    /// <summary>
    /// A body provider which keeps the xaml body in a string.
    /// It allows to test the whole xaml subsystem without a file and without Visual Studio.
    /// </summary>
    public sealed class MemoryXamlBodyProvider : IXamlBodyProvider
    {
        /// <summary>
        /// The current body. It is updated by <see cref="XamlDocument.SaveIfChangesExistsAgainst"/>.
        /// </summary>
        public string Body
        {
            get;
            private set;
        }

        /// <inheritdoc/>
        public string XamlFilePath
        {
            get;
        }

        public MemoryXamlBodyProvider(
            string body,
            string xamlFilePath = @"c:\solution\project\Test.xaml"
            )
        {
            Body = body;
            XamlFilePath = xamlFilePath;
        }

        /// <inheritdoc/>
        public string ReadText() => Body;

        /// <inheritdoc/>
        public void UpdateText(string text) => Body = text;

        /// <summary>
        /// Build a xaml document over the given body.
        /// </summary>
        public static (XamlDocument Document, MemoryXamlBodyProvider Provider) CreateDocument(string body)
        {
            var provider = new MemoryXamlBodyProvider(body);
            var document = new XamlDocument(provider);

            return (document, provider);
        }

        /// <summary>
        /// Apply <see cref="XamlDocument.MoveObject"/> to the given body and return the result
        /// as it would be written into the file.
        /// </summary>
        public static string MoveObject(
            string body,
            string sourceNamespace,
            string objectClassName,
            string targetNamespace
            )
        {
            var (document, provider) = CreateDocument(body);

            var modified = document.MoveObject(sourceNamespace, objectClassName, targetNamespace);
            modified.SaveIfChangesExistsAgainst(document);

            return provider.Body;
        }
    }
}
