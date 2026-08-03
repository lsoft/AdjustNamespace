using AdjustNamespace.Namespace;
using AdjustNamespace.Settings;
using Community.VisualStudio.Toolkit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Helper
{
    /// <summary>
    /// Helpers around the namespaces: determining the target namespace of a file
    /// and searching for the namespace declarations in a syntax tree.
    /// </summary>
    public static class NamespaceHelper
    {
        /// <summary>
        /// Check if the namespace is a special one, i.e. the one we must never touch.
        /// </summary>
        public static bool IsSpecialNamespace(
            string namespaceName
            )
        {
            //workaround: we will not remove a System* and Microsoft* namespaces
            //such namespaces may exists in the codebase because of
            //delivering special attributes in the obsolete codebase
            //(like nullable attributes, CallerMemberNameAttribute etc)
            //we do not want to remove System, System.*, Microsoft.* from
            //the codebase in this case
            if (namespaceName.StartsWith("System"))
            {
                return true;
            }
            if (namespaceName.StartsWith("Microsoft"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Find all the declarations of the given namespace in the document.
        /// </summary>
        /// <param name="syntaxRoot">Syntax root of the document.</param>
        /// <param name="namespaceName">Full name of the namespace to search for.</param>
        /// <param name="result">(out) Found declarations.</param>
        /// <returns><c>true</c> if at least one declaration has been found.</returns>
        public static bool TryFindNamespaceNodesFor(
            this SyntaxNode syntaxRoot,
            string namespaceName,
            out List<BaseNamespaceDeclarationSyntax> result
            )
        {
            if (syntaxRoot is null)
            {
                throw new ArgumentNullException(nameof(syntaxRoot));
            }

            if (namespaceName is null)
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            //we need return a List<> of namespaces syntax because the following code may exists in single file:
            //namespace a { class a1 {} } namespace a { class a2 {} } namespace a { class a3 {} }
            result = new();

            var allFoundNamespaceSyntaxes = syntaxRoot
                .DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .ToList();


            foreach (var foundNamespaceSyntax in allFoundNamespaceSyntaxes)
            {
                var fnn = foundNamespaceSyntax.Name.ToString();
                if (fnn == namespaceName)
                {
                    result.Add(foundNamespaceSyntax);
                }
            }

            return result.Count > 0;
        }

        /// <summary>
        /// Determine the namespace the given file should belong to.
        /// It is built from the default namespace of the project plus the folders
        /// between the project folder and the file, and is finally modified with
        /// the user defined regex.
        /// The folders excluded by the user (see <see cref="Settings.AdjustNamespaceSettings.SkippedFolderSuffixes"/>)
        /// do not take a part in the resulting name.
        /// </summary>
        /// <param name="project">Project the file belongs to.</param>
        /// <param name="vss">Visual Studio services.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        /// <param name="documentFilePath">Full path to the file.</param>
        /// <returns>
        /// The target namespace, or <c>null</c> if the file is outside of its project folder
        /// (a linked file, for example).
        /// </returns>
        public static async Task<string?> TryDetermineTargetNamespaceAsync(
            this SolutionItem project,
            VsServices vss,
            NamespaceReplaceRegex replaceRegex,
            string documentFilePath
            )
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            if (documentFilePath is null)
            {
                throw new ArgumentNullException(nameof(documentFilePath));
            }

            var projectFolderPath = new FileInfo(project.FullPath).Directory.FullName;
            var documentFolderPath = new FileInfo(documentFilePath).Directory.FullName;

            if (documentFolderPath.Length < projectFolderPath.Length || !documentFolderPath.StartsWith(projectFolderPath))
            {
                return null;
            }

            //collect the folder names from the file folder up to the project folder
            var names = new List<string>();
            var dir = new DirectoryInfo(documentFolderPath);
            while (dir.FullName != projectFolderPath && dir.FullName.Length > projectFolderPath.Length)
            {
                if (!vss.Settings.IsSkippedFolder(dir.FullName))
                {
                    names.Add(dir.Name);
                }

                dir = dir.Parent;
            }

            names.Reverse();
            names.Insert(0, await GetProjectDefaultNamespaceAsync(vss, documentFilePath, project));

            var targetNamespace = string.Join(".", names);

            targetNamespace = replaceRegex.Modify(targetNamespace);

            return targetNamespace;
        }


        /// <summary>
        /// Get the default (root) namespace of the project the file belongs to.
        /// For a C# project and a database (sqlproj) project it is taken from the project
        /// properties; for any other project kind the project name without its last part
        /// is used as a fallback (this is how the shared projects are supported:
        /// `MyApp.Shared` -> `MyApp`).
        /// </summary>
        private static async Task<string> GetProjectDefaultNamespaceAsync(
            VsServices vss,
            string documentFilePath,
            SolutionItem project
            )
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var pi = vss.Dte.Solution.FindProjectItem(documentFilePath);
            if (pi != null)
            {
                var cp = pi.ContainingProject;
                if(cp != null)
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

            //get roslynProject by SolutionItem project
            //if (!string.IsNullOrEmpty(roslynProject.DefaultNamespace))
            //{
            //    return project.DefaultNamespace!;
            //}

            //if (project.IsProjectOfType(SolutionHelper.DatabaseProjectKind))
            //{
            //    var dn = await ((Community.VisualStudio.Toolkit.Project)project).GetAttributeAsync("RootNamespace");
            //    return dn!;
            //}

            var dotIndex = project.Name.LastIndexOf(".");
            if(dotIndex <= 0)
            {
                return project.Name;
            }

            var result = project.Name.Substring(0, dotIndex);
            return result;
        }
    }
}
