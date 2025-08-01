using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using HotChocolate.Language;
using HotChocolate.StarWars;

namespace HotChocolate.Validation;

public class DocumentValidatorTests
{
    [Fact]
    public void DocumentIsNull()
    {
        // arrange
        var schema = ValidationUtils.CreateSchema();
        var queryValidator = CreateValidator();

        // act
        void Error() =>
            queryValidator.Validate(
                schema,
                null!);

        // assert
        Assert.Throws<ArgumentNullException>(Error);
    }

    [Fact]
    public void SchemaIsNull()
    {
        // arrange
        var queryValidator = CreateValidator();

        // act
        void Error() =>
            queryValidator.Validate(
                null!,
                new DocumentNode(null, new List<IDefinitionNode>()));

        // assert
        Assert.Throws<ArgumentNullException>(Error);
    }

    [Fact]
    public void QueryWithTypeSystemDefinitions()
    {
        ExpectErrors(
            """
            query getDogName {
                dog {
                    name
                    color
                }
            }

            extend type Dog {
                color: String
            }
            """,
            t => Assert.Equal(
                "A document containing TypeSystemDefinition "
                + "is invalid for execution.",
                t.Message),
            t => Assert.Equal(
                "The field `color` does not exist "
                + "on the type `Dog`.",
                t.Message));
    }

    [Fact]
    public void QueryWithOneAnonymousAndOneNamedOperation()
    {
        ExpectErrors(
            """
            {
                dog {
                    name
                }
            }

            query getName {
                dog {
                    owner {
                        name
                    }
                }
            }
            """,
            t =>
            {
                Assert.Equal(
                    "GraphQL allows a short‐hand form for defining query "
                    + "operations when only that one operation exists in "
                    + "the document.",
                    t.Message);
            });
    }

    [Fact]
    public void TwoQueryOperationsWithTheSameName()
    {
        ExpectErrors(
            """
            query getName {
                dog {
                    name
                }
            }

            query getName {
                dog {
                    owner {
                        name
                    }
                }
            }
            """,
            t => Assert.Equal(
                "The operation name `getName` is not unique.",
                t.Message));
    }

    [Fact]
    public void OperationWithTwoVariablesThatHaveTheSameName()
    {
        ExpectErrors(
            """
            query houseTrainedQuery(
                $atOtherHomes: Boolean, $atOtherHomes: Boolean) {
                dog {
                    isHouseTrained(atOtherHomes: $atOtherHomes)
                }
            }
            """,
            t => Assert.Equal(
                "A document containing operations that "
                + "define more than one variable with the same "
                + "name is invalid for execution.",
                t.Message));
    }

    [Fact]
    public void DuplicateArgument()
    {
        ExpectErrors(
            """
                {
                    arguments {
                        ... goodNonNullArg
                    }
                }
                fragment goodNonNullArg on Arguments {
                    nonNullBooleanArgField(
                        nonNullBooleanArg: true, nonNullBooleanArg: true)
                }
            """,
            t => Assert.Equal(
                $"More than one argument with the same name in an argument set "
                + "is ambiguous and invalid.",
                t.Message));
    }

    [Fact]
    public void MissingRequiredArgNonNullBooleanArg()
    {
        ExpectErrors(
            """
                {
                    arguments {
                        ... missingRequiredArg
                    }
                }

                fragment missingRequiredArg on Arguments {
                    nonNullBooleanArgField(nonNullBooleanArg: null)
                }
            """,
            t => Assert.Equal(
                "The argument `nonNullBooleanArg` is required.",
                t.Message));
    }

    [Fact]
    public void DisallowedSecondRootField()
    {
        ExpectErrors(
            """
            subscription sub {
                newMessage {
                    body
                    sender
                }
                disallowedSecondRootFieldNonExisting
            }
            """,
            t => Assert.Equal(
                $"Subscription operations must have exactly one root field.",
                t.Message),
            t => Assert.Equal(
                "The field `disallowedSecondRootFieldNonExisting` does not exist "
                + "on the type `Subscription`.",
                t.Message));
    }

