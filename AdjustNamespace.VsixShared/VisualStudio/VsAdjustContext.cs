using AdjustNamespace.Namespace;
using AdjustNamespace.Xaml.BodyProvider;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.LanguageServices;
using System;
using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// The <see cref="AdjustContext"/> of a run inside Visual Studio.
    ///
    /// This is the single place which resolves the services of the IDE and puts the Visual
    /// Studio implementations of the boundary interfaces together; the context itself knows
    /// nothing about the IDE, which is what allows the console utility and the tests to build
    /// their own one.
    /// </summary>
    public static class VsAdjustContext
    {
        /// <summary>
        /// Resolve all the required Visual Studio services and read the solution settings.
        /// </summary>
        /// <exception cref="InvalidOperationException">One of the services is not available.</exception>
        public static async Task<AdjustContext> CreateAsync(
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

            var componentModel = (Microsoft.VisualStudio.ComponentModelHost.IComponentModel)
                (await serviceProvider.GetServiceAsync(typeof(Microsoft.VisualStudio.ComponentModelHost.SComponentModel)))!;
            if (componentModel == null)
            {
                throw new InvalidOperationException("Can't create a component model");
            }

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null)
            {
                throw new InvalidOperationException("Can't create a workspace");
            }

            var settings = AdjustContext.ReadSettingsOf(workspace);

            return new AdjustContext(
                workspace,
                new VsSolutionExplorer(),
                new TargetNamespaceResolver(
                    settings,
                    new DteProjectDefaultNamespaceProvider(dte)
                    ),
                new VsXamlBodyProviderFactory()
                );
        }
    }
}
