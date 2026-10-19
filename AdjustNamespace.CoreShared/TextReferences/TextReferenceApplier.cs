using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Adjusting.Edit.Apply;
using AdjustNamespace.Roslyn;
using AdjustNamespace.Xaml.BodyProvider;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Changes the approved text references and says what has been done with every match.
    ///
    /// The analysis is separated from the application as everywhere else: the approved matches
    /// become the edits of an <see cref="EditSet"/> (a C# document gets
    /// <see cref="ReplaceTextEdit"/>, any other file <see cref="ReplaceFileTextEdit"/>) and
    /// <see cref="EditApplier"/> writes them.
    ///
    /// A match is changed only if the text of the file is still the one it was found in: the
    /// spans are those of the search, and a text that has changed since then (a cancelled run
    /// leaves files behind, the user types) is left alone and reported as such.
    /// </summary>
    public sealed class TextReferenceApplier
    {
        private readonly Workspace _workspace;
        private readonly IXamlBodyProviderFactory _bodyProviders;

        /// <param name="workspace">Roslyn workspace of the solution.</param>
        /// <param name="bodyProviders">How a file that is no C# document is read and written.</param>
        public TextReferenceApplier(
            Workspace workspace,
            IXamlBodyProviderFactory bodyProviders
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (bodyProviders is null)
            {
                throw new ArgumentNullException(nameof(bodyProviders));
            }

            _workspace = workspace;
            _bodyProviders = bodyProviders;
        }

        /// <summary>
        /// Change the approved matches of the search.
        /// </summary>
        /// <returns>
        /// The matches that are shown to the user or are changed, each with what has been done
        /// with it. The unlikely matches nobody has asked for are not among them.
        /// </returns>
        public async Task<IReadOnlyList<TextReferenceResult>> ApplyAsync(
            TextReferenceScanResult scan,
            CancellationToken cancellationToken = default
            )
        {
            if (scan is null)
            {
                throw new ArgumentNullException(nameof(scan));
            }

            var options = scan.Options;

            var results = new List<TextReferenceResult>();
            var edits = new EditSet();

            foreach (var group in scan.Matches.GroupBy(m => m.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? text = null;
                var textRead = false;

                foreach (var match in group)
                {
                    if (!match.IsApproved)
                    {
                        if (options.IsShown(match.Level))
                        {
                            results.Add(new TextReferenceResult(match, false, ReasonOfLeaving(match, options)));
                        }

                        continue;
                    }

                    if (!textRead)
                    {
                        text = await TryReadTextAsync(match);
                        textRead = true;
                    }

                    if (text == null)
                    {
                        results.Add(new TextReferenceResult(match, false, "the file cannot be read"));
                        continue;
                    }

                    if (!IsStillThere(text, match))
                    {
                        results.Add(new TextReferenceResult(match, false, "the text has changed since the search"));
                        continue;
                    }

                    if (match.FileKind == TextReferenceFileKind.CSharp)
                    {
                        edits.ReplaceText(match.FilePath, match.Span, match.ReplacementText, EditKind.TextReference);
                    }
                    else
                    {
                        edits.ReplaceFileText(match.FilePath, match.Span, match.ReplacementText);
                    }

                    results.Add(new TextReferenceResult(match, true, null));
                }
            }

            await new EditApplier(_workspace, _bodyProviders).ApplyAsync(edits);

            return results
                .OrderBy(r => r.Match.Id)
                .ToList();
        }

        private static string ReasonOfLeaving(TextReferenceMatch match, TextReferenceOptions options)
        {
            if (options.Mode == TextReferenceMode.ReportOnly)
            {
                return "the mode is report only";
            }

            return options.IsApplied(match.Level)
                ? "unchecked"
                : $"{match.Level} matches are only offered in the {options.Mode} mode"
                ;
        }

        private static bool IsStillThere(string text, TextReferenceMatch match)
        {
            return match.Span.End <= text.Length
                && string.CompareOrdinal(text, match.Span.Start, match.MatchedText, 0, match.Span.Length) == 0
                && match.MatchedText.Length == match.Span.Length;
        }

        /// <summary>
        /// The text of the file as it is now: the document of the workspace for a C# file,
        /// the file itself (or the open buffer) for any other.
        /// </summary>
        private async Task<string?> TryReadTextAsync(TextReferenceMatch match)
        {
            if (match.FileKind == TextReferenceFileKind.CSharp)
            {
                var document = _workspace.GetDocument(match.FilePath);
                if (document == null)
                {
                    return null;
                }

                return (await document.GetTextAsync()).ToString();
            }

            try
            {
                var provider = await _bodyProviders.CreateForReadAsync(match.FilePath);

                return provider.ReadText();
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