    [Fact]
    public void FieldIsNotDefinedOnTypeInFragment()
    {
        ExpectErrors(
            """
            {
                dog {
                    ... fieldNotDefined
                    ... aliasedLyingFieldTargetNotDefined
                }
            }

            fragment fieldNotDefined on Dog {
                meowVolume
            }

            fragment aliasedLyingFieldTargetNotDefined on Dog {
                barkVolume: kawVolume
            }
            """,
            t => Assert.Equal(
                "The field `meowVolume` does not exist "
                + "on the type `Dog`.",
                t.Message),
            t => Assert.Equal(
                "The field `kawVolume` does not exist "
                + "on the type `Dog`.",
                t.Message));
    }

    [Fact]
    public void VariableNotUsedWithinFragment()
    {
        ExpectErrors(
            """
            query variableNotUsedWithinFragment($atOtherHomes: Boolean) {
                dog {
                    ...isHouseTrainedWithoutVariableFragment
                }
            }

            fragment isHouseTrainedWithoutVariableFragment on Dog {
                barkVolume
            }
            """,
            t => Assert.Equal(
                "The following variables were not used: "
                + "atOtherHomes.",
                t.Message));
    }

    [Fact]
    public void SkipDirectiveIsInTheWrongPlace()
    {
        ExpectErrors(
            """
            query @skip(if: $foo) {
                field
            }
            """);
    }

    [Fact]
    public void QueriesWithInvalidVariableTypes()
    {
        // arrange
        ExpectErrors(
            null,
            DocumentValidatorBuilder.New()
                .AddDefaultRules()
                .ModifyOptions(o => o.MaxAllowedErrors = int.MaxValue)
                .Build(),
                """
                query takesCat($cat: Cat) {
                    # ...
                }

                query takesDogBang($dog: Dog!) {
                    # ...
                }

                query takesListOfPet($pets: [Pet]) {
                    # ...
                }

                query takesCatOrDog($catOrDog: CatOrDog) {
                    # ...
                }
                """,
            t => Assert.Equal(
                "Operation `takesCat` has an empty selection set. Root types without "
                + "subfields are disallowed.",
                t.Message),
            t => Assert.Equal(
                "Operation `takesDogBang` has an empty selection set. Root types without "
                + "subfields are disallowed.",
                t.Message),
            t => Assert.Equal(
                "Operation `takesListOfPet` has an empty selection set. Root types without "
                + "subfields are disallowed.",
                t.Message),
            t => Assert.Equal(
                "Operation `takesCatOrDog` has an empty selection set. Root types without "
                + "subfields are disallowed.",
                t.Message),
            t => Assert.Equal(
                "The type of variable `cat` is not an input type.",
                t.Message),
            t => Assert.Equal(
                "The following variables were not used: cat.",
                t.Message),
            t => Assert.Equal(
                "The type of variable `dog` is not an input type.",
                t.Message),
            t => Assert.Equal(
                "The following variables were not used: dog.",
                t.Message),
            t => Assert.Equal(
                "The type of variable `pets` is not an input type.",
                t.Message),
            t => Assert.Equal(
                "The following variables were not used: pets.",
                t.Message),
            t => Assert.Equal(
                "The type of variable `catOrDog` is not an input type.",
                t.Message),
            t => Assert.Equal(
                "The following variables were not used: catOrDog.",
                t.Message));
    }

    [Fact]
    public void ConflictingBecauseAlias()
    {
        ExpectErrors(
            """
            fragment conflictingBecauseAlias on Dog {
                name: nickname
                name
            }
            """,
            t => Assert.Equal(
                "The specified fragment `conflictingBecauseAlias` "
                + "is not used within the current document.",
                t.Message));
    }

