using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Configurations;
using HotChocolate.Utilities;

#nullable enable

namespace HotChocolate.Internal;

public static class TypeExtensionHelper
{
    public static void MergeInterfaceFields(
        ITypeCompletionContext context,
        IList<InterfaceFieldConfiguration> extensionFields,
        IList<InterfaceFieldConfiguration> typeFields)
    {
        MergeOutputFields(context, extensionFields, typeFields,
            (_, _, _) => { });
    }

    public static void MergeInputObjectFields(
        ITypeCompletionContext context,
        IList<InputFieldConfiguration> extensionFields,
        IList<InputFieldConfiguration> typeFields)
    {
        MergeFields(context, extensionFields, typeFields,
             (_, extensionField, typeField) =>
             {
                 if (extensionField.IsDeprecated)
                 {
                     typeField.DeprecationReason = extensionField.DeprecationReason;
                 }
             });
    }

    private static void MergeOutputFields<T>(
        ITypeCompletionContext context,
        IList<T> extensionFields,
        IList<T> typeFields,
        Action<IList<T>, T, T> action,
        Action<T>? onBeforeAdd = null)
        where T : OutputFieldConfiguration
    {
        MergeFields(context, extensionFields, typeFields,
            (fields, extensionField, typeField) =>
            {
                if (extensionField.IsDeprecated)
                {
                    typeField.DeprecationReason =
                        extensionField.DeprecationReason;
                }

                MergeFields(
                    context,
                    extensionField.Arguments,
                    typeField.Arguments,
                    (_, _, _) => { });

                action(fields, extensionField, typeField);
            },
            onBeforeAdd);
    }

    private static void MergeFields<T>(
        ITypeCompletionContext context,
        IList<T> extensionFields,
        IList<T> typeFields,
        Action<IList<T>, T, T> action,
        Action<T>? onBeforeAdd = null)
        where T : FieldConfiguration
    {
        foreach (var extensionField in extensionFields)
        {
            var typeField = typeFields.FirstOrDefault(
                t => t.Name.EqualsOrdinal(extensionField.Name));

            if (typeField is null)
            {
                onBeforeAdd?.Invoke(extensionField);
                typeFields.Add(extensionField);
            }
            else
            {
                MergeDirectives(
                    context,
                    extensionField.Directives,
                    typeField.Directives);

                MergeFeatures(extensionField, typeField);

                action(typeFields, extensionField, typeField);
            }
        }
    }

    public static void MergeDirectives(
        ITypeCompletionContext context,
        IList<DirectiveConfiguration> extension,
        IList<DirectiveConfiguration> type)
    {
        var directives = new List<(DirectiveType type, DirectiveConfiguration def)>();

        foreach (var directive in type)
        {
            if (context.TryGetDirectiveType(directive.Type, out var directiveType))
            {
                directives.Add((directiveType, directive));
            }
        }

        foreach (var directive in extension)
        {
            MergeDirective(context, directives, directive);
        }

        type.Clear();

        foreach (var directive in directives.Select(t => t.def))
        {
            type.Add(directive);
        }
    }

    private static void MergeDirective(
        ITypeCompletionContext context,
        IList<(DirectiveType type, DirectiveConfiguration def)> directives,
        DirectiveConfiguration directive)
    {
        if (context.TryGetDirectiveType(directive.Type, out var directiveType))
        {
            if (directiveType.IsRepeatable)
            {
                directives.Add((directiveType, directive));
            }
            else
            {
                var entry = directives.FirstOrDefault(t => t.type == directiveType);
                if (entry == default)
                {
                    directives.Add((directiveType, directive));
                }
                else
                {
                    var index = directives.IndexOf(entry);
                    directives[index] = (directiveType, directive);
                }
            }
        }
    }

    public static void MergeFeatures(
        TypeSystemConfiguration extension,
        TypeSystemConfiguration type)
    {
        if (!extension.GetFeatures().IsEmpty)
        {
            foreach (var feature in extension.GetFeatures())
            {
                type.Features[feature.Key] = feature.Value;
            }
        }
    }

    public static void MergeInterfaces(
        ObjectTypeConfiguration extension,
        ObjectTypeConfiguration type)
    {
        if (extension.GetInterfaces().Count > 0)
        {
            foreach (var interfaceReference in extension.GetInterfaces())
            {
                type.Interfaces.Add(interfaceReference);
            }
        }

        if (extension.FieldBindingType != null
            && extension.FieldBindingType != typeof(object))
        {
            type.KnownRuntimeTypes.Add(extension.FieldBindingType);
        }
    }

    public static void MergeTypes(
        ICollection<TypeReference> extensionTypes,
        ICollection<TypeReference> typeTypes)
    {
        var set = new HashSet<TypeReference>(typeTypes);

        foreach (var reference in extensionTypes)
        {
            if (set.Add(reference))
            {
                typeTypes.Add(reference);
            }
        }
    }

    public static void MergeConfigurations(
        ICollection<ITypeSystemConfigurationTask> extensionConfigurations,
        ICollection<ITypeSystemConfigurationTask> typeConfigurations)
    {
        foreach (var configuration in extensionConfigurations)
        {
            typeConfigurations.Add(configuration);
        }
    }
}
