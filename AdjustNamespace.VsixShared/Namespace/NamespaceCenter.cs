using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// Namespace state container. It accumulates a changes during adjusting.
    /// </summary>
    public class NamespaceCenter
    {
        /// <summary>
        /// Types (full names) of the solution grouped by their containing namespace.
        /// A type is removed from here as soon as it has been moved into another namespace.
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> _types;

        /// <summary>
        /// Emptyfied namespaces. They need to be removed at the end of adjusting procedure.
        /// </summary>
        private readonly HashSet<string> _namespacesToRemove;

        /// <summary>
        /// The namespaces a type has been moved out of, no matter whether they are empty now.
        /// Such a namespace may be gone for one project and still be alive for another one,
        /// see <see cref="GetRemovedNamespaces"/>.
        /// </summary>
        private readonly HashSet<string> _touchedNamespaces;

        private NamespaceCenter(
            Dictionary<string, HashSet<string>> types
            )
        {
            if (types is null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            _types = types;
            _namespacesToRemove = new HashSet<string>();
            _touchedNamespaces = new HashSet<string>();
        }

        /// <summary>
        /// Filter incoming namespaces and return only those are allowed to delete
        /// (namespaces that does not exists after adjusting).
        /// </summary>
        /// <param name="namespacesToCheck">Using clauses of a document.</param>
        /// <param name="compilations">
        /// Compilations of the projects which compile that document, or <c>null</c> if they
        /// are unknown. A namespace is emptied for the whole solution, but a using clause is
        /// resolved against a single project: a namespace which another project still fills
        /// is not empty and is gone for this one nevertheless.
        /// </param>
        /// <returns>Those of them which point to an emptied namespace.</returns>
        public List<SyntaxNode> GetRemovedNamespaces(
            IReadOnlyList<UsingDirectiveSyntax> namespacesToCheck,
            IReadOnlyList<Compilation>? compilations = null
            )
        {
            if (namespacesToCheck is null)
            {
                throw new ArgumentNullException(nameof(namespacesToCheck));
            }

            if (namespacesToCheck.Count == 0)
            {
                return new List<SyntaxNode>();
            }

            var toRemove = new List<SyntaxNode>(namespacesToCheck.Count);

            foreach (var n in namespacesToCheck)
            {
                var nname = NormalizeUsingName(n.Name.ToString());

                if (!_namespacesToRemove.Contains(nname) && !IsGoneForTheseProjects(nname, compilations))
                {
                    //there are a types in this namespace
                    continue;
                }

                toRemove.Add(n);
            }

            return toRemove;
        }

        /// <summary>
        /// The adjusting has taken a type out of this namespace and there is nothing of it
        /// left in any of the given compilations: a using clause of it does not compile
        /// anymore, even though another project of the solution still fills that namespace.
        ///
        /// All of these compilations compile the very same text, so the clause has to stay
        /// as soon as a single one of them still has that namespace.
        /// </summary>
        private bool IsGoneForTheseProjects(
            string namespaceName,
            IReadOnlyList<Compilation>? compilations
            )
        {
            if (compilations == null || compilations.Count == 0)
            {
                return false;
            }

            if (!_touchedNamespaces.Contains(namespaceName))
            {
                //we have not touched it, so it is not our business
                return false;
            }

            foreach (var compilation in compilations)
            {
                if (compilation.TryFindNamespace(namespaceName) != null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The name of a using clause, as the namespaces are named here.
        /// The clause is taken from the document as it is written by the user, and it may
        /// contain the whitespace between the parts of the name (<c>using A . B;</c>)
        /// and the <c>global::</c> alias (<c>using global::A.B;</c>).
        /// </summary>
        private static string NormalizeUsingName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (!char.IsWhiteSpace(c))
                {
                    builder.Append(c);
                }
            }

            var result = builder.ToString();

            const string GlobalPrefix = "global::";
            if (result.StartsWith(GlobalPrefix))
            {
                result = result.Substring(GlobalPrefix.Length);
            }

            return result;
        }

        /// <summary>
        /// Report that the given type has been moved out of its namespace.
        /// If it was the last type of that namespace, the namespace is marked
        /// as the subject to remove from the using clauses.
        /// </summary>
        public void TypeRemoved(ITypeSymbol type)
        {
            var cnn = type.ContainingNamespace.ToDisplayString();

            _touchedNamespaces.Add(cnn);

            if (!_types.TryGetValue(cnn, out var set))
            {
                return;
            }

            if (IsDeclaredInSeveralFiles(type))
            {
                //a partial type, and only one of its files is being adjusted right now:
                //the rest of its declarations stay in this namespace, so it is not
                //an empty one. When the last file of it is adjusted, there is a single
                //declaration left and the namespace is emptied as usual.
                return;
            }

            var tn = type.ToDisplayString();

            if (!set.Contains(tn))
            {
                return;
            }

            set.Remove(tn);

            if (set.Count == 0)
            {
                _namespacesToRemove.Add(cnn);
            }
        }

        /// <summary>
        /// Report that the given type has been moved into the target namespace.
        /// Such a namespace exists after the adjusting even if another file of the same
        /// session has emptied it before (this is what a reorganization of the folders
        /// looks like: A.B goes to X.Y and Other.C comes into A.B).
        /// </summary>
        /// <param name="type">The moved type, as it was before the move.</param>
        /// <param name="targetNamespace">The namespace it has been moved into.</param>
        public void TypeAdded(ITypeSymbol type, string targetNamespace)
        {
            if (!_types.TryGetValue(targetNamespace, out var set))
            {
                set = new HashSet<string>();
                _types[targetNamespace] = set;
            }

            set.Add(BuildNameIn(type, targetNamespace));

            _namespacesToRemove.Remove(targetNamespace);
        }

        /// <summary>
        /// The full name the type will have in the given namespace.
        /// </summary>
        private static string BuildNameIn(ITypeSymbol type, string targetNamespace)
        {
            var typeFullName = type.ToDisplayString();
            var containingNamespaceName = type.ContainingNamespace.ToDisplayString();

            if (!typeFullName.StartsWith(containingNamespaceName + "."))
            {
                return typeFullName;
            }

            return targetNamespace + "." + typeFullName.Substring(containingNamespaceName.Length + 1);
        }

        /// <summary>
        /// The type is a partial one declared in more than a single file.
        /// The repeated declarations inside one file do not count: such a file is
        /// adjusted at once.
        /// </summary>
        private static bool IsDeclaredInSeveralFiles(ITypeSymbol type)
        {
            var filePaths = new HashSet<string>();

            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                filePaths.Add(reference.SyntaxTree.FilePath);
            }

            return filePaths.Count > 1;
        }

        /// <summary>
        /// Build a namespace center for a whole workspace.
        /// </summary>
        public static async Task<NamespaceCenter> CreateForAsync(
            Workspace workspace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            var typeContainer = await TypeContainer.CreateForAsync(workspace);

            var types = new Dictionary<string, HashSet<string>>(typeContainer.DictByFullName.Count);
            foreach (var nte in typeContainer.DictByFullName.Values)
            {
                var key = nte.ContainingNamespaceName;
                if (!types.ContainsKey(key))
                {
                    types[key] = new HashSet<string>();
                }
                types[key].Add(nte.TypeFullName);
            }

            var result = new NamespaceCenter(types);
            return result;
        }

    }
}
