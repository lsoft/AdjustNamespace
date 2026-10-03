using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The extension members of C# 14 (<c>extension(Cat cat) { ... }</c> blocks inside a
    /// static class, https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md).
    ///
    /// The extension indexers of C# 15 (<c>cat[0]</c>) are a preview feature and need the
    /// Roslyn 5.9 or newer.
    ///
    /// A call of an extension member writes neither the name of its static class nor the
    /// namespace of it: <c>cat.Loud</c>, <c>Cat.Create()</c>, <c>a + b</c>, <c>cat[0]</c>.
    /// The member is found only because a <c>using</c> clause (or an enclosing namespace)
    /// imports the static class. So when the static class moves, every file which calls
    /// one of its members needs the using clause of the target namespace, although the
    /// reference search of the class itself reports nothing in such a file. The classic
    /// extension methods (<c>this Cat cat</c>) have the very same problem, and
    /// <c>RefProcessor.FindReferencesForAsync</c> queries them one by one; see
    /// <c>CsAdjusterTests</c> for the classic case.
    /// </summary>
    public class CsAdjusterExtensionMemberTests
    {
        private const string CatBody =
@"namespace Animals
{
    public class Cat
    {
        public string Name => ""cat"";
    }
}
";

        /// <summary>
        /// The control case: a classic extension method of a moved static class.
        /// </summary>
        [Fact]
        public async Task A_classic_extension_method_of_a_moved_class_is_imported()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs", CatBody)
                .AddDocument("MyApp", "CatExtensions.cs",
@"using Animals;

namespace A.B
{
    public static class CatExtensions
    {
        public static string Shout(this Cat cat) => cat.Name.ToUpper();
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;
using Animals;

namespace Other
{
    public class Consumer
    {
        public string Call(Cat cat) => cat.Shout();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "CatExtensions.cs", "X.Y");

            AssertImportsTarget(solution, "Consumer.cs");
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// An instance extension method declared in an extension block.
        /// </summary>
        [Fact]
        public async Task An_extension_block_method_of_a_moved_class_is_imported()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs", CatBody)
                .AddDocument("MyApp", "CatExtensions.cs",
@"using Animals;

namespace A.B
{
    public static class CatExtensions
    {
        extension(Cat cat)
        {
            public string Shout() => cat.Name.ToUpper();
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;
using Animals;

namespace Other
{
    public class Consumer
    {
        public string Call(Cat cat) => cat.Shout();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "CatExtensions.cs", "X.Y");

            AssertImportsTarget(solution, "Consumer.cs");
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// An extension property: <c>cat.Loud</c> is a member access as an extension method
        /// call is, but the member is a property.
        ///
        /// Unlike an instance method of a block, a property has no classic extension method
        /// behind it, so <c>RefProcessor.FindReferencesForAsync</c> queries the members of
        /// the extension blocks of a moved static class one by one.
        /// </summary>
        [Fact]
        public async Task An_extension_property_of_a_moved_class_is_imported()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs", CatBody)
                .AddDocument("MyApp", "CatExtensions.cs",
@"using Animals;

namespace A.B
{
    public static class CatExtensions
    {
        extension(Cat cat)
        {
            public string Loud => cat.Name.ToUpper();
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;
using Animals;

namespace Other
{
    public class Consumer
    {
        public string Call(Cat cat) => cat.Loud;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "CatExtensions.cs", "X.Y");

            AssertImportsTarget(solution, "Consumer.cs");
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A static extension member is called through the name of the extended type
        /// (<c>Cat.Create()</c>), which has nothing to do with the moved static class.
        ///
        /// A static extension member has no classic extension method behind it either, see
        /// <see cref="An_extension_property_of_a_moved_class_is_imported"/>.
        /// </summary>
        [Fact]
        public async Task A_static_extension_member_of_a_moved_class_is_imported()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs", CatBody)
                .AddDocument("MyApp", "CatExtensions.cs",
@"using Animals;

namespace A.B
{
    public static class CatExtensions
    {
        extension(Cat)
        {
            public static Cat Create() => new Cat();
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;
using Animals;

namespace Other
{
    public class Consumer
    {
        public Cat Call() => Cat.Create();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "CatExtensions.cs", "X.Y");

            AssertImportsTarget(solution, "Consumer.cs");
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// An extension operator: its use (<c>left + right</c>) contains no name at all.
        ///
        /// The operator is queried as a member of its block, see
        /// <see cref="An_extension_property_of_a_moved_class_is_imported"/>, and its reference
        /// is an operator token and not a name: the using clause is added with no name to
        /// rewrite.
        /// </summary>
        [Fact]
        public async Task An_extension_operator_of_a_moved_class_is_imported()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs", CatBody)
                .AddDocument("MyApp", "CatExtensions.cs",
@"using Animals;

namespace A.B
{
    public static class CatExtensions
    {
        extension(Cat)
        {
            public static Cat operator +(Cat left, Cat right) => left;
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;
using Animals;

namespace Other
{
    public class Consumer
    {
        public Cat Call(Cat left, Cat right) => left + right;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "CatExtensions.cs", "X.Y");

            AssertImportsTarget(solution, "Consumer.cs");
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// An extension indexer of C# 15 (a preview feature, enabled here by
        /// <see cref="TestSolution.WithUnionSupport"/> which switches the language version).
        ///
        /// An indexer is a property for the symbol model, see
        /// <see cref="An_extension_property_of_a_moved_class_is_imported"/>, and Roslyn
        /// reports its use as an empty span in front of the argument list, which has no
        /// symbol of its own: <c>RefProcessor</c> goes up to the element access.
        /// </summary>
        [Fact]
        public async Task An_extension_indexer_of_a_moved_class_is_imported()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs", CatBody)
                .AddDocument("MyApp", "CatExtensions.cs",
@"using Animals;

namespace A.B
{
    public static class CatExtensions
    {
        extension(Cat cat)
        {
            public char this[int index] => cat.Name[index];
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;
using Animals;

namespace Other
{
    public class Consumer
    {
        public char Call(Cat cat) => cat[0];
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "CatExtensions.cs", "X.Y");

            AssertImportsTarget(solution, "Consumer.cs");
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The extended type itself moves, and the receiver of the extension block has no
        /// name (<c>extension(Cat)</c>): the type reference is the whole receiver parameter,
        /// the same tie the case list of a union has.
        /// </summary>
        [Fact]
        public async Task A_moved_receiver_type_of_an_extension_block_is_fixed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", "CatExtensions.cs",
@"using A.B;

namespace Other
{
    public static class CatExtensions
    {
        extension(Cat)
        {
            public static Cat Create() => new Cat();
        }

        extension<T>(T pet) where T : Cat
        {
            public T Self => pet;
        }
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cat.cs", "X.Y");

            AssertImportsTarget(solution, "CatExtensions.cs");
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The adjusted file itself calls an extension property and a static extension member
        /// of its old enclosing namespace, see <c>SelfReferenceFixer</c>: once the file leaves
        /// that namespace, it needs a using clause of it.
        /// </summary>
        [Fact]
        public async Task The_adjusted_file_keeps_seeing_the_extension_members_of_its_old_enclosing_namespace()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs", CatBody)
                .AddDocument("MyApp", "CatExtensions.cs",
@"using Animals;

namespace A.B
{
    public static class CatExtensions
    {
        extension(Cat cat)
        {
            public string Loud => cat.Name.ToUpper();

            public static Cat Create() => new Cat();
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using Animals;

namespace A.B.Inner
{
    public class Consumer
    {
        public string Call() => Cat.Create().Loud;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Consumer.cs", "X.Y");

            Assert.Contains("using A.B;", solution.TextOf("MyApp", "Consumer.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same for an extension operator and an extension indexer, which are written
        /// with no name at all (<c>left + right</c>, <c>cat[0]</c>).
        /// </summary>
        [Fact]
        public async Task The_adjusted_file_keeps_seeing_the_extension_operators_and_indexers_of_its_old_enclosing_namespace()
        {
            using var solution = new TestSolution()
                .WithUnionSupport()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs", CatBody)
                .AddDocument("MyApp", "CatExtensions.cs",
@"using Animals;

namespace A.B
{
    public static class CatExtensions
    {
        extension(Cat cat)
        {
            public char this[int index] => cat.Name[index];
        }

        extension(Cat)
        {
            public static Cat operator +(Cat left, Cat right) => left;
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using Animals;

namespace A.B.Inner
{
    public class Consumer
    {
        public Cat Sum(Cat left, Cat right) => left + right;

        public char First(Cat cat) => cat[0];
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Consumer.cs", "X.Y");

            Assert.Contains("using A.B;", solution.TextOf("MyApp", "Consumer.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// An operator and an indexer of a type itself need no import, so the adjusted file
        /// gets no using clause for them.
        /// </summary>
        [Fact]
        public async Task An_operator_of_a_type_itself_adds_no_using()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Money.cs",
@"namespace A.B
{
    public class Money
    {
        public static Money operator +(Money left, Money right) => left;

        public int this[int index] => index;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace A.B.Inner
{
    public class Consumer
    {
        public object Sum(global::A.B.Money left, global::A.B.Money right) => left + right;

        public int First(global::A.B.Money money) => money[0];
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Consumer.cs", "X.Y");

            Assert.DoesNotContain("using A.B;", solution.TextOf("MyApp", "Consumer.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        private static void AssertImportsTarget(
            TestSolution solution,
            string relativeFilePath
            )
        {
            var text = solution.TextOf("MyApp", relativeFilePath);

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
        }
    }
}
