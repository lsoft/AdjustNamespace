namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The weights of the score of a match, all in one place.
    ///
    /// They are the starting point and are tuned against the corpus of real examples of
    /// <c>TextReferenceScoringCorpusTests</c>: a change of a weight is checked against every one of
    /// them, and each has to land in its expected level.
    /// </summary>
    public static class ScoreSignals
    {
        /// <summary>
        /// The base signal of the place. A string literal, an xml or json value and a comment
        /// are not equal to each other: a text that is the name as a whole is worth more than
        /// a name inside a sentence.
        /// </summary>
        /// <param name="place">Where the name is found.</param>
        /// <param name="isWholeValue">The text (the literal, the value) is the name as a whole.</param>
        public static ConfidenceSignal Place(TextReferencePlace place, bool isWholeValue)
        {
            switch (place)
            {
                case TextReferencePlace.RazorDirective:
                    return new ConfidenceSignal("Razor directive", 100);

                case TextReferencePlace.ProjectStartupObject:
                    return new ConfidenceSignal("StartupObject of the project", 100);

                case TextReferencePlace.SettingsNamespace:
                    return new ConfidenceSignal("GeneratedClassNamespace of the settings", 100);

                case TextReferencePlace.CustomToolNamespace:
                    return new ConfidenceSignal("CustomToolNamespace of the project item", 100);

                case TextReferencePlace.KnownTypeNameArgument:
                    return new ConfidenceSignal("string passed as a type name", 95);

                case TextReferencePlace.XmlTypeAttribute:
                    return new ConfidenceSignal("xml config type attribute", 85);

                case TextReferencePlace.JsonTypeValue:
                    return new ConfidenceSignal("json type value", 80);

                case TextReferencePlace.CSharpStringLiteral:
                    return isWholeValue
                        ? new ConfidenceSignal("string literal that is the name", 60)
                        : new ConfidenceSignal("name inside a longer string", 30);

                case TextReferencePlace.OtherValue:
                    return isWholeValue
                        ? new ConfidenceSignal("xml / json value that is the name", 50)
                        : new ConfidenceSignal("name inside a longer string", 30);

                case TextReferencePlace.Comment:
                    return new ConfidenceSignal("comment", 40);

                default:
                    return new ConfidenceSignal(place.ToString(), 0);
            }
        }

        /// <summary>
        /// The name is assembly qualified and the assembly is the one of the moved type.
        /// </summary>
        public static ConfidenceSignal AssemblyOfTheMovedType { get; } =
            new ConfidenceSignal("assembly of the moved type", 20);

        /// <summary>
        /// The name is assembly qualified, and the assembly is another one: a type of the same
        /// name elsewhere.
        /// </summary>
        public static ConfidenceSignal AssemblyOfAnotherType { get; } =
            new ConfidenceSignal("assembly of another type", -40);

        /// <summary>
        /// The metadata form (<c>Outer+Inner</c>, <c>C`1</c>) matches the real nesting or arity.
        /// </summary>
        public static ConfidenceSignal MetadataForm { get; } =
            new ConfidenceSignal("metadata form of the type", 10);

        /// <summary>
        /// The literal (the value) is exactly the name, after trimming.
        /// </summary>
        public static ConfidenceSignal ExactName { get; } =
            new ConfidenceSignal("exactly the name", 10);

        /// <summary>
        /// The name stands in prose: letters or spaces around it, not quotes or <c>=</c>.
        /// </summary>
        public static ConfidenceSignal Prose { get; } =
            new ConfidenceSignal("stands in prose", -10);

        /// <summary>
        /// Followed by a file extension or <c>.</c> and a lowercase letter, or a part of a path
        /// or of a URL.
        /// </summary>
        public static ConfidenceSignal LooksLikeAFile { get; } =
            new ConfidenceSignal("looks like a file or a resource", -40);

        /// <summary>
        /// A type or a namespace of that full name exists in another referenced assembly.
        /// </summary>
        public static ConfidenceSignal MeansSomethingElseToo { get; } =
            new ConfidenceSignal("the name means something else too", -30);

        /// <summary>
        /// A file under a folder like <c>TestData</c> or <c>Fixtures</c>, or a value of a
        /// <c>.resx</c> file.
        /// </summary>
        public static ConfidenceSignal TestData { get; } =
            new ConfidenceSignal("test data", -20);

        /// <summary>
        /// The namespace is emptied by the run: never "certain". The weight takes the total
        /// down to <see cref="Confidence.EmptiedNamespaceCap"/>.
        /// </summary>
        public static ConfidenceSignal EmptiedNamespaceCap(int currentTotal)
        {
            return new ConfidenceSignal(
                "namespace is emptied: never certain",
                Confidence.EmptiedNamespaceCap - currentTotal
                );
        }
    }
}
