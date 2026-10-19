using AdjustNamespace.Adjusting.BuildChecking;
using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.TextReferences;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// Collects what a session does and makes an <see cref="AdjustReport"/> of it.
    ///
    /// The adjusters and the cleanup are given a builder and tell it what they change; they
    /// need none (it is optional everywhere), so the report costs nothing to the code which does
    /// not ask for it.
    /// </summary>
    public sealed class AdjustReportBuilder
    {
        private readonly string? _solutionFolder;

        private readonly List<AdjustFileOutcome> _files = new();

        private readonly HashSet<string> _recordedFiles = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Every touched file, the files in the order they were touched first.
        /// </summary>
        private readonly EditLedger _touched = new();

        /// <summary>
        /// The files the cleanup has changed.
        /// </summary>
        private readonly EditLedger _cleanup = new();

        /// <summary>
        /// The edits made because of the adjusting of a subject file, per subject file.
        /// </summary>
        private readonly Dictionary<string, EditLedger> _bySubject = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<TextReferenceResult> _textReferences = new();

        private readonly List<string> _notes = new();

        private bool _textReferencesWereSearched;

        private int _hiddenTextReferences;

        private BuildCheckResult? _buildCheck;

        /// <param name="solutionFolder">Folder of the solution, the paths are shown relative to it.</param>
        public AdjustReportBuilder(
            string? solutionFolder
            )
        {
            _solutionFolder = solutionFolder;
        }

        /// <summary>
        /// The path as the report is going to show it, see <see cref="AdjustReport.DisplayPath"/>.
        /// </summary>
        public string DisplayPath(string filePath)
        {
            return ReportPaths.Relative(filePath, _solutionFolder);
        }

        /// <summary>
        /// Has the outcome of this file been recorded already.
        /// </summary>
        public bool IsRecorded(string filePath)
        {
            return _recordedFiles.Contains(filePath);
        }

        /// <summary>
        /// Record what has happened with a file. A file is recorded once; the first answer wins.
        /// </summary>
        public void RecordFile(AdjustFileOutcome outcome)
        {
            if (outcome is null)
            {
                throw new ArgumentNullException(nameof(outcome));
            }

            if (!_recordedFiles.Add(outcome.FilePath))
            {
                return;
            }

            _files.Add(outcome);
        }

        /// <summary>
        /// Record the edits the adjusting of a subject file is about to write.
        /// </summary>
        /// <param name="subjectFilePath">The file being adjusted.</param>
        /// <param name="edits">What is going to be written.</param>
        public void RecordEdits(
            string subjectFilePath,
            EditSet edits
            )
        {
            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            if (edits is null)
            {
                throw new ArgumentNullException(nameof(edits));
            }

            foreach (var filePath in edits.FilePaths)
            {
                foreach (var edit in edits.EditsOf(filePath))
                {
                    RecordEdit(subjectFilePath, filePath, EditKinds.KindOf(edit), 1);
                }
            }
        }

        /// <summary>
        /// Record an edit of a file made because of the adjusting of a subject file.
        /// </summary>
        /// <param name="subjectFilePath">The file being adjusted.</param>
        /// <param name="filePath">The file the edit is made in.</param>
        /// <param name="kind">What the edit is.</param>
        /// <param name="count">How many of them.</param>
        public void RecordEdit(
            string subjectFilePath,
            string filePath,
            EditKind kind,
            int count
            )
        {
            _touched.Add(filePath, kind, count);

            if (!_bySubject.TryGetValue(subjectFilePath, out var ledger))
            {
                ledger = new EditLedger();
                _bySubject[subjectFilePath] = ledger;
            }

            ledger.Add(filePath, kind, count);
        }

        /// <summary>
        /// Record an edit which belongs to no subject file (a text reference, for example).
        /// </summary>
        public void RecordEdit(
            string filePath,
            EditKind kind,
            int count
            )
        {
            _touched.Add(filePath, kind, count);
        }

        /// <summary>
        /// Record the edits of the cleanup, which belong to no subject file.
        /// </summary>
        public void RecordCleanup(
            string filePath,
            int usingsRemoved,
            int nameofsReplaced
            )
        {
            if (usingsRemoved > 0)
            {
                _touched.Add(filePath, EditKind.UsingRemoved, usingsRemoved);
                _cleanup.Add(filePath, EditKind.UsingRemoved, usingsRemoved);
            }

            if (nameofsReplaced > 0)
            {
                _touched.Add(filePath, EditKind.NameofReplaced, nameofsReplaced);
                _cleanup.Add(filePath, EditKind.NameofReplaced, nameofsReplaced);
            }
        }

        /// <summary>
        /// Record what has been done with the text references.
        /// </summary>
        /// <param name="results">The matches that were shown or changed, each with what was done with it.</param>
        /// <param name="hiddenCount">How many unlikely matches were found and hidden.</param>
        public void RecordTextReferences(
            IReadOnlyList<TextReferenceResult> results,
            int hiddenCount
            )
        {
            if (results is null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            _textReferencesWereSearched = true;
            _textReferences.AddRange(results);
            _hiddenTextReferences += hiddenCount;

            foreach (var result in results.Where(r => r.IsChanged))
            {
                _touched.Add(result.Match.FilePath, EditKind.TextReference, 1);
            }

            foreach (var note in results.Select(r => r.Match.Note).Where(n => n != null).Distinct())
            {
                AddNote(note!);
            }
        }

        /// <summary>
        /// The files the run has changed so far.
        /// </summary>
        public IReadOnlyList<string> TouchedFilePaths => _touched.ToTouchedFiles().Select(f => f.FilePath).ToList();

        /// <summary>
        /// What has been done with the text references so far.
        /// </summary>
        public IReadOnlyList<TextReferenceResult> TextReferenceResults => _textReferences;

        /// <summary>
        /// Record the result of the build check.
        /// </summary>
        public void RecordBuildCheck(BuildCheckResult result)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _buildCheck = result;
        }

        /// <summary>
        /// Add a note the reader of the report has to see; the same note is added once.
        /// </summary>
        public void AddNote(string note)
        {
            if (note is null)
            {
                throw new ArgumentNullException(nameof(note));
            }

            if (!_notes.Contains(note))
            {
                _notes.Add(note);
            }
        }

        /// <summary>
        /// Make the report.
        /// </summary>
        public AdjustReport Build(
            AdjustSessionOutcome outcome,
            string? failureMessage = null
            )
        {
            foreach (var file in _files)
            {
                if (!_bySubject.TryGetValue(file.FilePath, out var ledger))
                {
                    continue;
                }

                var editedFiles = ledger.ToTouchedFiles();

                file.EditedFiles = editedFiles;
                file.EditsInOtherFiles = editedFiles
                    .Where(f => !string.Equals(f.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase))
                    .Sum(f => f.TotalEdits);
            }

            return new AdjustReport(
                outcome,
                failureMessage,
                _solutionFolder,
                _files.ToList(),
                _touched.ToTouchedFiles(),
                _cleanup.ToTouchedFiles(),
                _textReferences.ToList(),
                _textReferencesWereSearched,
                _hiddenTextReferences,
                _notes.ToList(),
                _buildCheck
                );
        }

        /// <summary>
        /// The edits per file and per kind, the files in the order they were first seen.
        /// </summary>
        private sealed class EditLedger
        {
            private readonly Dictionary<string, Dictionary<EditKind, int>> _edits =
                new(StringComparer.OrdinalIgnoreCase);

            private readonly List<string> _order = new();

            public void Add(string filePath, EditKind kind, int count)
            {
                if (!_edits.TryGetValue(filePath, out var kinds))
                {
                    kinds = new Dictionary<EditKind, int>();
                    _edits[filePath] = kinds;
                    _order.Add(filePath);
                }

                kinds.TryGetValue(kind, out var already);
                kinds[kind] = already + count;
            }

            public IReadOnlyList<AdjustTouchedFile> ToTouchedFiles()
            {
                return _order
                    .Select(p => new AdjustTouchedFile(p, _edits[p]))
                    .ToList();
            }
        }
    }
}
