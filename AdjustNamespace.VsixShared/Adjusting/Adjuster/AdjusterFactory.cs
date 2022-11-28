using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Adjuster
{
    public class AdjusterFactory
    {
        private readonly VsServices _vss;
        private readonly bool _openFilesToEnableUndo;
        private readonly NamespaceCenter _namespaceCenter;
        private readonly List<string> _xamlFilePaths;

        public static async Task<AdjusterFactory> CreateAsync(
            VsServices vss,
            bool openFilesToEnableUndo,
            NamespaceCenter namespaceCenter
            )
        {
            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            //get all xaml files in current solution
            var filePaths = vss.Dte.Solution.ProcessSolution();
            var xamlFilePaths = filePaths.FindAll(fp => fp.EndsWith(".xaml"));

            return new AdjusterFactory(
                vss,
                openFilesToEnableUndo,
                namespaceCenter,
                xamlFilePaths
                );

        }

        private AdjusterFactory(
            VsServices vss,
            bool openFilesToEnableUndo,
            NamespaceCenter namespaceCenter,
            List<string> xamlFilePaths
            )
        {
            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            if (xamlFilePaths is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePaths));
            }

            _vss = vss;
            _openFilesToEnableUndo = openFilesToEnableUndo;
            _namespaceCenter = namespaceCenter;
            _xamlFilePaths = xamlFilePaths;
        }

        public async Task<IAdjuster?> CreateAsync(
            string subjectFilePath
            )
        {
            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            if (!_vss.Dte.Solution.TryGetProjectItem(subjectFilePath, out var subjectProject, out var subjectProjectItem))
            {
                return null;
            }

            var roslynProject = _vss.Workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == subjectProjectItem!.ContainingProject.FullName);
            if (roslynProject == null)
            {
                return null;
            }

            var targetNamespace = await roslynProject.TryDetermineTargetNamespaceAsync(subjectFilePath, _vss);
            if (string.IsNullOrEmpty(targetNamespace))
            {
                return null;
            }

            if (subjectFilePath.EndsWith(".xaml"))
            {
                //it's a xaml

                var xamlAdjuster = new XamlAdjuster(
                    _vss,
                    _openFilesToEnableUndo,
                    subjectFilePath,
                    targetNamespace!
                    );
                return xamlAdjuster;
            }
            else
            {
                //we can do nothing with not a C# documents
                var subjectDocument = _vss.Workspace.GetDocument(subjectFilePath);
                if (!subjectDocument.IsDocumentInScope())
                {
                    return null;
                }

                var csAdjuster = new CsAdjuster(
                    _vss,
                    _openFilesToEnableUndo,
                    _namespaceCenter,
                    subjectFilePath,
                    targetNamespace!,
                    _xamlFilePaths
                    );

                return csAdjuster;
            }
        }
    }
}
