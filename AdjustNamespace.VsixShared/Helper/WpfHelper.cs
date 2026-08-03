using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace AdjustNamespace.Helper
{
    /// <summary>
    /// Helpers to search through the WPF logical and visual trees.
    /// </summary>
    public static class WpfHelper
    {
        /// <summary>
        /// All the children of the given type in the logical tree (recursively).
        /// </summary>
        public static IEnumerable<T> FindLogicalChildren<T>(this DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                foreach (var rawChild in LogicalTreeHelper.GetChildren(depObj))
                {
                    if (rawChild is DependencyObject)
                    {
                        var child = (DependencyObject)rawChild;
                        if (child is T)
                        {
                            yield return (T)child;
                        }

                        foreach (var childOfChild in child.FindLogicalChildren<T>())
                        {
                            yield return childOfChild;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// All the children of the given type in the visual tree.
        /// The search does not descend into the found children.
        /// </summary>
        public static List<TChildItem> FindVisualChildren<TChildItem>(
            this DependencyObject obj
                )
           where TChildItem : DependencyObject
        {
            var result = new List<TChildItem>();

            obj.FindVisualChildren(
                result
                );

            return result;
        }

        /// <summary>
        /// All the children of the given type in the visual tree, appended to the given list.
        /// </summary>
        public static void FindVisualChildren<TChildItem>(
            this DependencyObject obj,
            List<TChildItem> result
            )
           where TChildItem : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);

                if (child is TChildItem tchi)
                {
                    result.Add(tchi);
                }
                else
                {
                    child.FindVisualChildren(result);
                }
            }
        }
    }
}
