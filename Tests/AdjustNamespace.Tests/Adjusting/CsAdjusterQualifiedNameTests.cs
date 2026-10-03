using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The full (<c>First.Second.Third.MyClass</c>) and the partially qualified names
    /// (<c>Second.Third.MyClass</c>, <c>Third.MyClass</c>) a C# file may use. A partial name
    /// is resolved from the namespace the file is written in, so it breaks both when the
    /// type it names moves and when the file which writes it moves; a full name breaks
    /// when the namespace the file moves into hides its head.
    /// </summary>
    public class CsAdjusterQualifiedNameTests
    {
        private const string MyClassFile =
@"namespace First.Second.Third
{
    public class MyClass
    {
        public static int Value => 1;

        public class Nested { }
    }
}
";

        #region The type moves, the file which names it stays

        /// <summary>
        /// Every form of the name, in every place a type name is written in.
        /// </summary>
        [Theory]
        [InlineData("Other", "First.Second.Third.MyClass")]
        [InlineData("Other", "global::First.Second.Third.MyClass")]
        [InlineData("First", "Second.Third.MyClass")]
        [InlineData("First.Other", "Second.Third.MyClass")]
        [InlineData("First.Second", "Third.MyClass")]
        [InlineData("First.Second.Other", "Third.MyClass")]
        [InlineData("First.Second.Other.Deeper", "Third.MyClass")]
        public async Task A_name_of_the_moved_type_is_rewritten_in_every_place(string consumerNamespace, string name)
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "Consumer.cs", ConsumerOf(consumerNamespace, name))
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// The consumer is written in nested namespace declarations: <c>Second.Third.MyClass</c>
        /// is resolved through the outer one.
        /// </summary>
        [Fact]
        public async Task A_partial_name_inside_nested_namespace_declarations_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "Consumer.cs",
