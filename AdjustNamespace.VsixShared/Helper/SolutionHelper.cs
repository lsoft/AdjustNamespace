using EnvDTE;
using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices;

namespace AdjustNamespace.Helper
{
    public static class SolutionHelper
    {
        //public const string SolutionKind = "{52AEFF70-BBD8-11d2-8598-006097C68E81}";

        //public const string ProjectItemKindFolder = "{6BB5F8EF-4483-11D3-8BCF-00C04F8EC28C}";
        //public const string ProjectItemKindFile = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";

        public const string DatabaseProjectKind = "{00d1a9c2-b5f0-4af3-8072-f6c62b433612}";
        //public const string DatabaseProjectItemKindFolder = "{6bb5f8ef-4483-11d3-8bcf-00c04f8ec28c}";
        //public const string DatabaseProjectItemKindFile = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";


        public static bool IsProjectOfType(this SolutionItem project, string projectType)
        {
            if (project.Type != SolutionItemType.Project)
            {
                throw new InvalidOperationException($"{project.FullPath} is not a project!");
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            project.GetItemInfo(
                out Microsoft.VisualStudio.Shell.Interop.IVsHierarchy h,
                out uint itemId,
                out IVsHierarchyItem hi
                );
            var r = h.IsProjectOfType(SolutionHelper.DatabaseProjectKind);

            return r;
        }

        public static string GetDefaultNamespace(
            this SolutionItem project
            )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            project.GetItemInfo(
                out Microsoft.VisualStudio.Shell.Interop.IVsHierarchy h,
                out uint itemId,
                out IVsHierarchyItem hi
                );
            var r = h.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_DefaultNamespace, out var propbody);
            if (!ErrorHandler.Succeeded(r))
            {
                throw new InvalidOperationException("Cannot get property!");
            }

            return propbody.ToString();
        }



        public static List<SolutionItem> ProcessDownRecursivelyFor(
            this SolutionItem item,
            SolutionItemType type,
            string? fullPath
            )
        {
            var result = new List<SolutionItem>();

            if (item.Type == type && (string.IsNullOrEmpty(fullPath) || fullPath == item.FullPath))
            {
                result.Add(item);
            }

            foreach (var child in item.Children)
            {
                if (child != null)
                {
                    result.AddRange(child.ProcessDownRecursivelyFor(type, fullPath));
                }
            }

            return result;
        }

        public static async Task<List<string>> GetAllFilesFromAsync(
            )
        {
            var solution = await VS.Solutions.GetCurrentSolutionAsync();

            if (solution == null)
            {
                return new List<string>();
            }

            var files = solution.ProcessDownRecursivelyFor(SolutionItemType.PhysicalFile, null);
            return files.ConvertAll(i => i.FullPath!).FindAll(i => !string.IsNullOrEmpty(i));
        }


        public static async Task<(bool result, SolutionItem? rProject, SolutionItem? rProjectItem)> TryGetProjectItemAsync(
            string filePath
            )
        {
            var solution = await VS.Solutions.GetCurrentSolutionAsync();

            if (solution != null)
            {
                var projects = solution.ProcessDownRecursivelyFor(SolutionItemType.Project, null);
                foreach (var project in projects)
                {
                    var files = project.ProcessDownRecursivelyFor(SolutionItemType.PhysicalFile, filePath);
                    if (files.Count > 0)
                    {
                        return (true, project, files[0]);
                    }
                }
            }

            return (false, null, null);
        }
    }
}
