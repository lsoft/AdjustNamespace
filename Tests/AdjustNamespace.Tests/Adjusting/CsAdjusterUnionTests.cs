using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The unions of C# (<c>public union Pet(Cat, Dog);</c>,
    /// https://github.com/dotnet/csharplang/blob/main/proposals/unions.md).
    ///
    /// A union declaration is a type declaration as any other one for the syntax tree
    /// (the compiler builds a <c>StructDeclarationSyntax</c> of the kind
    /// <c>UnionDeclaration</c> out of it), so the adjusting of the declaration itself
    /// needs nothing special. What is new is the list of the case types: it looks like
    /// a parameter list, and every case is a parameter which has a type and no name at all,
    /// which is a position a type reference has never been written at before.
    ///
    /// These tests need the preview features of the language and the runtime types a union
    /// is lowered to, see <see cref="TestSolution.WithUnionSupport"/>.
    /// </summary>
    public class CsAdjusterUnionTests
    {
        /// <summary>
        /// A union is moved into the target namespace and the fully qualified references
        /// to it are rewritten.
        /// </summary>
        [Fact]
        public async Task A_union_is_moved_with_its_references()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Pet.cs",
@"namespace A.B
{
    public class Cat { }

    public class Dog { }

    public union Pet(Cat, Dog);
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.Pet Create() => new A.B.Pet(new A.B.Cat());
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Pet.cs", "X.Y");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Pet.cs"));
            Assert.Equal(2, CountOf(solution.TextOf("MyApp", "Consumer.cs"), "X.Y.Pet"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A file which references a union by its short name gets the using clause
        /// of the target namespace, and the clause of the emptied one disappears.
        /// </summary>
        [Fact]
        public async Task A_short_reference_to_a_union_gets_a_using()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Pet.cs",
@"namespace A.B
{
    public class Cat { }

    public class Dog { }

    public union Pet(Cat, Dog);
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Pet Create() => new Pet(new Cat());
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Pet.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A case type is written inside the case list of the union as a bare type name.
        /// When such a type is moved into another namespace, the file of the union has to
        /// be fixed as any other file which references it.
        /// </summary>
        [Fact]
        public async Task A_case_type_of_a_union_is_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", "Pet.cs",
@"using A.B;

namespace Other
{
    public class Dog { }

    public union Pet(Cat, Dog);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cat.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Pet.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same, but the case type is written with its namespace: such a name is
        /// rewritten in place instead of getting a using clause.
        /// </summary>
        [Fact]
        public async Task A_qualified_case_type_of_a_union_is_rewritten()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", "Pet.cs",
@"namespace Other
{
    public class Dog { }

    public union Pet(A.B.Cat, Dog);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cat.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Pet.cs");

            Assert.Contains("union Pet(X.Y.Cat, Dog)", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A case type of a union of another project is moved as well: the case list is
        /// nothing special for the reference search of Roslyn.
        /// </summary>
        [Fact]
        public async Task A_case_type_of_a_union_of_another_project_is_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddProject("MyApp.Consumers")
                .AddProjectReference("MyApp.Consumers", "MyApp")
                .AddDocument("MyApp", "Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp.Consumers", "Pet.cs",
@"using A.B;

namespace Other
{
    public class Dog { }

    public union Pet(Cat, Dog);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cat.cs", "X.Y");

            var text = solution.TextOf("MyApp.Consumers", "Pet.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union may have a body with its own members, and these members reference
        /// the case types as usual.
        /// </summary>
        [Fact]
        public async Task A_union_with_a_body_is_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Pet.cs",
@"namespace A.B
{
    public class Cat
    {
        public string Name => ""cat"";
    }

    public class Dog
    {
        public string Name => ""dog"";
    }

    public union Pet(Cat, Dog)
    {
        public string GetName()
        {
            return Value switch
            {
                Cat cat => cat.Name,
                Dog dog => dog.Name,
                _ => string.Empty
            };
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public string Describe(Pet pet) => pet.GetName();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Pet.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union may be a generic one, and its case types may be the type parameters
        /// and the generic types built of them.
        /// </summary>
        [Fact]
        public async Task A_generic_union_is_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "OneOrMore.cs",
@"using System.Collections.Generic;

namespace A.B
{
    public union OneOrMore<T>(T, IEnumerable<T>);
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public OneOrMore<string> Create() => new OneOrMore<string>(""x"");
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "OneOrMore.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A case type may be a generic type built of a moved one
        /// (<c>union Option&lt;T&gt;(None, Some&lt;T&gt;)</c>): the reference is written
        /// inside the case list and has a type argument list behind it.
        /// </summary>
        [Fact]
        public async Task A_generic_case_type_of_a_union_is_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cases.cs",
@"namespace A.B
{
    public class None { }

    public class Some<T>
    {
        public Some(T value)
        {
            Value = value;
        }

        public T Value { get; }
    }
}
")
                .AddDocument("MyApp", "Option.cs",
@"using A.B;

namespace Other
{
    public union Option<T>(None, Some<T>);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cases.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Option.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union may be a case of another union.
        /// </summary>
        [Fact]
        public async Task A_union_which_is_a_case_of_another_union_is_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Pet.cs",
@"namespace A.B
{
    public class Cat { }

    public class Dog { }

    public union Pet(Cat, Dog);
}
")
                .AddDocument("MyApp", "Animal.cs",
@"using A.B;

namespace Other
{
    public class Horse { }

    public union Animal(Pet, Horse);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Pet.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Animal.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union inside a file scoped namespace is moved as any other type.
        /// </summary>
        [Fact]
        public async Task A_union_in_a_file_scoped_namespace_is_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Pet.cs",
@"namespace A.B;

public class Cat { }

public class Dog { }

public union Pet(Cat, Dog);
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Pet Create() => new Pet(new Cat());
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Pet.cs", "X.Y");

            Assert.Contains("namespace X.Y;", solution.TextOf("MyApp", "Pet.cs"));

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union nested into a class follows its outer type and is not moved on its own.
        /// </summary>
        [Fact]
        public async Task A_union_nested_into_a_class_follows_its_outer_type()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Container.cs",
@"namespace A.B
{
    public class Cat { }

    public class Dog { }

    public class Container
    {
        public union Pet(Cat, Dog);
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.Container.Pet Create() => new A.B.Container.Pet(new A.B.Cat());
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Container.cs", "X.Y");

            Assert.Equal(2, CountOf(solution.TextOf("MyApp", "Consumer.cs"), "X.Y.Container.Pet"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union may be declared as a partial one, and both of its parts are moved
        /// together with the file they are written in. Only one of the parts carries
        /// the case list (CS8863), so the other one is a declaration without any.
        /// </summary>
        [Fact]
        public async Task Both_parts_of_a_partial_union_are_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Pet.cs",
@"namespace A.B
{
    public class Cat { }

    public class Dog { }

    public partial union Pet(Cat, Dog);
}
")
                .AddDocument("MyApp", "Pet.Extra.cs",
@"namespace A.B
{
    public partial union Pet
    {
        public bool IsCat => Value is Cat;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public bool Check(Pet pet) => pet.IsCat;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var namespaceCenter = await AdjustAsync(solution, "MyApp", "Pet.cs", "X.Y");
            await AdjustAsync(solution, namespaceCenter, "MyApp", "Pet.Extra.cs", "X.Y");
            await CleanupAsync(solution, namespaceCenter);

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Pet.cs"));
            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Pet.Extra.cs"));

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A case type may be written as a nullable one: the case is the underlying type
        /// then, and the reference stands inside the nullable type syntax and not directly
        /// inside the case list.
        /// </summary>
        [Fact]
        public async Task A_nullable_case_type_of_a_union_is_moved()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Size.cs",
@"namespace A.B
{
    public struct Size
    {
        public int Width;
    }
}
")
                .AddDocument("MyApp", "Shape.cs",
@"using A.B;

namespace Other
{
    public class Empty { }

    public union Shape(Size?, Empty);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Size.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Shape.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A case type written with the <c>global::</c> alias keeps that alias when it is moved.
        /// </summary>
        [Fact]
        public async Task A_global_qualified_case_type_of_a_union_keeps_its_prefix()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", "Pet.cs",
@"namespace Other
{
    public class Dog { }

    public union Pet(global::A.B.Cat, Dog);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cat.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Pet.cs");

            Assert.Contains("union Pet(global::X.Y.Cat, Dog)", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union may declare the static members, and such a member is accessed through
        /// the name of the union, see <c>CsAdjusterMemberAccessTests</c>.
        /// </summary>
        [Fact]
        public async Task A_static_member_of_a_union_is_accessed_through_the_new_namespace()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Pet.cs",
@"namespace A.B
{
    public class Cat { }

    public class Dog { }

    public union Pet(Cat, Dog)
    {
        public static Pet Default => new Pet(new Cat());
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.Pet Create() => A.B.Pet.Default;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Pet.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Pet.Default", text);
            Assert.DoesNotContain("A.B.Pet", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union is a type of its namespace: the namespace of a file which declares
        /// nothing but a union is emptied by the move, and the using clauses of it are
        /// removed by the cleanup.
        /// </summary>
        [Fact]
        public async Task A_namespace_of_a_union_only_is_emptied()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cases.cs",
@"namespace Cases
{
    public class Cat { }

    public class Dog { }
}
")
                .AddDocument("MyApp", "Pet.cs",
@"using Cases;

namespace A.B
{
    public union Pet(Cat, Dog);
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;
using Cases;

namespace Other
{
    public class Consumer
    {
        public Pet Create() => new Pet(new Cat());
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Pet.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            //the namespace of the case types is not touched at all
            Assert.Contains("using Cases;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A union is visible for the conflict detection of the second wizard step:
        /// a file must not be moved into a namespace which contains a union of that name.
        /// </summary>
        [Fact]
        public async Task A_union_is_a_type_of_the_conflict_check()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Pet.cs",
@"namespace X.Y
{
    public class Cat { }

    public class Dog { }

    public union Pet(Cat, Dog);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.True(container.CheckForTypeExists("X.Y", "Pet"));
            Assert.False(container.CheckForTypeExists("X.Y", "Elephant"));
        }

        /// <summary>
        /// The target namespace ends with the name of the moved case type
        /// (<c>Cat</c> goes into <c>X.Cat</c>), so its short name inside the case list
        /// resolves to that namespace and not to the type anymore: a using clause does not
        /// help here and the name has to be qualified,
        /// see <c>CsAdjusterNamespaceNameCollisionTests</c>.
        /// </summary>
        [Fact]
        public async Task A_case_type_shadowed_by_the_target_namespace_is_qualified()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", "Pet.cs",
@"using A.B;

namespace X
{
    public class Dog { }

    public union Pet(Cat, Dog);
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cat.cs", "X.Cat");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