@"namespace First
{
    namespace Other
    {
        public class Consumer
        {
            public Second.Third.MyClass Field = new Second.Third.MyClass();
        }
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// The head of the target namespace is a type of the consumer's namespace:
        /// <c>Target.Place.MyClass</c> would start at the class <c>Other.Target</c>.
        /// </summary>
        [Fact]
        public async Task The_new_name_is_not_hidden_by_a_type_named_as_its_head()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Target { }

    public class Consumer
    {
        public First.Second.Third.MyClass Field = new First.Second.Third.MyClass();
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// The target is <c>Second.Place</c>, and the consumer lives in <c>First</c>, where
        /// <c>Second</c> means <c>First.Second</c>.
        /// </summary>
        [Fact]
        public async Task The_new_name_is_not_hidden_by_a_namespace_named_as_its_head()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "Keeper.cs",
@"namespace First.Second
{
    public class Keeper { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace First
{
    public class Consumer
    {
        public Second.Third.MyClass Field = new Second.Third.MyClass();
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Second.Place");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// The target namespace is a child of the old one, and the consumer names the type
        /// relative to a common ancestor.
        /// </summary>
        [Fact]
        public async Task A_partial_name_follows_a_move_into_a_child_namespace()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "Consumer.cs", ConsumerOf("First.Second", "Third.MyClass"))
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "First.Second.Third.Inner");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// Was red: the new using clause of the consumer brings a second <c>Helper</c> into the
        /// file, the target namespace has one, and so has a namespace the file imports
        /// (CS0104). The reference to the moved type is qualified instead.
        /// </summary>
        [Fact]
        public async Task The_new_using_does_not_make_another_name_ambiguous()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "TargetHelper.cs",
@"namespace Target.Place
{
    public class Helper { }
}
")
                .AddDocument("MyApp", "Lib.cs",
@"namespace Lib
{
    public class Helper
    {
        public void OnlyLib() { }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using First.Second.Third;
using Lib;

namespace Other
{
    public class Consumer
    {
        public MyClass Field = new MyClass();

        public void Use(Helper helper) => helper.OnlyLib();
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("using Target.Place;", text);
            Assert.Contains("public Target.Place.MyClass Field = new Target.Place.MyClass();", text);
            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// A documentation reference written with a partial name.
        /// </summary>
        [Fact]
        public async Task A_partial_name_in_a_cref_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "Consumer.cs",
@"namespace First.Second
{
    /// <summary>
    /// See <see cref=""Third.MyClass""/>.
    /// </summary>
    public class Consumer
    {
    }
}
")
                ;

            Assert.Empty(await solution.UnresolvedCrefsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            await AssertCompilesAsync(solution);
            Assert.Empty(await solution.UnresolvedCrefsAsync());
        }

        /// <summary>
        /// An attribute written with a partial name and without its <c>Attribute</c> suffix.
        /// </summary>
        [Fact]
        public async Task A_partial_name_of_an_attribute_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MarkerAttribute.cs",
@"namespace First.Second.Third
{
    public class MarkerAttribute : System.Attribute { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace First.Second
{
    [Third.Marker]
    public class Consumer
    {
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MarkerAttribute.cs", "Target.Place");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// <c>global using static</c> of the moved type, in a file of its own.
        /// </summary>
        [Fact]
        public async Task A_global_using_static_of_the_moved_type_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "GlobalUsings.cs",
@"global using static First.Second.Third.MyClass;
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public int Get() => Value;
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// Was red: <c>nameof</c> of the emptied namespace itself (CS0103). It is replaced with
        /// the string it gives: rewriting it to the new namespace would change the string the
        /// program sees, and the types of the namespace may have gone into several ones.
        /// </summary>
        [Fact]
        public async Task A_nameof_of_the_emptied_namespace_does_not_break()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public string Name => nameof(First.Second.Third);
    }
}
")
                .AddDocument("MyApp", "Keeper.cs",
@"namespace First.Second
{
    public class Keeper
    {
        public string Name => nameof(Third);

        public string Relative => nameof(Second.Third);
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            Assert.Contains(@"public string Name => ""Third"";", solution.TextOf("MyApp", "Keeper.cs"));
            Assert.Contains(@"public string Relative => ""Third"";", solution.TextOf("MyApp", "Keeper.cs"));
            Assert.Contains(@"public string Name => ""Third"";", solution.TextOf("MyApp", "Consumer.cs"));
            await AssertCompilesAsync(solution);
        }

        #endregion

        #region The file which names a type moves

        /// <summary>
        /// The moved file names a type of a sibling namespace relative to its old
        /// enclosing namespaces.
        /// </summary>
        [Theory]
        [InlineData("Fourth.Thing")]
        [InlineData("Second.Fourth.Thing")]
        [InlineData("First.Second.Fourth.Thing")]
        [InlineData("Fourth.Deep.Thing2")]
        public async Task A_name_relative_to_the_old_namespace_is_kept_resolvable(string name)
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Thing.cs",
@"namespace First.Second.Fourth
{
    public class Thing
    {
        public static int Value => 1;
    }
}

namespace First.Second.Fourth.Deep
{
    public class Thing2
    {
        public static int Value => 1;
    }
}
")
                .AddDocument("MyApp", "MyClass.cs",
$@"namespace First.Second.Third
{{
    public class MyClass
    {{
        public {name} Field = new {name}();

        public int Get() => {name}.Value;
    }}
}}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// The moved file names its own type relative to an enclosing namespace.
        /// </summary>
        [Theory]
        [InlineData("Third.MyClass")]
        [InlineData("Second.Third.MyClass")]
        [InlineData("First.Second.Third.MyClass")]
        public async Task A_partial_name_of_a_type_of_the_moved_file_itself_is_rewritten(string name)
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs",
$@"namespace First.Second.Third
{{
    public class MyClass
    {{
        public {name} Self;
    }}
}}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// A using clause inside the namespace declaration is resolved relative to that
        /// namespace (<c>Fourth.Thing</c> is <c>First.Second.Fourth.Thing</c> there). A clause
        /// which names a type is a reference to that type and is rewritten as such.
        /// </summary>
        [Theory]
        [InlineData("using static Fourth.Thing;", "public int Get() => Value;")]
        [InlineData("using Alias = Fourth.Thing;", "public Alias Field;")]
        public async Task A_relative_using_of_a_type_inside_the_namespace_is_kept_resolvable(string usingClause, string member)
        {
            await MoveAFileWithAUsingInsideItsNamespaceAsync(usingClause, member);
        }

        /// <summary>
        /// Was red: a using clause inside the namespace declaration which names a namespace
        /// (<c>using Fourth;</c>, <c>using Alias = Fourth;</c>) relative to the old enclosing
        /// namespace is left as it is, and does not resolve in the new namespace
        /// (CS0246). Only the references to the types are processed, and a namespace is none.
        /// </summary>
        [Theory]
        [InlineData("using Fourth;", "public Thing Field;")]
        [InlineData("using Alias = Fourth;", "public Alias.Thing Field;")]
        public async Task A_relative_using_of_a_namespace_inside_the_namespace_is_kept_resolvable(string usingClause, string member)
        {
            await MoveAFileWithAUsingInsideItsNamespaceAsync(usingClause, member);
        }

        private static async Task MoveAFileWithAUsingInsideItsNamespaceAsync(string usingClause, string member)
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Thing.cs",
@"namespace First.Second.Fourth
{
    public class Thing
    {
        public static int Value => 1;
    }
}
")
                .AddDocument("MyApp", "MyClass.cs",
$@"namespace First.Second.Third
{{
    {usingClause}

    public class MyClass
    {{
        {member}
    }}
}}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            Assert.DoesNotContain(usingClause, solution.TextOf("MyApp", "MyClass.cs"));
            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// Was red: the same with a file scoped namespace, the using clauses behind it belong to it.
        /// </summary>
        [Fact]
        public async Task A_relative_using_behind_a_file_scoped_namespace_is_kept_resolvable()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Thing.cs",
@"namespace First.Second.Fourth
{
    public class Thing { }
}
")
                .AddDocument("MyApp", "MyClass.cs",
@"namespace First.Second.Third;

using Fourth;

public class MyClass
{
    public Thing Field;
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            Assert.Contains("using First.Second.Fourth;", solution.TextOf("MyApp", "MyClass.cs"));
            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// Was red: the file moves into <c>Target.First</c>, and its full name
        /// <c>First.Second.Fourth.Thing</c> starts at <c>Target.First</c> there (CS0234).
        /// The file's own names are not checked for being hidden by the new namespace; the
        /// names rewritten in the other files are (<c>RefProcessor.IsGlobalPrefixRequired</c>).
        /// </summary>
        [Fact]
        public async Task A_full_name_is_not_hidden_by_the_new_namespace_of_the_file()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Thing.cs",
@"namespace First.Second.Fourth
{
    public class Thing { }
}
")
                .AddDocument("MyApp", "MyClass.cs",
@"namespace Other
{
    public class MyClass
    {
        public First.Second.Fourth.Thing Field;
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.First");

            Assert.Contains("public global::First.Second.Fourth.Thing Field;", solution.TextOf("MyApp", "MyClass.cs"));
            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// Was red: before the move <c>Logger</c> is the one of the enclosing <c>First.Second</c>,
        /// which wins over the imported <c>Lib.Logger</c>. After the move both of them are
        /// imported at the same level, and the name is ambiguous (CS0104).
        /// </summary>
        [Fact]
        public async Task A_name_of_the_old_enclosing_namespace_does_not_become_ambiguous()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Logger.cs",
@"namespace First.Second
{
    public class Logger
    {
        public void Write() { }
    }
}
")
                .AddDocument("MyApp", "Lib.cs",
@"namespace Lib
{
    public class Logger { }

    public class Util { }
}
")
                .AddDocument("MyApp", "MyClass.cs",
@"using Lib;

namespace First.Second.Third
{
    public class MyClass
    {
        public Util Util;

        public void Use(Logger logger) => logger.Write();
    }
}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            var text = solution.TextOf("MyApp", "MyClass.cs");

            Assert.Contains("public void Use(First.Second.Logger logger)", text);
            Assert.Contains("public Util Util;", text);
            await AssertCompilesAsync(solution);
        }

        /// <summary>
        /// Was red: the target namespace has a <c>Logger</c> of its own, which wins over the
        /// imported one and over the one of the old enclosing namespace: the name silently
        /// means another type. <c>Write</c> exists in the original type only, so here the
        /// rebinding does not compile (CS1061); with the same members it would compile and
        /// call another code.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task A_name_does_not_silently_mean_a_type_of_the_target_namespace(bool imported)
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Logger.cs",
$@"namespace {(imported ? "Lib" : "First.Second")}
{{
    public class Logger
    {{
        public void Write() {{ }}
    }}
}}
")
                .AddDocument("MyApp", "TargetLogger.cs",
@"namespace Target.Place
{
    public class Logger { }
}
")
                .AddDocument("MyApp", "MyClass.cs",
$@"{(imported ? "using Lib;" : string.Empty)}

namespace First.Second.Third
{{
    public class MyClass
    {{
        public void Use(Logger logger) => logger.Write();
    }}
}}
")
                ;

            await AssertCompilesAsync(solution);

            await AdjustAndCleanupAsync(solution, "MyApp", "MyClass.cs", "Target.Place");

            Assert.Contains(
                $"public void Use({(imported ? "Lib" : "First.Second")}.Logger logger)",
                solution.TextOf("MyApp", "MyClass.cs")
                );
            await AssertCompilesAsync(solution);
        }

        #endregion

        #region Both of them move

        /// <summary>
        /// The type and the file which names it relative to their common ancestor move into
        /// different namespaces in one session.
        /// </summary>
        [Fact]
        public async Task A_partial_name_survives_when_both_files_move()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyClass.cs", MyClassFile)
                .AddDocument("MyApp", "Consumer.cs", ConsumerOf("First.Second.Other", "Third.MyClass"))
                ;

            await AssertCompilesAsync(solution);

            var namespaceCenter = await NamespaceCenter.CreateForAsync(solution.Workspace);
            await AdjustAsync(solution, namespaceCenter, "MyApp", "MyClass.cs", "Target.A");
            await AdjustAsync(solution, namespaceCenter, "MyApp", "Consumer.cs", "Target.B");
            await CleanupAsync(solution, namespaceCenter);

            await AssertCompilesAsync(solution);
        }

        #endregion

        /// <summary>
        /// The solution compiles; the errors are the message otherwise.
        /// </summary>
        private static async Task AssertCompilesAsync(TestSolution solution)
        {
            var errors = await solution.CompilationErrorsAsync();

            Assert.True(errors.Count == 0, string.Join(System.Environment.NewLine, errors));
        }

        /// <summary>
        /// A consumer which writes the given name of <c>MyClass</c> everywhere a type name
        /// may be written.
        /// </summary>
        private static string ConsumerOf(string @namespace, string name)
        {
            return
$@"namespace {@namespace}
{{
    public class Consumer
    {{
        public {name} Field = new {name}();

        public System.Collections.Generic.List<{name}> List;

        public {name}[] Array;

        public {name}.Nested NestedField;

        public object Cast(object o) => ({name})o;

        public bool Is(object o) => o is {name};

        public System.Type Type => typeof({name});

        public string Name => nameof({name});

        public {name} Default => default({name});

        public int Value => {name}.Value;
    }}
}}
";
        }
    }
}
