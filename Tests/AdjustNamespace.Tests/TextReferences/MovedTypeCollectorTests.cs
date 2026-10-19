using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.TextReferences;
using AdjustNamespace.Tests.Infrastructure;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// Tests of <see cref="MovedTypeCollector"/>: which types (and namespaces) the plans of a
    /// session move, learnt before anything is changed.
    /// </summary>
    public class MovedTypeCollectorTests
    {
        private static async System.Threading.Tasks.Task<MovedTypeSet> CollectAsync(
            TestSolution solution,
            params string[] relativePaths
            )
        {
            var plans = new System.Collections.Generic.List<AdjustPlanItem>();

            foreach (var relativePath in relativePaths)
            {
                var plan = await new AdjustPlanner(solution.Context, new NamespaceReplaceRegex(string.Empty, string.Empty))
                    .TryPlanAsync(solution.PathOf("MyApp", relativePath));

                if (plan.HasValue)
                {
                    plans.Add(plan.Value);
                }
            }

            return await MovedTypeCollector.CollectAsync(solution.Workspace, plans);
        }

        [Fact]
        public async Task The_types_of_the_planned_files_are_collected_with_their_new_namespaces()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs",
@"namespace Legacy.Models
{
    public class Customer { }

    public enum Kind { A }

    public delegate void Handler();

    public class Repo<T> { }
}
")
                ;

            var moved = await CollectAsync(solution, @"Models\Customer.cs");

            Assert.Equal(
                new[] { "Legacy.Models.Customer", "Legacy.Models.Kind", "Legacy.Models.Handler", "Legacy.Models.Repo" },
                moved.Types.Select(t => t.OldFullName).ToArray()
                );
            Assert.All(moved.Types, t => Assert.Equal("MyApp.Models", t.NewNamespace));
            Assert.All(moved.Types, t => Assert.Equal("MyApp", t.AssemblyName));
            Assert.Equal(1, moved.Types.Single(t => t.Name == "Repo").Arity);
            Assert.Equal(0, moved.Types.Single(t => t.Name == "Customer").Arity);
        }

        [Fact]
        public async Task A_nested_type_moves_with_its_outer_one_and_is_known_by_its_metadata_name()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Order.cs",
@"namespace Legacy.Models
{
    public class Order
    {
        public class Item
        {
            public class Part { }
        }
    }
}
")
                ;

            var moved = await CollectAsync(solution, @"Models\Order.cs");

            var order = Assert.Single(moved.Types);

            Assert.Equal("Legacy.Models.Order", order.OldFullName);
            Assert.Equal(new[] { "Item", "Item+Part" }, order.NestedTypeNames.OrderBy(n => n).ToArray());
        }

        [Fact]
        public async Task A_file_which_is_not_planned_collects_nothing()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Settled.cs", "namespace MyApp { public class Settled { } }")
                .AddDocument("MyApp", "System.cs", "namespace System.Foo { public class Special { } }")
                ;

            var moved = await CollectAsync(solution, "Settled.cs", "System.cs");

            Assert.True(moved.IsEmpty);
            Assert.Empty(moved.Namespaces);
        }

        [Fact]
        public async Task A_namespace_that_loses_its_last_type_is_emptied_and_has_a_single_target()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs", "namespace Legacy.Models { public class Customer { } }")
                .AddDocument("MyApp", @"Models\Order.cs", "namespace Legacy.Models { public class Order { } }")
                ;

            var moved = await CollectAsync(solution, @"Models\Customer.cs", @"Models\Order.cs");

            var @namespace = Assert.Single(moved.Namespaces);

            Assert.Equal("Legacy.Models", @namespace.Name);
            Assert.True(@namespace.IsEmptied);
            Assert.Equal("MyApp.Models", @namespace.NewName);
        }

        [Fact]
        public async Task A_namespace_that_keeps_a_type_is_alive()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs", "namespace Legacy.Models { public class Customer { } }")
                .AddDocument("MyApp", @"Models\Order.cs", "namespace Legacy.Models { public class Order { } }")
                ;

            var moved = await CollectAsync(solution, @"Models\Customer.cs");

            var @namespace = Assert.Single(moved.Namespaces);

            Assert.False(@namespace.IsEmptied);
        }

        [Fact]
        public async Task A_namespace_whose_types_go_to_several_places_has_no_single_target()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"One\Customer.cs", "namespace Legacy.Models { public class Customer { } }")
                .AddDocument("MyApp", @"Two\Order.cs", "namespace Legacy.Models { public class Order { } }")
                ;

            var moved = await CollectAsync(solution, @"One\Customer.cs", @"Two\Order.cs");

            var @namespace = Assert.Single(moved.Namespaces);

            Assert.True(@namespace.IsEmptied);
            Assert.Null(@namespace.NewName);
            Assert.Equal(2, @namespace.Targets.Count);
        }

        [Fact]
        public async Task A_namespace_that_is_refilled_by_a_moved_type_is_not_emptied()
        {
            //the types swap their namespaces: after the run each of the two namespaces still
            //has a type (a reorganization of the folders looks like this)
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Alpha\One.cs", "namespace MyApp.Beta { public class One { } }")
                .AddDocument("MyApp", @"Beta\Two.cs", "namespace MyApp.Alpha { public class Two { } }")
                ;

            var moved = await CollectAsync(solution, @"Alpha\One.cs", @"Beta\Two.cs");

            Assert.False(moved.NamespaceOf("MyApp.Beta")!.IsEmptied);
            Assert.False(moved.NamespaceOf("MyApp.Alpha")!.IsEmptied);
        }

        [Fact]
        public async Task A_type_of_the_same_full_name_in_another_assembly_is_a_namesake()
        {
            using var solution = new TestSolution()
                .AddProject("Lib")
                .AddProject("MyApp")
                .AddProjectReference("MyApp", "Lib")
                .AddDocument("Lib", "Clock.cs", "namespace Legacy.Util { public class Clock { } }")
                .AddDocument("MyApp", @"Util\Clock.cs", "namespace Legacy.Util { public class Clock { } }")
                .AddDocument("MyApp", @"Util\Alone.cs", "namespace Legacy.Util { public class Alone { } }")
                ;

            var moved = await CollectAsync(solution, @"Util\Clock.cs", @"Util\Alone.cs");

            Assert.True(moved.Types.Single(t => t.Name == "Clock").HasLookalikeElsewhere);
            Assert.False(moved.Types.Single(t => t.Name == "Alone").HasLookalikeElsewhere);
        }

        [Fact]
        public async Task The_types_of_a_xaml_file_are_not_collected()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var xaml = solution.AddXamlFile("MyApp", @"Views\Lonely.xaml",
@"<UserControl x:Class=""Old.Lonely""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>");

            var moved = await MovedTypeCollector.CollectAsync(
                solution.Workspace,
                new[] { AdjustPlanItem.Xaml(xaml, "MyApp.Views") }
                );

            Assert.True(moved.IsEmpty);
        }
    }
}
