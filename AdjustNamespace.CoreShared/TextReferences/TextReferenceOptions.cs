using AdjustNamespace.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The settings of the search of the text references: how aggressive it is and which
    /// kinds of files it looks into.
    ///
    /// The defaults come from the user options (Tools -> Options in Visual Studio, the command
    /// line of the console utility) and the solution may override them in its settings file
    /// (see <see cref="Resolve"/>).
    /// </summary>
    public sealed class TextReferenceOptions
    {
        /// <summary>
        /// How aggressive the search is.
        /// </summary>
        public TextReferenceMode Mode
        {
            get;
        }

        /// <summary>
        /// The kinds of files the search looks into besides the string literals of C#.
        /// </summary>
        public TextReferenceKinds Kinds
        {
            get;
        }

        /// <summary>
        /// The xml and json files besides the usual configs, see
        /// <see cref="AdjustNamespaceSettings.TextReferenceFiles"/>.
        /// </summary>
        public IReadOnlyList<string> ExtraFiles
        {
            get;
        }

        /// <summary>
        /// The search is performed at all.
        /// </summary>
        public bool IsScanEnabled => Mode != TextReferenceMode.Off;

        /// <summary>
        /// The balanced search of the usual kinds, the default of everything.
        /// </summary>
        public static TextReferenceOptions Default { get; } =
            new TextReferenceOptions(TextReferenceMode.Balanced, TextReferenceKinds.Default);

        /// <summary>
        /// No search.
        /// </summary>
        public static TextReferenceOptions Off { get; } =
            new TextReferenceOptions(TextReferenceMode.Off, TextReferenceKinds.Default);

        public TextReferenceOptions(
            TextReferenceMode mode,
            TextReferenceKinds kinds,
            IReadOnlyList<string>? extraFiles = null
            )
        {
            Mode = mode;
            Kinds = kinds;
            ExtraFiles = extraFiles ?? Array.Empty<string>();
        }

        /// <summary>
        /// A match of this level is changed by the run, unless the user unchecks it.
        /// </summary>
        public bool IsApplied(ConfidenceLevel level)
        {
            switch (Mode)
            {
                case TextReferenceMode.Conservative:
                    return level == ConfidenceLevel.Certain;

                case TextReferenceMode.Balanced:
                    return level >= ConfidenceLevel.Likely;

                case TextReferenceMode.Aggressive:
                    return level >= ConfidenceLevel.Possible;

                default:
                    return false;
            }
        }

        /// <summary>
        /// A match of this level is shown to the user: changed by the run, or offered and left
        /// unchecked. The level Unlikely is hidden unless the user asks for it.
        /// </summary>
        public bool IsShown(ConfidenceLevel level)
        {
            return IsScanEnabled && level >= ConfidenceLevel.Possible;
        }

        /// <summary>
        /// The same options with another mode.
        /// </summary>
        public TextReferenceOptions WithMode(TextReferenceMode mode)
        {
            return new TextReferenceOptions(mode, Kinds, ExtraFiles);
        }

        /// <summary>
        /// The same options with other kinds.
        /// </summary>
        public TextReferenceOptions WithKinds(TextReferenceKinds kinds)
        {
            return new TextReferenceOptions(Mode, kinds, ExtraFiles);
        }

        /// <summary>
        /// The options of a solution: the defaults of the user, overridden by whatever the
        /// settings file of the solution says. A settings file without the elements of the
        /// search (every file written before 0.8.0) overrides nothing.
        /// </summary>
        public static TextReferenceOptions Resolve(
            TextReferenceOptions defaults,
            AdjustNamespaceSettings? settings
            )
        {
            if (defaults is null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            if (settings is null)
            {
                return defaults;
            }

            var mode = defaults.Mode;
            if (!string.IsNullOrWhiteSpace(settings.TextReferenceMode)
                && TryParseMode(settings.TextReferenceMode!, out var parsed))
            {
                mode = parsed;
            }

            var kinds = defaults.Kinds;
            kinds = Override(kinds, TextReferenceKinds.Comments, settings.ScanComments);
            kinds = Override(kinds, TextReferenceKinds.XmlConfigs, settings.ScanXmlConfigs);
            kinds = Override(kinds, TextReferenceKinds.Json, settings.ScanJson);
            kinds = Override(kinds, TextReferenceKinds.Razor, settings.ScanRazor);
            kinds = Override(kinds, TextReferenceKinds.ProjectFiles, settings.ScanProjectFiles);

            var extraFiles = defaults.ExtraFiles
                .Concat(settings.TextReferenceFiles ?? new List<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new TextReferenceOptions(mode, kinds, extraFiles);
        }

        /// <summary>
        /// Read a mode from a text: <c>off</c>, <c>report</c> (or <c>reportonly</c>),
        /// <c>conservative</c>, <c>balanced</c>, <c>aggressive</c>, in any case.
        /// </summary>
        public static bool TryParseMode(string text, out TextReferenceMode mode)
        {
            mode = TextReferenceMode.Balanced;

            switch (text.Trim().Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant())
            {
                case "off":
                    mode = TextReferenceMode.Off;
                    return true;

                case "report":
                case "reportonly":
                    mode = TextReferenceMode.ReportOnly;
                    return true;

                case "conservative":
                    mode = TextReferenceMode.Conservative;
                    return true;

                case "balanced":
                    mode = TextReferenceMode.Balanced;
                    return true;

                case "aggressive":
                    mode = TextReferenceMode.Aggressive;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Read the kinds from a comma separated list: <c>comments</c>, <c>xml</c>,
        /// <c>json</c>, <c>razor</c>, <c>project</c> (<c>none</c> and <c>all</c> too).
        /// </summary>
        public static bool TryParseKinds(string text, out TextReferenceKinds kinds)
        {
            kinds = TextReferenceKinds.None;

            foreach (var part in text.Split(',', ';'))
            {
                switch (part.Trim().ToLowerInvariant())
                {
                    case "":
                    case "none":
                        break;

                    case "all":
                        kinds |= TextReferenceKinds.All;
                        break;

                    case "comments":
                        kinds |= TextReferenceKinds.Comments;
                        break;

                    case "xml":
                        kinds |= TextReferenceKinds.XmlConfigs;
                        break;

                    case "json":
                        kinds |= TextReferenceKinds.Json;
                        break;

                    case "razor":
                        kinds |= TextReferenceKinds.Razor;
                        break;

                    case "project":
                        kinds |= TextReferenceKinds.ProjectFiles;
                        break;

                    default:
                        return false;
                }
            }

            return true;
        }

        private static TextReferenceKinds Override(
            TextReferenceKinds kinds,
            TextReferenceKinds kind,
            bool? value
            )
        {
            if (!value.HasValue)
            {
                return kinds;
            }

            return value.Value
                ? kinds | kind
                : kinds & ~kind
                ;
        }
    }
}
