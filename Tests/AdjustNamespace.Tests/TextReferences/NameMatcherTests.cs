using AdjustNamespace.TextReferences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// Tests of <see cref="NameMatcher"/>: which names of the moved types it finds in a text.
    /// Only the full names are searched, never a short one, and a name has borders.
    /// </summary>
    public class NameMatcherTests
    {
        private static readonly MovedType Customer = new("Legacy.Models", "Lib.Models", "Customer", 0, "Lib");

        private static readonly MovedType Order = new(
            "Legacy.Models",
            "Lib.Models",
            "Order",
            0,
            "Lib",
            nestedTypeNames: new[] { "Item", "Item+Part" }
            );

        private static readonly MovedType Repo = new("Legacy.Models", "Lib.Models", "Repo", 1, "Lib");

        private static readonly MovedType PlainRepo = new("Legacy.Models", "Lib.Models", "Repo", 0, "Lib");

        private static NameMatcher MatcherOf(params MovedType[] types)
        {
            return new NameMatcher(new MovedTypeSet(types, new List<MovedNamespace>()));
        }

        private static List<NameOccurrence> Find(string text, params MovedType[] types)
        {
            return MatcherOf(types.Length == 0 ? new[] { Customer, Order, Repo } : types)
                .FindTypes(text, 0, text.Length);
        }

        [Fact]
        public void A_full_name_is_found()
        {
            var found = Assert.Single(Find("create Legacy.Models.Customer now"));

            Assert.Same(Customer, found.Type);
            Assert.Equal(7, found.Start);
            Assert.Equal("Legacy.Models.Customer".Length, found.Length);
        }

        [Fact]
        public void A_short_name_is_never_found()
        {
            Assert.Empty(Find("Customer"));
            Assert.Empty(Find("Models.Customer"));
            Assert.Empty(Find("the Customer of the Models"));
        }

        [Theory]
        [InlineData("X.Legacy.Models.Customer")]
        [InlineData("XLegacy.Models.Customer")]
        [InlineData("Legacy.Models.CustomerExtra")]
        [InlineData("Legacy.Models.Customer_2")]
        [InlineData("Legacy.Models.Customers")]
        public void A_name_has_borders(string text)
        {
            Assert.Empty(Find(text));
        }

        [Theory]
        [InlineData("\"Legacy.Models.Customer\"")]
        [InlineData("type=Legacy.Models.Customer;")]
        [InlineData("(Legacy.Models.Customer)")]
        [InlineData("<Legacy.Models.Customer>")]
        [InlineData("[Legacy.Models.Customer]")]
        [InlineData("a,Legacy.Models.Customer")]
        public void Punctuation_is_a_border(string text)
        {
            Assert.Single(Find(text));
        }

        [Fact]
        public void A_dot_behind_the_name_is_allowed()
        {
            var found = Assert.Single(Find("Legacy.Models.Customer.Name"));

            Assert.Equal("Legacy.Models.Customer".Length, found.Length);
            Assert.Equal(".Name", found.DottedRemainder);
        }

        [Fact]
        public void The_global_prefix_is_a_part_of_the_occurrence_and_not_of_the_name()
        {
            var found = Assert.Single(Find("global::Legacy.Models.Customer"));

            Assert.Equal(0, found.FullStart);
            Assert.Equal("global::".Length, found.Start);
            Assert.Equal("Legacy.Models.Customer".Length, found.Length);
        }

        [Fact]
        public void The_nested_form_is_recognized()
        {
            var found = Assert.Single(Find("Legacy.Models.Order+Item+Part"));

            Assert.Same(Order, found.Type);
            Assert.Equal("Item+Part", found.WrittenNestedPath);
            Assert.Equal("Legacy.Models.Order+Item+Part".Length, found.FullEnd);
        }

        [Fact]
        public void The_arity_picks_the_type_of_that_arity()
        {
            var generic = Assert.Single(Find("Legacy.Models.Repo`1", Repo, PlainRepo));
            var plain = Assert.Single(Find("Legacy.Models.Repo", Repo, PlainRepo));

            Assert.Same(Repo, generic.Type);
            Assert.Equal(1, generic.WrittenArity);
            Assert.Same(PlainRepo, plain.Type);
        }

        [Fact]
        public void The_assembly_qualified_form_is_recognized()
        {
            var found = Assert.Single(
                Find("Legacy.Models.Customer, Lib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
                );

            Assert.Equal("Lib", found.WrittenAssembly);
            Assert.Equal(
                "Legacy.Models.Customer, Lib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null".Length,
                found.FullEnd
                );
        }

        [Fact]
        public void An_assembly_name_alone_is_recognized_at_the_end_of_the_text_and_before_a_delimiter()
        {
            Assert.Equal("Lib", Assert.Single(Find("Legacy.Models.Customer, Lib")).WrittenAssembly);
            Assert.Equal("Lib", Assert.Single(Find("[[Legacy.Models.Customer, Lib]]")).WrittenAssembly);
            Assert.Equal("Lib", Assert.Single(Find("\"Legacy.Models.Customer, Lib\" more")).WrittenAssembly);
        }

        [Fact]
        public void A_comma_in_prose_is_no_assembly()
        {
            Assert.Null(Assert.Single(Find("Legacy.Models.Customer, which is a class")).WrittenAssembly);
            Assert.Null(Assert.Single(Find("Legacy.Models.Customer, and Legacy.Models.Order", Customer)).WrittenAssembly);
        }

        [Fact]
        public void Several_names_in_a_text_are_all_found()
        {
            var found = Find("Legacy.Models.Customer;Legacy.Models.Order;Legacy.Models.Customer");

            Assert.Equal(3, found.Count);
            Assert.Equal(
                new[] { Customer, Order, Customer },
                found.Select(f => f.Type).ToArray()
                );
        }

        [Fact]
        public void Only_the_given_part_of_the_text_is_searched()
        {
            var text = "Legacy.Models.Customer and Legacy.Models.Order";
            var matcher = MatcherOf(Customer, Order);

            var found = matcher.FindTypes(text, "Legacy.Models.Customer and ".Length, "Legacy.Models.Order".Length);

            Assert.Same(Order, Assert.Single(found).Type);
        }

        [Fact]
        public void The_start_of_the_searched_part_is_a_border()
        {
            //the dot in front of the part belongs to a text the search was not asked about
            var text = "X.Legacy.Models.Customer";

            var found = MatcherOf(Customer).FindTypes(text, 2, text.Length - 2);

            Assert.Single(found);
        }

        [Fact]
        public void A_whole_namespace_is_found_only_as_the_whole_text()
        {
            var @namespace = new MovedNamespace("Legacy.Models", new[] { "Lib.Models" }, true);
            var matcher = new NameMatcher(new MovedTypeSet(new[] { Customer }, new[] { @namespace }));

            var found = matcher.FindWholeNamespace("  Legacy.Models;  ", 0, "  Legacy.Models;  ".Length);

            Assert.NotNull(found);
            Assert.Equal(2, found!.Value.Start);
            Assert.Equal("Legacy.Models".Length, found.Value.Length);

            Assert.Null(matcher.FindWholeNamespace("Legacy.Models.Sub", 0, "Legacy.Models.Sub".Length));
            Assert.Null(matcher.FindWholeNamespace("static Legacy.Models", 0, "static Legacy.Models".Length));
        }
    }
}
