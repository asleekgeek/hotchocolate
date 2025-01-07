using System.Collections.Immutable;
using HotChocolate.Fusion.Events;
using static HotChocolate.Fusion.Logging.LogEntryHelper;

namespace HotChocolate.Fusion.PreMergeValidation.Rules;

/// <summary>
/// <para>
/// This rule ensures that enum types with the same name across different source schemas in a
/// composite schema have identical sets of values. Enums must be consistent across source schemas
/// to avoid conflicts and ambiguities in the composite schema.
/// </para>
/// <para>
/// When an enum is defined with differing values, it can lead to confusion and errors in query
/// execution. For instance, a value valid in one schema might be passed to another where it’s
/// unrecognized, leading to unexpected behavior or failures. This rule prevents such
/// inconsistencies by enforcing that all instances of the same named enum across schemas have an
/// exact match in their values.
/// </para>
/// </summary>
/// <seealso href="https://graphql.github.io/composite-schemas-spec/draft/#sec-Enum-Types-Inconsistent">
/// Specification
/// </seealso>
internal sealed class EnumTypesInconsistentRule : IEventHandler<EnumTypeGroupEvent>
{
    public void Handle(EnumTypeGroupEvent @event, CompositionContext context)
    {
        var (_, enumGroup) = @event;

        if (enumGroup.Length < 2)
        {
            return;
        }

        var enumValues = enumGroup
            .SelectMany(e => e.Type.Values)
            .Where(ValidationHelper.IsAccessible)
            .Select(v => v.Name)
            .ToImmutableHashSet();

        foreach (var (enumType, schema) in enumGroup)
        {
            foreach (var enumValue in enumValues)
            {
                if (!enumType.Values.ContainsName(enumValue))
                {
                    context.Log.Write(
                        EnumTypesInconsistent(enumType, enumValue, schema));
                }
            }
        }
    }
}
