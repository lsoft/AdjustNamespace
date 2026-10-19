namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The kinds of the files the search looks into, each with a scanner of its own.
    /// </summary>
    public enum TextReferenceFileKind
    {
        /// <summary>
        /// A C# document: the string literals and, if asked for, the comments.
        /// </summary>
        CSharp,

        /// <summary>
        /// A <c>.cshtml</c> or <c>.razor</c> file.
        /// </summary>
        Razor,

        /// <summary>
        /// A project file (<c>.csproj</c>).
        /// </summary>
        Project,

        /// <summary>
        /// A <c>.settings</c> file.
        /// </summary>
        Settings,

        /// <summary>
        /// An xml config: <c>app.config</c>, <c>web.config</c>, <c>nlog.config</c>, ...
        /// </summary>
        XmlConfig,

        /// <summary>
        /// A json file: <c>appsettings.json</c>, ...
        /// </summary>
        Json
    }
}
