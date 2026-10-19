using AdjustNamespace.TextReferences;
using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel.Select
{
    /// <summary>
    /// An item of the combo box of the aggressiveness of the search of the text references.
    /// </summary>
    public sealed class TextModeChoice
    {
        /// <summary>
        /// The mode.
        /// </summary>
        public TextReferenceMode Mode
        {
            get;
        }

        /// <summary>
        /// The words the mode is shown as.
        /// </summary>
        public string Title
        {
            get;
        }

        /// <summary>
        /// What the mode does, shown as a tooltip.
        /// </summary>
        public string Description
        {
            get;
        }

        public TextModeChoice(
            TextReferenceMode mode,
            string title,
            string description
            )
        {
            Mode = mode;
            Title = title;
            Description = description;
        }

        /// <summary>
        /// Every mode, from the weakest to the boldest.
        /// </summary>
        public static IReadOnlyList<TextModeChoice> All { get; } = new[]
        {
            new TextModeChoice(TextReferenceMode.Off, "Off", "Do not search for the text references."),
            new TextModeChoice(TextReferenceMode.ReportOnly, "Report only", "Search and list everything from the level Possible, change nothing."),
            new TextModeChoice(TextReferenceMode.Conservative, "Conservative", "Change the Certain matches, offer the Likely and the Possible ones."),
            new TextModeChoice(TextReferenceMode.Balanced, "Balanced", "Change the Certain and the Likely matches, offer the Possible ones."),
            new TextModeChoice(TextReferenceMode.Aggressive, "Aggressive", "Change the Certain, the Likely and the Possible matches.")
        };
    }
}
