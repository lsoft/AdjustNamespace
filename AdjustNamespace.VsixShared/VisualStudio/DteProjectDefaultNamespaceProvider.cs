using AdjustNamespace.Namespace;
using EnvDTE80;
using System;
using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// The default namespace as the project properties of Visual Studio report it.
    /// It is taken from the properties for a C# and for a database (sqlproj) project;
    /// for any other project kind the fallback of
    /// <see cref="TargetNamespaceCalculator.DefaultNamespaceFallback"/> is used.
    ///
    /// DTE is available from the main thread only, hence the switch.
    /// </summary>
    public sealed class DteProjectDefaultNamespaceProvider : IProjectDefaultNamespaceProvider
    {
        private readonly DTE2 _dte;

        /// <param name="dte">DTE, the automation object model of Visual Studio.</param>
        public DteProjectDefaultNamespaceProvider(
            DTE2 dte
            )
        {
            if (dte is null)
            {
                throw new ArgumentNullException(nameof(dte));
            }

            _dte = dte;
        }

        /// <inheritdoc/>
        public async Task<string> GetAsync(
            ProjectRef project,
            string documentFilePath
            )
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var pi = _dte.Solution.FindProjectItem(documentFilePath);
            if (pi != null)
            {
                var cp = pi.ContainingProject;
                if (cp != null)
                {
                    if (cp.Kind.In(SolutionHelper.CSharpProjectKind, SolutionHelper.DatabaseProjectKind))
                    {
                        var prop = cp.Properties;
                        if (prop != null)
                        {
                            var dn = prop.Item("DefaultNamespace");
                            if (dn != null)
                            {
                                return dn.Value.ToString();
                            }
                        }
                    }
                }
            }

            return TargetNamespaceCalculator.DefaultNamespaceFallback(project.Name);
        }
    }
}