    [Fact]
    public void InvalidFieldArgName()
    {
        ExpectErrors(
            """
                {
                    dog {
                        ... invalidArgName
                    }
                }

                fragment invalidArgName on Dog {
                    doesKnowCommand(command: CLEAN_UP_HOUSE)
                }
            """,
            t => Assert.Equal(
                "The argument `command` does not exist.",
                t.Message),
            t => Assert.Equal(
                "The argument `dogCommand` is required.",
                t.Message));
    }

    [Fact]
    public void UnusedFragment()
    {
        ExpectErrors(
            """
            fragment nameFragment on Dog { # unused
                name
            }

            {
                dog {
                    name
                }
            }
            """,
            t => Assert.Equal(
                "The specified fragment `nameFragment` "
                + "is not used within the current document.",
                t.Message));
    }

    [Fact]
    public void DuplicateFragments()
    {
        ExpectErrors(
            """
                {
                    dog {
                        ...fragmentOne
                    }
                }

                fragment fragmentOne on Dog {
                    name
                }

                fragment fragmentOne on Dog {
                    owner {
                        name
                    }
                }
            """,
            t => Assert.Equal(
                "There are multiple fragments with the name `fragmentOne`.",
                t.Message));
    }

    [Fact]
    public void ScalarSelectionsNotAllowedOnInt()
    {
        ExpectErrors(
            """
            {
                dog {
                    barkVolume {
                        sinceWhen
                    }
                }
            }
            """,
            t => Assert.Equal(
                "Field \"barkVolume\" must not have a selection since type \"Int\" has no "
                + "subfields.",
                t.Message));
    }

    [Fact]
    public void InlineFragOnScalar()
    {
        ExpectErrors(
            """
                {
                    dog {
                       ... inlineFragOnScalar
                    }
                }

                fragment inlineFragOnScalar on Dog {
                    ... on Boolean {
                        somethingElse
                    }
                }
            """,
            t => Assert.Equal(
                "Fragments can only be declared on unions, interfaces, and objects.",
                t.Message));
    }

    [Fact]
    public void FragmentCycle1()
    {
        ExpectErrors(
            """
                {
                    dog {
                        ...nameFragment
                    }
                }

                fragment nameFragment on Dog {
                    name
                    ...barkVolumeFragment
                }

                fragment barkVolumeFragment on Dog {
                    barkVolume
                    ...nameFragment
                }
            """,
            t => Assert.Equal(
                "The graph of fragment spreads must not form any "
                + "cycles including spreading itself. Otherwise an "
                + "operation could infinitely spread or infinitely "
                + "execute on cycles in the underlying data.",
                t.Message));
    }

    [Fact]
    public void UndefinedFragment()
    {
        ExpectErrors(
            """
            {
                dog {
                    ...undefinedFragment
                }
            }
            """,
            t => Assert.Equal(
                "The specified fragment `undefinedFragment` "
                + "does not exist.",
                t.Message));
    }

    [Fact]
    public void FragmentDoesNotMatchType()
    {
        ExpectErrors(
            """
                {
                    dog {
                        ...fragmentDoesNotMatchType
                    }
                }

                fragment fragmentDoesNotMatchType on Human {
                    name
                }
            """,
            t => Assert.Equal(
                "The parent type does not match the type condition on "
                + "the fragment.",
                t.Message));
    }

    [Fact]
    public void NotExistingTypeOnInlineFragment()
    {
        ExpectErrors(
            """
            {
                dog {
                    ...inlineNotExistingType
                }
            }

            fragment inlineNotExistingType on Dog {
                ... on NotInSchema {
                    name
                }
            }
            """,
            t =>
            {
                Assert.Equal(
                    "Unknown type `NotInSchema`.",
                    t.Message);
            });
    }

