using System;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The kinds of text the search looks into besides the string literals of C# (which are
    /// always searched, unless the search is off). Every kind is a switch.
    /// </summary>
    [Flags]
    public enum TextReferenceKinds
    {
        /// <summary>
        /// Only the string literals of C#.
        /// </summary>
        None = 0,

        /// <summary>
        /// The comments and the text of the documentation comments of C#.
        /// </summary>
        Comments = 1,

        /// <summary>
        /// The xml configs: <c>app.config</c>, <c>web.config</c>, <c>nlog.config</c> and the other
        /// xml files named in the settings.
        /// </summary>
        XmlConfigs = 2,

        /// <summary>
        /// The json files: <c>appsettings*.json</c> and the other ones named in the settings.
        /// </summary>
        Json = 4,

        /// <summary>
        /// The directives of the <c>.cshtml</c> and <c>.razor</c> files.
        /// </summary>
        Razor = 8,

        /// <summary>
        /// The project files (<c>.csproj</c>) and the <c>.settings</c> files.
        /// </summary>
        ProjectFiles = 16,

        /// <summary>
        /// Everything but the comments: the default.
        /// </summary>
        Default = XmlConfigs | Json | Razor | ProjectFiles,

        /// <summary>
        /// Everything.
        /// </summary>
        All = Comments | XmlConfigs | Json | Razor | ProjectFiles
    }
}
