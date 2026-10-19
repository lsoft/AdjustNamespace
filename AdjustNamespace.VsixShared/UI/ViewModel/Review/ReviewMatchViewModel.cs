using AdjustNamespace.TextReferences;
using System;
using System.Text;
using System.Windows.Media;

namespace AdjustNamespace.UI.ViewModel.Review
{
    /// <summary>
    /// A text reference on the review step: a row with a check box, the level badge, the line
    /// with the name highlighted and what the name is going to be.
    /// </summary>
    public sealed class ReviewMatchViewModel : BaseViewModel
    {
        /// <summary>
        /// How much of the line after the name is shown.
        /// </summary>
        private const int MaxLineTail = 80;

        private readonly Action _changed;

        /// <summary>
        /// The match the row stands for.
        /// </summary>
        public TextReferenceMatch Match
        {
            get;
        }

        /// <summary>
        /// The run changes this text. The state lives in the match itself, which is what the
        /// session reads.
        /// </summary>
        public bool IsChecked
        {
            get => Match.IsApproved;
            set
            {
                Match.IsApproved = value;

                OnPropertyChanged(nameof(IsChecked));

                _changed();
            }
        }

        /// <summary>
        /// The level in words: the text of the badge.
        /// </summary>
        public string LevelText => Match.Level.ToString();

        /// <summary>
        /// The color of the badge.
        /// </summary>
        public Brush LevelBrush
        {
            get;
        }

        /// <summary>
        /// The line of the file, starting with 1.
        /// </summary>
        public int Line => Match.Line;

        /// <summary>
        /// The line in front of the name (without the indentation).
        /// </summary>
        public string LineBefore
        {
            get;
        }

        /// <summary>
        /// The name as it is written: highlighted.
        /// </summary>
        public string LineMatch
        {
            get;
        }

        /// <summary>
        /// The line behind the name.
        /// </summary>
        public string LineAfter
        {
            get;
        }

        /// <summary>
        /// <c>old -> new</c>.
        /// </summary>
        public string Change => $"{Match.MatchedText} -> {Match.ReplacementText}";

        /// <summary>
        /// The reason of the score: one signal per line.
        /// </summary>
        public string ToolTip
        {
            get;
        }

        public ReviewMatchViewModel(
            TextReferenceMatch match,
            Action changed
            )
        {
            if (match is null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (changed is null)
            {
                throw new ArgumentNullException(nameof(changed));
            }

            Match = match;
            _changed = changed;

            LevelBrush = BrushOf(match.Level);

            SplitLine(match, out var before, out var highlighted, out var after);
            LineBefore = before;
            LineMatch = highlighted;
            LineAfter = after;

            ToolTip = BuildToolTip(match);
        }

        private static Brush BrushOf(ConfidenceLevel level)
        {
            Color color;

            switch (level)
            {
                case ConfidenceLevel.Certain:
                    color = Color.FromRgb(0x2E, 0x7D, 0x32);
                    break;

                case ConfidenceLevel.Likely:
                    color = Color.FromRgb(0x15, 0x65, 0xC0);
                    break;

                case ConfidenceLevel.Possible:
                    color = Color.FromRgb(0xB2, 0x6A, 0x00);
                    break;

                default:
                    color = Color.FromRgb(0x61, 0x61, 0x61);
                    break;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();

            return brush;
        }

        /// <summary>
        /// The line cut into the part in front of the name, the name and the rest.
        /// </summary>
        private static void SplitLine(
            TextReferenceMatch match,
            out string before,
            out string highlighted,
            out string after
            )
        {
            var line = match.LineText;
            var indentation = line.Length - line.TrimStart().Length;

            var start = Math.Max(indentation, Math.Min(match.Column, line.Length));
            var length = Math.Max(0, Math.Min(match.Span.Length, line.Length - start));

            before = line.Substring(indentation, start - indentation);
            highlighted = line.Substring(start, length);

            var tail = line.Substring(start + length).TrimEnd();
            after = tail.Length > MaxLineTail
                ? tail.Substring(0, MaxLineTail) + "..."
                : tail
                ;
        }

        private static string BuildToolTip(TextReferenceMatch match)
        {
            var text = new StringBuilder();

            text.Append(match.Level).Append(' ').Append(match.Score.Value).Append(": ").Append(match.FilePath).Append('\n');

            foreach (var signal in match.Score.Signals)
            {
                text.Append("  ").Append(signal).Append('\n');
            }

            if (match.Note != null)
            {
                text.Append(match.Note).Append('\n');
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// Tell the view that the state of the match has been changed from the outside.
        /// </summary>
        public void RefreshChecked()
        {
            OnPropertyChanged(nameof(IsChecked));
        }
    }
}