    [Fact]
    public void InvalidInputObjectFieldsExist()
    {
        ExpectErrors(
            """
                {
                    findDog(complex: { favoriteCookieFlavor: "Bacon" })
                    {
                        name
                    }
                }
            """,
            t => Assert.Equal(
                "The specified input object field "
                + "`favoriteCookieFlavor` does not exist.",
                t.Message));
    }

    [Fact]
    public void RequiredFieldIsNull()
    {
        ExpectErrors(
            """
            {
                findDog2(complex: { name: null })
                {
                    name
                }
            }
            """,
            t => Assert.Equal(
                "`name` is a required field and cannot be null.",
                t.Message));
    }

    [Fact]
    public void NameFieldIsAmbiguous()
    {
        ExpectErrors(
            """
            {
                findDog(complex: { name: "A", name: "B" })
                {
                    name
                }
            }
            """,
            t =>
                Assert.Equal("There can be only one input field named `name`.", t.Message));
    }

    [Fact]
    public void UnsupportedDirective()
    {
        ExpectErrors(
            """
            {
                dog {
                    name @foo(bar: true)
                }
            }
            """,
            t => Assert.Equal(
                "The specified directive `foo` "
                + "is not supported by the current schema.",
                t.Message));
    }

    [Fact]
    public void StringIntoInt()
    {
        ExpectErrors(
            """
            {
                arguments {
                    ...stringIntoInt
                }
            }

            fragment stringIntoInt on Arguments {
                intArgField(intArg: "123")
            }
            """,
            t => Assert.Equal(
                "The specified argument value does not match the "
                + "argument type.",
                t.Message));
    }

    [Fact]
    public void MaxDepthRuleIsIncluded()
    {
        ExpectErrors(
            null,
            DocumentValidatorBuilder.New()
                .AddDefaultRules()
                .AddMaxExecutionDepthRule(1)
                .Build(),
            """
            query {
                catOrDog
                {
                    ... on Cat {
                        name
                    }
                }
            }
            """,
            t =>
            {
                Assert.Equal(
                    "The GraphQL document has an execution depth of 2 "
                    + "which exceeds the max allowed execution depth of 1.",
                    t.Message);
            });
    }

    [Fact]
    public void GoodBooleanArgDefault2()
    {
        ExpectValid(
            """
                query {
                    arguments {
                        ... goodBooleanArgDefault
                    }
                }

                fragment goodBooleanArgDefault on Arguments {
                    optionalNonNullBooleanArgField2
                }
            """);
    }

    [Fact]
    public void StarWars_Query_Is_Valid()
    {
        ExpectValid(
            SchemaBuilder.New()
                .AddStarWarsTypes()
                .Create(),
            null,
            FileResource.Open("StarWars_Request.graphql"));
    }

