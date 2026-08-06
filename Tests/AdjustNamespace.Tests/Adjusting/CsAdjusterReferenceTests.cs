using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of the reference processing of <see cref="AdjustNamespace.Adjusting.CsAdjuster"/>:
    /// every kind of the syntax which may reference a moved type
    /// (see <see cref="AdjustNamespace.Adjusting.Adjuster.Cs.RefProcessor"/>).
    /// </summary>
    public class CsAdjusterReferenceTests
    {
        [Fact]
        public async Task A_generic_type_is_rewritten_in_a_qualified_reference()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1<T> { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.Class1<int> Create() => new A.B.Class1<int>();
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("A.B.Class1", text);
            Assert.Equal(2, CountOf(text, "X.Y.Class1<int>"));
        }

        [Fact]
        public async Task A_reference_in_a_type_constraint_is_processed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer<T>
        where T : Class1
    {
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
        }

        [Fact]
        public async Task A_reference_in_a_base_type_list_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer : A.B.Class1
    {
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("Consumer : X.Y.Class1", text);
        }

        /// <summary>
        /// <c>global::A.B.Class1</c> has to keep its <c>global::</c> prefix,
        /// otherwise the reference may resolve to another type.
        /// </summary>
        [Fact]
        public async Task A_global_qualified_reference_keeps_its_prefix()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public global::A.B.Class1 Create() => new global::A.B.Class1();
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("A.B.Class1", text);
            Assert.Equal(2, CountOf(text, "global::X.Y.Class1"));
        }

        [Fact]
        public async Task A_usage_of_a_moved_attribute_is_processed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyAttribute.cs",
@"namespace A.B
{
    public class MyAttribute : System.Attribute { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    [A.B.My]
    public class Consumer
    {
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "MyAttribute.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("A.B.My", text);
            Assert.Contains("X.Y.My", text);
        }

        [Fact]
        public async Task A_qualified_access_to_a_static_member_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Consts.cs",
@"namespace A.B
{
    public static class Consts
    {
        public const string Title = ""title"";
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public string Get() => A.B.Consts.Title;
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Consts.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Consts.Title", text);
        }

        /// <summary>
        /// Every reference of a file is scheduled as a separate
        /// <see cref="AdjustNamespace.Adjusting.Edit.ReplaceTextEdit"/> and all of them
        /// are applied at once, so a file with several references has to be fixed completely.
        /// </summary>
        [Fact]
        public async Task Several_qualified_references_of_one_file_are_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Classes.cs",
@"namespace A.B
{
    public class Class1 { }

    public class Class2 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.Class1 Create1() => new A.B.Class1();

        public A.B.Class2 Create2() => new A.B.Class2();
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Classes.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("A.B.Class", text);
            Assert.Equal(2, CountOf(text, "X.Y.Class1"));
            Assert.Equal(2, CountOf(text, "X.Y.Class2"));
        }

        [Fact]
        public async Task An_enum_is_moved_with_its_references()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyEnum.cs",
@"namespace A.B
{
    public enum MyEnum
    {
        First
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.MyEnum Get() => A.B.MyEnum.First;
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "MyEnum.cs", "X.Y");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "MyEnum.cs"));

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("A.B.MyEnum", text);
            Assert.Contains("X.Y.MyEnum", text);
        }

        [Fact]
        public async Task A_delegate_is_moved_with_its_references()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyDelegate.cs",
@"namespace A.B
{
    public delegate void MyDelegate(int value);
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.MyDelegate? Handler;
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "MyDelegate.cs", "X.Y");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "MyDelegate.cs"));

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.MyDelegate", text);
        }

        [Fact]
        public async Task An_interface_is_moved_with_its_implementation()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "IContract.cs",
