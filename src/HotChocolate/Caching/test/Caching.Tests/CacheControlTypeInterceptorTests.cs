using System.Text;
using HotChocolate.Execution;
using HotChocolate.Tests;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Caching.Tests;

public class CacheControlTypeInterceptorTests
{
    [Fact]
    public async Task QueryFields_ApplyDefaults()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddDocumentFromString(@"
                type Query {
                    field1: String
                    field2: String @cacheControl(maxAge: 200)
                }
            ")
            .UseField(_ => _)
            .AddCacheControl()
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task QueryFields_ApplyDefaults_DifferentDefaultMaxAge()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddDocumentFromString(@"
                type Query {
                    field1: String
                    field2: String @cacheControl(maxAge: 200)
                }
            ")
            .UseField(_ => _)
            .AddCacheControl()
            .ModifyCacheControlOptions(o => o.DefaultMaxAge = 100)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task QueryFields_ApplyDefaults_DifferentDefaultScope()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddDocumentFromString(@"
                type Query {
                    field1: String
                    field2: String @cacheControl(maxAge: 200, scope: PUBLIC)
                }
            ")
            .UseField(_ => _)
            .AddCacheControl()
            .ModifyCacheControlOptions(o => o.DefaultScope = CacheControlScope.Private)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task QueryFields_ApplyDefaults_False()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddDocumentFromString(@"
                type Query {
                    field1: String
                    field2: String @cacheControl(maxAge: 200)
                }
            ")
            .UseField(_ => _)
            .AddCacheControl()
            .ModifyCacheControlOptions(o => o.ApplyDefaults = false)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task QueryFields_CacheControl_Disabled()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddDocumentFromString(@"
                type Query {
                    field1: String
                    field2: String @cacheControl(maxAge: 200)
                }
            ")
            .UseField(_ => _)
            .AddCacheControl()
            .ModifyCacheControlOptions(o => o.Enable = false)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task DataResolvers_ApplyDefaults()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<Query>()
            .AddCacheControl()
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task DataResolvers_ApplyDefaults_DifferentDefaultMaxAge()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<Query>()
            .AddCacheControl()
            .ModifyCacheControlOptions(o => o.DefaultMaxAge = 100)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task DataResolvers_ApplyDefaults_DifferentDefaultScope()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<Query>()
            .AddCacheControl()
            .ModifyCacheControlOptions(o => o.DefaultScope = CacheControlScope.Private)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task DataResolvers_ApplyDefaults_False()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<Query>()
            .AddCacheControl()
            .ModifyCacheControlOptions(o => o.ApplyDefaults = false)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public async Task DataResolvers_CacheControl_Disabled()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<Query>()
            .AddCacheControl()
            .ModifyCacheControlOptions(o => o.Enable = false)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    [Fact]
    public void NegativeMaxAge()
    {
        ExpectErrors(builder => builder
            .AddDocumentFromString(@"
                type Query {
                    field: String @cacheControl(maxAge: -10)
                }
            ")
            .Use(_ => _ => default)
            .AddCacheControl());
    }

    [Fact]
    public void MaxAgeAndInheritMaxAgeOnSameField()
    {
        ExpectErrors(builder => builder
            .AddDocumentFromString(@"
                type Query {
                    field: NestedType
                }

                type NestedType {
                    field: String @cacheControl(maxAge: 10 inheritMaxAge: true)
                }
            ")
            .Use(_ => _ => default)
            .AddCacheControl());
    }

    [Fact]
    public void SharedMaxAgeAndInheritMaxAgeOnSameField()
    {
        ExpectErrors(builder => builder
            .AddDocumentFromString(@"
                type Query {
                    field: NestedType
                }

                type NestedType {
                    field: String @cacheControl(sharedMaxAge: 10 inheritMaxAge: true)
                }
            ")
            .Use(_ => _ => default)
            .AddCacheControl());
    }

    [Fact]
    public void CacheControlOnInterfaceField()
    {
        ExpectErrors(builder => builder
            .AddDocumentFromString(@"
                type Query {
                    field: Interface!
                }

                interface Interface {
                    field: String @cacheControl(maxAge: 10)
                }

                type Object implements Interface {
                    field: String
                }
            ")
            .Use(_ => _ => default)
            .AddCacheControl());
    }

    [Fact]
    public void InheritMaxAgeOnObjectType()
    {
        ExpectErrors(builder => builder
            .AddDocumentFromString(@"
                type Query {
                    field: ObjectType
                }

                type ObjectType @cacheControl(inheritMaxAge: true) {
                    field: String
                }
            ")
            .Use(_ => _ => default)
            .AddCacheControl());
    }

    [Fact]
    public void InheritMaxAgeOnInterfaceType()
    {
        ExpectErrors(builder => builder
            .AddDocumentFromString(@"
                type Query {
                    field: InterfaceType
                }

                type InterfaceType @cacheControl(inheritMaxAge: true) {
                    field: String
                }

                type ObjectType {
                    field: String
                }
            ")
            .Use(_ => _ => default)
            .AddCacheControl());
    }

    [Fact]
    public void InheritMaxAgeOnUnionType()
    {
        ExpectErrors(builder => builder
            .AddDocumentFromString(@"
                type Query {
                    field: UnionType
                }

                union UnionType @cacheControl(inheritMaxAge: true) = ObjectType

                type ObjectType {
                    field: String
                }
            ")
            .Use(_ => _ => default)
            .AddCacheControl());
    }

    [Fact]
    public void InheritMaxAgeOnQueryTypeField()
    {
        ExpectErrors(builder => builder
            .AddDocumentFromString(@"
                type Query {
                    field: String @cacheControl(inheritMaxAge: true)
                }
            ")
            .Use(_ => _ => default)
            .AddCacheControl());
    }

    private static void ExpectErrors(Action<SchemaBuilder> configureBuilder)
    {
        try
        {
            var builder = SchemaBuilder.New();

            configureBuilder(builder);

            builder.Create();

            Assert.Fail("Expected error!");
        }
        catch (SchemaException ex)
        {
            Assert.NotEmpty(ex.Errors);

            var text = new StringBuilder();

            foreach (var error in ex.Errors)
            {
                text.AppendLine(error.ToString());
                text.AppendLine();
            }

            text.ToString().MatchSnapshot();
        }
    }

    public class Query : NestedType
    {
        public NestedType Nested { get; } = new();

        public NestedType2 Nested2 { get; } = new();
    }

    public class NestedType
    {
        public string PureField { get; } = null!;

        public Task<string> TaskField() => null!;

        public ValueTask<string> ValueTaskField() => default!;

        public IExecutable<string> ExecutableField() => null!;

        public IQueryable<string> QueryableField() => null!;

        [UsePaging]
        public IQueryable<string> QueryableFieldWithConnection() => null!;

        [UseOffsetPaging]
        public IQueryable<string> QueryableFieldWithCollectionSegment() => null!;

        [CacheControl(200)]
        public string PureFieldWithCacheControl { get; } = null!;

        [CacheControl(200)]
        public Task<string> TaskFieldWithCacheControl() => null!;

        [CacheControl(200)]
        public ValueTask<string> ValueTaskFieldWithCacheControl() => default!;

        [CacheControl(200)]
        public IExecutable<string> ExecutableFieldWithCacheControl() => null!;

        [CacheControl(MaxAge = 200)]
        public IQueryable<string> QueryableFieldWithCacheControl() => null!;

        [CacheControl(SharedMaxAge = 200)]
        public IQueryable<string> QueryableFieldWithCacheControlSharedMaxAge() => null!;

        [CacheControl(500, SharedMaxAge = 200)]
        public IQueryable<string> QueryableFieldWithCacheControlMaxAgeAndSharedMaxAge() => null!;

        [CacheControl(500, SharedMaxAge = 200, Vary = ["accept-language", "x-timezoneoffset"])]
        public IQueryable<string> QueryableFieldWithCacheControlMaxAgeAndSharedMaxAgeAndVary() => null!;

        [CacheControl(200)]
        [UsePaging]
        public IQueryable<string> QueryableFieldWithConnectionWithCacheControl() => null!;

        [CacheControl(200)]
        [UseOffsetPaging]
        public IQueryable<string> QueryableFieldWithCollectionSegmentWithCacheControl() => null!;
    }

    public class NestedType2
    {
        [CacheControl(InheritMaxAge = true)]
        public Task<string> TaskFieldWithInheritMaxAge() => null!;
    }
}
