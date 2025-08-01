using HotChocolate.Types;

namespace HotChocolate.Configuration;

public class TypeDiscoveryTests
{
    [Fact]
    public void InferDateTime()
    {
        SchemaBuilder.New()
            .AddQueryType<QueryWithDateTime>()
            .Create()
            .ToString()
            .MatchSnapshot();
    }

    [Fact]
    public void InferDateTimeFromModel()
    {
        SchemaBuilder.New()
            .AddQueryType<QueryType>()
            .Create()
            .ToString()
            .MatchSnapshot();
    }

    [Fact]
    public void TypeDiscovery_Should_InferStructs()
    {
        SchemaBuilder.New()
            .AddQueryType<QueryTypeWithStruct>()
            .Create()
            .ToString()
            .MatchSnapshot();
    }

    [Fact]
    public void InferInputStructsWithNonDefaultCtor()
    {
        SchemaBuilder.New()
            .AddQueryType<QueryTypeWithInputStruct>()
            .Create()
            .ToString()
            .MatchSnapshot();
    }

    [Fact]
    public void InferInputTypeWithComputedProperty()
    {
        SchemaBuilder.New()
            .AddQueryType<QueryTypeWithComputedProperty>()
            .Create()
            .ToString()
            .MatchSnapshot();
    }

    [Fact]
    public void Custom_LocalDate_Should_Throw_SchemaException_When_Not_Bound()
    {
        static void Act() =>
            SchemaBuilder.New()
                .AddQueryType<QueryTypeWithCustomLocalDate>()
                .Create();

        Assert.Equal(
            "The name `LocalDate` was already registered by another type.",
            Assert.Throws<SchemaException>(Act).Errors[0].Message);
    }

    public class QueryWithDateTime
    {
        public DateTimeOffset DateTimeOffset(DateTimeOffset time) => time;

        public DateTime DateTime(DateTime time) => time;
    }

    public class QueryType : ObjectType
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Query");
            descriptor.Field("items")
                .Type<ListType<ModelType>>()
                .Resolve(string.Empty);

            descriptor.Field("paging")
                .UsePaging<ModelType>()
                .Resolve(string.Empty);
        }
    }

    public class ModelType : ObjectType<Model>
    {
        protected override void Configure(IObjectTypeDescriptor<Model> descriptor)
        {
            descriptor.Field(t => t.Time)
                .Type<NonNullType<DateTimeType>>();

            descriptor.Field(t => t.Date)
                .Type<NonNullType<DateType>>();
        }
    }

    public class Model
    {
        public string Foo { get; set; }

        public int Bar { get; set; }

        public bool Baz { get; set; }

        public DateTime Time { get; set; }

        public DateTime Date { get; set; }
    }

    public struct InferStruct
    {
        public Guid Id { get; set; }

        public int Number { get; set; }
    }

    public class QueryTypeWithStruct
    {
        public InferStruct Struct { get; set; }

        public InferStruct? NullableStruct { get; set; }

        public InferStruct[] StructArray { get; set; }

        public InferStruct?[] NullableStructArray { get; set; }

        public InferStruct[][] StructNestedArray { get; set; }

        public InferStruct?[][] NullableStructNestedArray { get; set; }

        public Guid ScalarGuid { get; set; }

        public DateTime ScalarDateTime { get; set; }
    }

    public struct InputStructWithCtor
    {
        public InputStructWithCtor(IEnumerable<int> values)
            => Values = [.. values];

        public System.Collections.Immutable.ImmutableArray<int> Values { get; set; }
    }

    public class QueryTypeWithInputStruct
    {
        public int Foo(InputStructWithCtor arg) => 0;
    }

    public class InputTypeWithReadOnlyProperties(int property2)
    {
        public int Property1 { get; set; }

        public int Property2 { get; } = property2;

        public int Property3 => Property2 / 2;
    }

    public class QueryTypeWithComputedProperty
    {
        public int Foo(InputTypeWithReadOnlyProperties arg) => arg.Property1;
    }

    public class QueryTypeWithCustomLocalDate
    {
        public LocalDate Foo() => new();
    }

    public class LocalDate
    {
        public DateOnly Date { get; set; }
    }
}
