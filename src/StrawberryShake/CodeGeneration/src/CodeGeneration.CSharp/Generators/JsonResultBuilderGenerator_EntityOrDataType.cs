using System.Text.Json;
using StrawberryShake.CodeGeneration.CSharp.Builders;
using StrawberryShake.CodeGeneration.Descriptors.TypeDescriptors;
using StrawberryShake.CodeGeneration.Extensions;
using static StrawberryShake.CodeGeneration.Utilities.NameUtils;

namespace StrawberryShake.CodeGeneration.CSharp.Generators;

public partial class JsonResultBuilderGenerator
{
    private void AddEntityOrDataTypeDeserializerMethod(
        ClassBuilder classBuilder,
        MethodBuilder methodBuilder,
        ComplexTypeDescriptor complexTypeDescriptor,
        HashSet<string> processed)
    {
        if (complexTypeDescriptor is InterfaceTypeDescriptor interfaceTypeDescriptor)
        {
            AddEntityDataTypeDeserializerToMethod(methodBuilder, interfaceTypeDescriptor);
        }
        else
        {
            throw new InvalidOperationException();
        }

        AddRequiredDeserializeMethods(complexTypeDescriptor, classBuilder, processed);
    }

    private void AddEntityDataTypeDeserializerToMethod(
        MethodBuilder methodBuilder,
        InterfaceTypeDescriptor interfaceTypeDescriptor)
    {
        methodBuilder.AddCode(
            AssignmentBuilder
                .New()
                .SetLeftHandSide($"var {Typename}")
                .SetRightHandSide(MethodCallBuilder
                    .Inline()
                    .SetMethodName(
                        Obj,
                        "Value",
                        nameof(JsonElement.GetProperty))
                    .AddArgument(WellKnownNames.TypeName.AsStringToken())
                    .Chain(x => x.SetMethodName(nameof(JsonElement.GetString)))));

        foreach (var concreteType in interfaceTypeDescriptor.ImplementedBy)
        {
            ICode builder;
            if (concreteType.IsEntity())
            {
                builder = CodeBlockBuilder
                    .New()
                    .AddCode(
                        AssignmentBuilder
                            .New()
                            .SetLeftHandSide($"{TypeNames.EntityId} {EntityId}")
                            .SetRightHandSide(
                                MethodCallBuilder
                                    .Inline()
                                    .SetMethodName(GetFieldName(IdSerializer), "Parse")
                                    .AddArgument($"{Obj}.Value")))
                    .AddCode(CreateUpdateEntityStatement(concreteType)
                        .AddCode(MethodCallBuilder
                            .New()
                            .SetReturn()
                            .SetNew()
                            .SetMethodName(TypeNames.EntityIdOrData)
                            .AddArgument(EntityId)));
            }
            else
            {
                builder =
                    MethodCallBuilder
                        .New()
                        .SetNew()
                        .SetReturn()
                        .SetMethodName(TypeNames.EntityIdOrData)
                        .AddArgument(CreateBuildDataStatement(concreteType)
                            .SetDetermineStatement(false)
                            .SetNew());
            }

            methodBuilder
                .AddEmptyLine()
                .AddCode(IfBuilder
                    .New()
                    .SetCondition(
                        $"typename?.Equals(\"{concreteType.Name}\", "
                        + $"{TypeNames.OrdinalStringComparison}) ?? false")
                    .AddCode(builder));
        }

        methodBuilder
            .AddEmptyLine()
            .AddCode(ExceptionBuilder.New(TypeNames.NotSupportedException));
    }
}
