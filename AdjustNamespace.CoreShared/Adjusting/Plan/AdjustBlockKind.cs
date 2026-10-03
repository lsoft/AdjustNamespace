namespace AdjustNamespace.Adjusting.Plan
{
    /// <summary>
    /// Why a file cannot be adjusted. Benign cases (already in the target namespace,
    /// a xaml with nothing to change) are not represented here: they are dropped silently.
    /// </summary>
    public enum AdjustBlockKind
    {
        /// <summary>
        /// Moving a type of the file would collide with a type of the same name which
        /// already exists (or is about to land) in the target namespace.
        /// </summary>
        TypeNameConflict,

        /// <summary>
        /// The file is not part of the solution tree the host knows about.
        /// </summary>
        NoProject,

        /// <summary>
        /// The target namespace cannot be derived (a linked file outside of the project
        /// folder, for example).
        /// </summary>
        TargetNamespaceUnknown,

        /// <summary>
        /// The file is not a processable C# document of the workspace.
        /// </summary>
        NotAProcessableDocument,

        /// <summary>
        /// Several projects with different project files compile the file (a shared
        /// project referenced by more than one project): there is no single target namespace.
        /// </summary>
        CompiledBySeveralProjects,

        /// <summary>
        /// The projects which compile the file (the target frameworks of a multi target
        /// project) disagree whether the namespace the file leaves stays alive.
        /// </summary>
        NamespaceStateContradictory,

        /// <summary>
        /// The code behind of the xaml is compiled by several projects, so the xaml
        /// itself cannot be moved either.
        /// </summary>
        XamlCodeBehindMultiProject,

        /// <summary>
        /// The namespace the file leaves is imported by a project file
        /// (<c>&lt;Using Include="A.B" /&gt;</c>, which becomes a <c>global using</c> of a
        /// generated file in <c>obj</c>), and the move would leave that namespace empty.
        /// The generated file is written again out of the project file on every build, and
        /// the adjusting does not edit the project files, so the build would break (CS0246).
        /// </summary>
        NamespaceImportedByProjectFile,

        /// <summary>
        /// A type of the file is partial, and another part of it (the designer file of a
        /// Windows Forms form, for example) is not adjusted together with it or would land in
        /// another namespace: the parts would become two different classes.
        /// </summary>
        PartialTypeSplit,
    }
}