    [Fact]
    public void DuplicatesWillBeIgnoredOnFieldMerging()
    {
        // arrange
        var schema = SchemaBuilder.New()
            .AddStarWarsTypes()
            .Create();

        var document = Utf8GraphQLParser.Parse(
            FileResource.Open("InvalidIntrospectionQuery.graphql"));

        var originalOperation = (OperationDefinitionNode)document.Definitions[0];
        var operationWithDuplicates = originalOperation.WithSelectionSet(
            originalOperation.SelectionSet.WithSelections(
                [
                    originalOperation.SelectionSet.Selections[0],
                    originalOperation.SelectionSet.Selections[0]
                ]));

        document = document.WithDefinitions(
            [
                .. document.Definitions.Skip(1),
                operationWithDuplicates
            ]);

        var validator = CreateValidator();

        // act
        var result = validator.Validate(schema, document);

        // assert
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Ensure_That_Merged_Fields_Are_Not_In_Violation_Of_Duplicate_Directives_Rule()
    {
        ExpectValid(
            """
            query ($a: Boolean!) {
                dog {
                    ... inlineFragOnScalar
                    owner @include(if: $a) {
                        address
                    }
                }
            }

            fragment inlineFragOnScalar on Dog {
                owner @include(if: $a) {
                    name
                }
            }
            """);
    }

    [Fact]
    public void Ensure_Recursive_Fragments_Fail()
    {
        ExpectErrors("fragment f on Query{...f} {...f}");
    }

    [Fact]
    public void Ensure_Recursive_Fragments_Fail_2()
    {
        ExpectErrors(
            """
            fragment f on Query {
                ...f
                f {
                    ...f
                    f {
                        ...f
                    }
                }
            }

            {...f}
            """);
    }

    [Fact]
    public void Short_Long_Names()
    {
        ExpectErrors(FileResource.Open("short_long_names_query.graphql"));
    }

    [Fact]
    public void Anonymous_empty_query_repeated_25000()
    {
        ExpectErrors(FileResource.Open("anonymous_empty_query_repeated_25000.graphql"));
    }

    [Fact]
    public void Type_query_repeated_6250()
    {
        ExpectErrors(FileResource.Open("__type_query_repeated_6250.graphql"));
    }

    [Fact]
    public void Typename_query_repeated_4167()
    {
        ExpectErrors(FileResource.Open("__typename_query_repeated_4167.graphql"));
    }

    [Fact]
    public void Typename_query()
    {
        ExpectValid(FileResource.Open("__typename_query.graphql"));
    }

    [Fact]
    public void Produce_Many_Errors_100_query()
    {
        ExpectErrors(FileResource.Open("100_query.graphql"));
    }

    [Fact]
    public void Produce_Many_Errors_1000_query()
    {
        ExpectErrors(FileResource.Open("1000_query.graphql"));
    }

    [Fact]
    public void Produce_Many_Errors_10000_query()
    {
        ExpectErrors(FileResource.Open("10000_query.graphql"));
    }

    [Fact]
    public void Produce_Many_Errors_25000_query()
    {
        ExpectErrors(FileResource.Open("25000_query.graphql"));
    }

    [Fact]
    public void Produce_Many_Errors_30000_query()
    {
        ExpectErrors(FileResource.Open("30000_query.graphql"));
    }

    [Fact]
    public void Produce_Many_Errors_50000_query()
    {
        ExpectErrors(FileResource.Open("50000_query.graphql"));
    }

    [Fact]
    public void Introspection_Cycle_Detected()
        => ExpectErrors(FileResource.Open("introspection_with_cycle.graphql"));

    private static void ExpectValid([StringSyntax("graphql")] string sourceText)
        => ExpectValid(null, null, sourceText);

    private static void ExpectValid(
        ISchemaDefinition? schema,
        DocumentValidator? validator,
        [StringSyntax("graphql")] string sourceText)
    {
        // arrange
        schema ??= ValidationUtils.CreateSchema();
        validator ??= CreateValidator();
        var operation = Utf8GraphQLParser.Parse(sourceText);

        // act
        var result = validator.Validate(schema, operation);

        // assert
        Assert.Empty(result.Errors);
    }

    private static void ExpectErrors(
        [StringSyntax("graphql")] string sourceText,
        params Action<IError>[] elementInspectors)
        => ExpectErrors(null, null, sourceText, elementInspectors);

    private static void ExpectErrors(
        ISchemaDefinition? schema,
        DocumentValidator? validator,
        string sourceText,
        params Action<IError>[] elementInspectors)
    {
        // arrange
        schema ??= ValidationUtils.CreateSchema();
        validator ??= CreateValidator();
        var document = Utf8GraphQLParser.Parse(sourceText, new ParserOptions(maxAllowedFields: int.MaxValue));

        // act
        var result = validator.Validate(schema, document);

        // assert
        Assert.NotEmpty(result.Errors);

        if (elementInspectors.Length > 0)
        {
            Assert.Collection(result.Errors, elementInspectors);
        }

        result.Errors.MatchSnapshot();
    }

    private static DocumentValidator CreateValidator()
    {
        return DocumentValidatorBuilder.New()
            .AddDefaultRules()
            .Build();
    }
}
