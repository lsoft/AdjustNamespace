using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Looks for the names of the moved types in the parts of a file a scanner has found
    /// (<see cref="TextCandidate"/>) and gives every name a score.
    ///
    /// The scanners decide where a name may be written; this class decides how sure it is that
    /// a name found there is a reference to the moved type. The score is a list of signals
    /// (<see cref="ConfidenceSignal"/>), so the reason of it is data.
    /// </summary>
    internal sealed class TextReferenceAnalyzer
    {
        private static readonly string[] TestDataFolderMarks = { "testdata", "test-data", "test_data", "fixture" };

        private readonly MovedTypeSet _set;
        private readonly NameMatcher _matcher;

        public TextReferenceAnalyzer(
            MovedTypeSet set
            )
        {
            if (set is null)
            {
                throw new ArgumentNullException(nameof(set));
            }

            _set = set;
            _matcher = new NameMatcher(set);
        }

        /// <summary>
        /// The text names at least one moved type by its full name (a quick check that spares a
        /// scanner the walk through a file that has nothing to find).
        /// </summary>
        public bool MayContainTypes(string fileText)
        {
            return _matcher.FindTypes(fileText, 0, fileText.Length).Count > 0;
        }

        /// <summary>
        /// The text names a moved type by its full name, or a namespace that is changed as a
        /// whole (a Razor <c>@using</c> names nothing else): a quick check for the files whose
        /// namespaces are searched.
        /// </summary>
        public bool MayContainNames(string fileText)
        {
            if (MayContainTypes(fileText))
            {
                return true;
            }

            return _set.Namespaces.Any(
                n => n.IsEmptied
                    && n.NewName != null
                    && fileText.IndexOf(n.Name, StringComparison.Ordinal) >= 0
                );
        }

        /// <summary>
        /// Score the names found in the candidates of a file.
        /// </summary>
        /// <param name="filePath">Full path to the file.</param>
        /// <param name="fileKind">What kind of file it is.</param>
        /// <param name="fileText">The text of the file.</param>
        /// <param name="candidates">The parts of the file that may name a type.</param>
        public List<TextReferenceMatch> Analyze(
            string filePath,
            TextReferenceFileKind fileKind,
            string fileText,
            IEnumerable<TextCandidate> candidates
            )
        {
            var result = new List<TextReferenceMatch>();
            var lines = new LineMap(fileText);
            var seen = new HashSet<(int, int)>();

            var isTestData = IsTestData(filePath);

            foreach (var candidate in candidates)
            {
                if (candidate.ExpectsNamespace)
                {
                    var namespaceMatch = AnalyzeNamespace(filePath, fileKind, fileText, candidate, lines, isTestData);
                    if (namespaceMatch != null && seen.Add((namespaceMatch.Span.Start, namespaceMatch.Span.Length)))
                    {
                        result.Add(namespaceMatch);
                    }

                    if (!candidate.AlsoSearchTypes)
                    {
                        continue;
                    }
                }

                foreach (var occurrence in _matcher.FindTypes(fileText, candidate.Start, candidate.Length))
                {
                    if (!seen.Add((occurrence.Start, occurrence.Length)))
                    {
                        continue;
                    }

                    result.Add(AnalyzeType(filePath, fileKind, fileText, candidate, occurrence, lines, isTestData));
                }
            }

            return result;
        }

        private TextReferenceMatch AnalyzeType(
            string filePath,
            TextReferenceFileKind fileKind,
            string fileText,
            TextCandidate candidate,
            NameOccurrence occurrence,
            LineMap lines,
            bool isTestData
            )
        {
            var type = occurrence.Type;

            var regionEnd = candidate.Start + candidate.Length;
            var occurrenceText = fileText.Substring(occurrence.FullStart, occurrence.FullEnd - occurrence.FullStart);
            var isWhole = fileText.Substring(candidate.Start, candidate.Length).Trim() == occurrenceText;

            var signals = new List<ConfidenceSignal>
            {
                ScoreSignals.Place(candidate.Place, isWhole)
            };

            if (occurrence.WrittenAssembly != null && type.AssemblyName != null)
            {
                signals.Add(
                    string.Equals(occurrence.WrittenAssembly, type.AssemblyName, StringComparison.OrdinalIgnoreCase)
                        ? ScoreSignals.AssemblyOfTheMovedType
                        : ScoreSignals.AssemblyOfAnotherType
                    );
            }

            if (MatchesTheRealMetadataForm(occurrence, type))
            {
                signals.Add(ScoreSignals.MetadataForm);
            }

            if (isWhole)
            {
                signals.Add(ScoreSignals.ExactName);
            }
            else if (StandsInProse(fileText, candidate.Start, regionEnd, occurrence))
            {
                signals.Add(ScoreSignals.Prose);
            }

            if (LooksLikeAFile(fileText, candidate.Start, regionEnd, occurrence))
            {
                signals.Add(ScoreSignals.LooksLikeAFile);
            }

            if (type.HasLookalikeElsewhere)
            {
                signals.Add(ScoreSignals.MeansSomethingElseToo);
            }

            if (isTestData)
            {
                signals.Add(ScoreSignals.TestData);
            }

            return CreateMatch(
                filePath,
                fileKind,
                candidate,
                lines,
                new TextSpan(occurrence.Start, occurrence.Length),
                type.OldFullName,
                type.NewFullName,
                type,
                null,
                new ConfidenceScore(signals)
                );
        }

        private TextReferenceMatch? AnalyzeNamespace(
            string filePath,
            TextReferenceFileKind fileKind,
            string fileText,
            TextCandidate candidate,
            LineMap lines,
            bool isTestData
            )
        {
            var found = _matcher.FindWholeNamespace(fileText, candidate.Start, candidate.Length);
            if (found == null)
            {
                return null;
            }

            var @namespace = found.Value.Namespace;

            //a namespace that stays alive is still there, and one that is split between several
            //new places has no single name to be replaced with
            if (!@namespace.IsEmptied || @namespace.NewName == null)
            {
                return null;
            }

            var signals = new List<ConfidenceSignal>
            {
                ScoreSignals.Place(candidate.Place, true),
                ScoreSignals.ExactName
            };

            if (@namespace.HasLookalikeElsewhere)
            {
                signals.Add(ScoreSignals.MeansSomethingElseToo);
            }

            if (isTestData)
            {
                signals.Add(ScoreSignals.TestData);
            }

            var total = signals.Sum(s => s.Weight);
            if (total > Confidence.EmptiedNamespaceCap)
            {
                signals.Add(ScoreSignals.EmptiedNamespaceCap(total));
            }

            return CreateMatch(
                filePath,
                fileKind,
                candidate,
                lines,
                new TextSpan(found.Value.Start, found.Value.Length),
                @namespace.Name,
                @namespace.NewName,
                null,
                @namespace,
                new ConfidenceScore(signals)
                );
        }

        private static TextReferenceMatch CreateMatch(
            string filePath,
            TextReferenceFileKind fileKind,
            TextCandidate candidate,
            LineMap lines,
            TextSpan span,
            string matchedText,
            string replacementText,
            MovedType? type,
            MovedNamespace? @namespace,
            ConfidenceScore score
            )
        {
            var line = lines.LineOf(span.Start);

            return new TextReferenceMatch(
                filePath,
                fileKind,
                candidate.Place,
                span,
                line + 1,
                span.Start - lines.StartOf(line),
                lines.TextOf(line),
                matchedText,
                replacementText,
                type,
                @namespace,
                score,
                candidate.Note
                );
        }

        /// <summary>
        /// <c>Outer+Inner</c> names a type that is really nested into the moved one, and
        /// <c>C`1</c> has the arity of the moved type.
        /// </summary>
        private static bool MatchesTheRealMetadataForm(NameOccurrence occurrence, MovedType type)
        {
            if (occurrence.WrittenNestedPath.Length > 0)
            {
                return type.NestedTypeNames.Contains(occurrence.WrittenNestedPath);
            }

            return occurrence.WrittenArity > 0 && occurrence.WrittenArity == type.Arity;
        }

        /// <summary>
        /// A letter or a space is right next to the name: a sentence and not a value.
        /// </summary>
        private static bool StandsInProse(string text, int regionStart, int regionEnd, NameOccurrence occurrence)
        {
            if (occurrence.FullStart > regionStart && IsProseCharacter(text[occurrence.FullStart - 1]))
            {
                return true;
            }

            return occurrence.FullEnd < regionEnd && IsProseCharacter(text[occurrence.FullEnd]);
        }

        private static bool IsProseCharacter(char c)
        {
            return char.IsLetter(c) || char.IsWhiteSpace(c);
        }

        /// <summary>
        /// A resource or a file name (<c>A.B.C.resources</c>, <c>A.B.C.cs</c>), or a part of a
        /// path or of a URL.
        /// </summary>
        private static bool LooksLikeAFile(string text, int regionStart, int regionEnd, NameOccurrence occurrence)
        {
            if (occurrence.DottedRemainder.Split('.').Any(s => s.Length > 0 && char.IsLower(s[0])))
            {
                return true;
            }

            if (occurrence.FullStart > regionStart && IsPathSeparator(text[occurrence.FullStart - 1]))
            {
                return true;
            }

            return occurrence.FullEnd < regionEnd && IsPathSeparator(text[occurrence.FullEnd]);
        }

        private static bool IsPathSeparator(char c)
        {
            return c == '/' || c == '\\';
        }

        /// <summary>
        /// The file lies under a folder that holds the data of tests, or is a resource file.
        /// </summary>
        private static bool IsTestData(string filePath)
        {
            if (filePath.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var folder = Path.GetDirectoryName(filePath) ?? string.Empty;

            foreach (var part in folder.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                foreach (var mark in TestDataFolderMarks)
                {
                    if (part.IndexOf(mark, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