@"namespace A.B
{
    public interface IContract { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer : A.B.IContract
    {
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "IContract.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("Consumer : X.Y.IContract", text);
        }

        /// <summary>
        /// A file may declare several namespaces at once; every one of them is moved
        /// into the target namespace of that file.
        /// </summary>
        [Fact]
        public async Task Every_namespace_of_the_adjusted_file_is_moved()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Classes.cs",
@"namespace A.B
{
    public class Class1 { }
}

namespace Q.W
{
    public class Class2 { }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Classes.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Classes.cs");

            Assert.DoesNotContain("namespace A.B", text);
            Assert.DoesNotContain("namespace Q.W", text);
            Assert.Equal(2, CountOf(text, "namespace X.Y"));
        }

        /// <summary>
        /// The parts of a partial class live in different files, and the user adjusts
        /// every one of them. The class must not be torn apart in the meantime:
        /// after both files are processed both parts are in the target namespace.
        /// </summary>
        [Fact]
        public async Task Both_parts_of_a_partial_class_are_moved()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public partial class Class1
    {
        public int First;
    }
}
")
                .AddDocument("MyApp", "Class1.Other.cs",
@"namespace A.B
{
    public partial class Class1
    {
        public int Second;
    }
}
")
                ;

            var namespaceCenter = await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");
            await AdjustAsync(solution, namespaceCenter, "MyApp", "Class1.Other.cs", "X.Y");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Class1.cs"));
            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Class1.Other.cs"));
        }

        /// <summary>
        /// <c>using C = A.B.Class1;</c> names the type itself, so the namespace inside
        /// that clause has to be rewritten.
        /// </summary>
        [Fact]
        public async Task An_alias_using_of_the_moved_type_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using C = A.B.Class1;

namespace Other
{
    public class Consumer
    {
        public C Create() => new C();
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using C = X.Y.Class1;", text);
        }

        /// <summary>
        /// <c>using static A.B.Consts;</c> names the type itself as well: adding
        /// <c>using X.Y;</c> does not help, the clause has to point to the new place.
        /// </summary>
        [Fact]
        public async Task A_using_static_of_the_moved_type_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Consts.cs",
@"namespace A.B
{
    public static class Consts
    {
        public const string Title = ""title"";
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using static A.B.Consts;

namespace Other
{
    public class Consumer
    {
        public string Get() => Title;
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Consts.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using static X.Y.Consts;", text);
        }

        /// <summary>
        /// A namespace which merely starts with the target one is a different namespace
        /// and has to be moved as usual.
        /// </summary>
        [Fact]
        public async Task A_namespace_which_starts_with_the_target_one_is_moved()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace X.Yzz
{
    public class Class1 { }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.DoesNotContain("namespace X.Yzz", text);
            Assert.Contains("namespace X.Y", text);
        }

        /// <summary>
        /// The code behind class of a window is referenced by the <c>x:Class</c> attribute
        /// of its xaml, which has to follow the class into the target namespace.
        /// </summary>
        [Fact]
        public async Task The_x_Class_of_the_xaml_follows_its_code_behind()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MainWindow.xaml.cs",
@"namespace A.B
{
    public partial class MainWindow
    {
    }
}
")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml",
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            await AdjustAsync(solution, "MyApp", "MainWindow.xaml.cs", "X.Y", xamlFilePath);

            Assert.Contains(@"x:Class=""X.Y.MainWindow""", solution.XamlTextOf("MyApp", "MainWindow.xaml"));
        }

        /// <summary>
        /// The xaml files of the whole solution are processed, not only those of the project
        /// which declares the moved type.
        /// </summary>
        [Fact]
        public async Task A_xaml_of_another_project_is_updated()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddProject("MyApp.Ui")
                .AddProjectReference("MyApp.Ui", "MyApp")
                .AddDocument("MyApp", "MyButton.cs",
@"namespace A.B
{
    public class MyButton { }
}
")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp.Ui", "MainWindow.xaml",
@"<Window x:Class=""Q.W.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B;assembly=MyApp"">
    <local:MyButton />
</Window>");

            await AdjustAsync(solution, "MyApp", "MyButton.cs", "X.Y", xamlFilePath);

            var xaml = solution.XamlTextOf("MyApp.Ui", "MainWindow.xaml");

            Assert.Contains("clr-namespace:X.Y;assembly=MyApp", xaml);
            Assert.DoesNotContain("<local:MyButton", xaml);
        }

        /// <summary>
        /// A type of the same namespace which lives in another (not adjusted) file
        /// keeps that namespace, so the using clause of it must not be removed
        /// from the consumers.
        /// </summary>
        [Fact]
        public async Task A_namespace_which_is_not_emptied_is_kept_in_the_usings()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Class2.cs",
@"namespace A.B
{
    public class Class2 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Class1 Create1() => new Class1();

        public Class2 Create2() => new Class2();
    }
}
")
                ;

            var namespaceCenter = await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");
            await CleanupAsync(solution, namespaceCenter);

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using A.B;", text);
            Assert.Contains("using X.Y;", text);
        }
    }
}
