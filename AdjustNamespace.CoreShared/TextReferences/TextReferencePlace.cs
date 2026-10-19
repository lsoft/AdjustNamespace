namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The place a name of a type is found in: the base of the score of a match.
    /// </summary>
    public enum TextReferencePlace
    {
        /// <summary>
        /// A Razor directive: <c>@using</c>, <c>@model</c>, <c>@inherits</c>, <c>@inject</c>,
        /// <c>@typeparam</c>, <c>@namespace</c>.
        /// </summary>
        RazorDirective,

        /// <summary>
        /// <c>&lt;StartupObject&gt;</c> of a project file.
        /// </summary>
        ProjectStartupObject,

        /// <summary>
        /// <c>GeneratedClassNamespace</c> of a <c>.settings</c> file.
        /// </summary>
        SettingsNamespace,

        /// <summary>
        /// <c>CustomToolNamespace</c> of a project item.
        /// </summary>
        CustomToolNamespace,

        /// <summary>
        /// A string literal that is passed as a type name: <c>Type.GetType("...")</c>,
        /// <c>[TypeConverter("...")]</c>, a parameter named <c>typeName</c>.
        /// </summary>
        KnownTypeNameArgument,

        /// <summary>
        /// The <c>type</c>, <c>typeName</c>, <c>contract</c>, <c>factory</c> attribute (or the
        /// <c>name</c> of a <c>service</c>) of an xml config.
        /// </summary>
        XmlTypeAttribute,

        /// <summary>
        /// The value of <c>$type</c>, <c>type</c>, <c>class</c> or <c>using</c> of a json file.
        /// </summary>
        JsonTypeValue,

        /// <summary>
        /// Any other string literal of C#.
        /// </summary>
        CSharpStringLiteral,

        /// <summary>
        /// Any other xml or json value.
        /// </summary>
        OtherValue,

        /// <summary>
        /// A comment, or the text of a documentation comment.
        /// </summary>
        Comment
    }
}
