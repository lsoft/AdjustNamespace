using AdjustNamespace.Settings;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    public sealed class NamespaceReplaceRegex
    {
        public string ReplaceRegex
        {
            get;
        }

        public string ReplacedString
        {
            get;
        }

        public NamespaceReplaceRegex(string replaceRegex, string replacedString)
        {
            ReplaceRegex = replaceRegex;
            ReplacedString = replacedString;
        }

        public string Modify(string myNamespace)
        {
            if(string.IsNullOrEmpty(ReplaceRegex))
            {
                return myNamespace;
            }
            if (string.IsNullOrEmpty(ReplacedString))
            {
                return myNamespace;
            }

            var result = Regex.Replace(myNamespace, ReplaceRegex, ReplacedString);
            return result;
        }
    }

    public readonly struct VsServices
    {
        public readonly IAsyncServiceProvider ServiceProvider;
        public readonly DTE2 Dte;
        public readonly IComponentModel ComponentModel;
        public readonly VisualStudioWorkspace Workspace;
        public readonly SettingsReader SettingsReader;
        public readonly AdjustNamespaceSettings2 Settings;

        private VsServices(
            Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider,
            DTE2 dte,
            IComponentModel componentModel,
            VisualStudioWorkspace workspace
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (dte is null)
            {
                throw new ArgumentNullException(nameof(dte));
            }

            if (componentModel is null)
            {
                throw new ArgumentNullException(nameof(componentModel));
            }

            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            ServiceProvider = serviceProvider;
            Dte = dte;
            ComponentModel = componentModel;
            Workspace = workspace;

            var solutionFolder = new FileInfo(workspace.CurrentSolution.FilePath).Directory.FullName;

            SettingsReader = new SettingsReader(solutionFolder);
            Settings = new AdjustNamespaceSettings2(
                solutionFolder,
                SettingsReader.ReadSettings() ?? new AdjustNamespaceSettings()
                );
        }

        public async Task OpenFileAsync(
            string documentFullPath
            )
        {
            _ = await VS.Documents.OpenAsync(documentFullPath);
        }

        public static async Task<VsServices> CreateAsync(
            Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var dte = await serviceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                throw new InvalidOperationException("Can't create a dte");
            }

            var componentModel = (IComponentModel)(await serviceProvider.GetServiceAsync(typeof(SComponentModel)))!;
            if (componentModel == null)
            {
                throw new InvalidOperationException("Can't create a component model");
            }

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null)
            {
                throw new InvalidOperationException("Can't create a workspace");
            }

            return new VsServices(serviceProvider, dte, componentModel, workspace);
        }
    }
}
