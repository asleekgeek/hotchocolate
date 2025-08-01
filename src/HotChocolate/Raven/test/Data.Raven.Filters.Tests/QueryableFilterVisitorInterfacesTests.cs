using System.ComponentModel.DataAnnotations;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Filters;

[Collection(SchemaCacheCollectionFixture.DefinitionName)]
public class QueryableFilterVisitorInterfacesTests
{
    private static readonly BarInterface[] s_barEntities =
    [
        new() { Test = new InterfaceImpl1 { Prop = "a" } },
        new() { Test = new InterfaceImpl1 { Prop = "b" } }
    ];

    private readonly SchemaCache _cache;

    public QueryableFilterVisitorInterfacesTests(SchemaCache cache)
    {
        _cache = cache;
    }

    [Fact]
    public async Task Create_InterfaceStringEqual_Expression()
    {
        // arrange
        var tester = _cache
            .CreateSchema<BarInterface, FilterInputType<BarInterface>>(s_barEntities,
                configure: Configure);

        // act
        var res1 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    "{ root(where: { test: { prop: { eq: \"a\"}}}) "
                    + "{ test{ prop }}}")
                .Build());

        var res2 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    "{ root(where: { test: { prop: { eq: \"b\"}}}) "
                    + "{ test{ prop }}}")
                .Build());

        var res3 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    "{ root(where: { test: { prop: { eq: null}}}) "
                    + "{ test{ prop}}}")
                .Build());

        // assert
        await Snapshot
            .Create()
            .AddResult(res1, "a")
            .AddResult(res2, "ba")
            .AddResult(res3, "null")
            .MatchAsync();
    }

    private static void Configure(IRequestExecutorBuilder builder)
        => builder
            .AddObjectType<InterfaceImpl1>(x => x.Implements<InterfaceType<Test>>())
            .AddObjectType<InterfaceImpl2>(x => x.Implements<InterfaceType<Test>>())
            .AddInterfaceType<Test>();

    public abstract class Test
    {
        [Key]
        public string? Id { get; set; }

        public string Prop { get; set; } = null!;
    }

    public class InterfaceImpl1 : Test
    {
        public string Specific1 { get; set; } = null!;
    }

    public class InterfaceImpl2 : Test
    {
        public string Specific2 { get; set; } = null!;
    }

    public class BarInterface
    {
        [Key]
        public string? Id { get; set; }

        public Test Test { get; set; } = null!;
    }
}
