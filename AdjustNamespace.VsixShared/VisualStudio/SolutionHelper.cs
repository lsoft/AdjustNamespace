using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// Helpers to walk through the solution tree (Community.VisualStudio.Toolkit objects).
    /// Almost everything here requires the main thread.
    /// </summary>
    public static class SolutionHelper
    {
        /// <summary>
        /// Kind (guid) of a database (sqlproj) project.
        /// </summary>
        public const string DatabaseProjectKind = "{00d1a9c2-b5f0-4af3-8072-f6c62b433612}";

        /// <summary>
        /// Kind (guid) of a C# project.
        /// </summary>
        public const string CSharpProjectKind = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";

        /// <summary>
        /// Descend the solution tree and collect the items of the requested type.
        /// The items which are visible in the `show all files` mode only
        /// (i.e. those which are not the members of a project) are skipped.
        /// </summary>
        /// <param name="item">The item to start from (a solution, a project, a folder).</param>
        /// <param name="type">Type of the items to collect.</param>
        /// <param name="fullPath">Full path of the single item to search for; <c>null</c> to collect all of them.</param>
        public static async Task<List<SolutionItem>> ProcessDownRecursivelyForAsync(
            this SolutionItem item,
            SolutionItemType type,
            string? fullPath
            )
        {
            var result = new List<SolutionItem>();

            //https://github.com/VsixCommunity/Community.VisualStudio.Toolkit/issues/401
            item.GetItemInfo(out IVsHierarchy hierarchy, out uint itemID, out _);
            if (HierarchyUtilities.TryGetHierarchyProperty(hierarchy, itemID, (int)__VSHPROPID.VSHPROPID_IsNonMemberItem, out bool isNonMemberItem))
            {
                if (isNonMemberItem)
                {
                    // The item is not usually visible. Skip it.
                    return result;
                }
            }

            if (item.Type == type && (string.IsNullOrEmpty(fullPath) || fullPath == item.FullPath))
            {
                result.Add(item);
            }

            foreach (var child in item.Children)
            {
                if (child == null)
                {
                    continue;
                }

                result.AddRange(await child.ProcessDownRecursivelyForAsync(type, fullPath));
            }

            return result;
        }

        /// <summary>
        /// Full paths of every physical file of the currently opened solution.
        /// </summary>
        public static async Task<List<string>> GetAllFilesFromAsync(
            )
        {
            var solution = await VS.Solutions.GetCurrentSolutionAsync();

            if (solution == null)
            {
                return new List<string>();
            }

            var files = await solution.ProcessDownRecursivelyForAsync(SolutionItemType.PhysicalFile, null);
            return files.ConvertAll(i => i.FullPath!).FindAll(i => !string.IsNullOrEmpty(i));
        }


        /// <summary>
        /// Build the map `file full path -> (its project, its project item)`.
        /// It is much faster than to search for the files one by one.
        /// If a file belongs to more than one project (a shared project, a multi-target project),
        /// the first found project wins.
        /// </summary>
        /// <param name="projects">Projects to scan; <c>null</c> to scan the whole solution.</param>
        public static async Task<Dictionary<string, ProjectItemInformation>> GetAllProjectItemsAsync(
            List<SolutionItem>? projects
            )
        {
            var result = new Dictionary<string, ProjectItemInformation>();

            var solution = await VS.Solutions.GetCurrentSolutionAsync();
            if (solution == null)
            {
                return result;
            }

            if(projects == null)
            {
                projects = await solution.ProcessDownRecursivelyForAsync(SolutionItemType.Project, null);
            }

            foreach (var project in projects!)
            {
                var files = await project.ProcessDownRecursivelyForAsync(SolutionItemType.PhysicalFile, null);
                foreach(var file in files)
                {
                    if(string.IsNullOrEmpty(file.FullPath))
                    {
                        continue;
                    }

                    if(result.ContainsKey(file.FullPath!))
                    {
                        continue;
                    }

                    result[file.FullPath!] = new ProjectItemInformation(project, file);
                }
            }

            return result;
        }

    }
}
