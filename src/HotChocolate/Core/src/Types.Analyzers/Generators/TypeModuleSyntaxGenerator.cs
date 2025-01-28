using System.Collections.Immutable;
using HotChocolate.Types.Analyzers.FileBuilders;
using HotChocolate.Types.Analyzers.Helpers;
using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using TypeInfo = HotChocolate.Types.Analyzers.Models.TypeInfo;

namespace HotChocolate.Types.Analyzers.Generators;

public sealed class TypeModuleSyntaxGenerator : ISyntaxGenerator
{
    public void Generate(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<SyntaxInfo> syntaxInfos)
        => Execute(context, compilation, syntaxInfos);

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<SyntaxInfo> syntaxInfos)
    {
        if (syntaxInfos.IsEmpty)
        {
            return;
        }

        var module = syntaxInfos.GetModuleInfo(compilation.AssemblyName, out var defaultModule);

        // the generator is disabled.
        if (module.Options == ModuleOptions.Disabled)
        {
            return;
        }

        // if there is only the module info we do not need to generate a module.
        if (!defaultModule && syntaxInfos.Length == 1)
        {
            return;
        }

        var syntaxInfoList = syntaxInfos.ToList();
        WriteOperationTypes(context, syntaxInfoList, module);
        WriteConfiguration(context, syntaxInfoList, module);
    }

    private static void WriteConfiguration(
        SourceProductionContext context,
        List<SyntaxInfo> syntaxInfos,
        ModuleInfo module)
    {
        var dataLoaderDefaults = syntaxInfos.GetDataLoaderDefaults();
        HashSet<(string InterfaceName, string ClassName)>? groups = null;
        using var generator = new ModuleFileBuilder(module.ModuleName, "Microsoft.Extensions.DependencyInjection");

        generator.WriteHeader();
        generator.WriteBeginNamespace();
        generator.WriteBeginClass();
        generator.WriteBeginRegistrationMethod();

        var operations = OperationType.No;
        var hasConfigurations = false;
        List<string>? _objectTypeExtensions = null;
        List<string>? _interfaceTypeExtensions = null;

        foreach (var syntaxInfo in syntaxInfos.OrderBy(s => s.OrderByKey))
        {
            if (syntaxInfo.Diagnostics.Length > 0)
            {
                continue;
            }

            switch (syntaxInfo)
            {
                case TypeInfo type:
                    if ((module.Options & ModuleOptions.RegisterTypes) == ModuleOptions.RegisterTypes)
                    {
                        generator.WriteRegisterType(type.Name);
                        hasConfigurations = true;
                    }
                    break;

                case TypeExtensionInfo extension:
                    if ((module.Options & ModuleOptions.RegisterTypes) == ModuleOptions.RegisterTypes)
                    {
                        generator.WriteRegisterTypeExtension(extension.Name, extension.IsStatic);
                        hasConfigurations = true;

                        if (extension.Type is not OperationType.No && (operations & extension.Type) != extension.Type)
                        {
                            operations |= extension.Type;
                        }
                    }
                    break;

                case RegisterDataLoaderInfo dataLoader:
                    if ((module.Options & ModuleOptions.RegisterDataLoader) == ModuleOptions.RegisterDataLoader)
                    {
                        generator.WriteRegisterDataLoader(dataLoader.Name);
                        hasConfigurations = true;
                    }
                    break;

                case DataLoaderInfo dataLoader:
                    if ((module.Options & ModuleOptions.RegisterDataLoader) == ModuleOptions.RegisterDataLoader)
                    {
                        var typeName = $"{dataLoader.Namespace}.{dataLoader.Name}";
                        var interfaceTypeName = $"{dataLoader.Namespace}.{dataLoader.InterfaceName}";

                        generator.WriteRegisterDataLoader(
                            typeName,
                            interfaceTypeName,
                            dataLoaderDefaults.GenerateInterfaces);
                        hasConfigurations = true;

                        if (dataLoader.Groups.Count > 0)
                        {
                            groups ??= [];
                            foreach (var groupName in dataLoader.Groups)
                            {
                                groups.Add((
                                    $"{dataLoader.Namespace}.I{groupName}",
                                    $"{dataLoader.Namespace}.{groupName}"));
                            }
                        }
                    }
                    break;

                case OperationRegistrationInfo operation:
                    if ((module.Options & ModuleOptions.RegisterTypes) == ModuleOptions.RegisterTypes)
                    {
                        generator.WriteRegisterTypeExtension(operation.TypeName, false);
                        hasConfigurations = true;

                        if (operation.Type is not OperationType.No && (operations & operation.Type) != operation.Type)
                        {
                            operations |= operation.Type;
                        }
                    }
                    break;

                case ObjectTypeExtensionInfo objectTypeExtension:
                    if ((module.Options & ModuleOptions.RegisterTypes) == ModuleOptions.RegisterTypes
                        && objectTypeExtension.Diagnostics.Length == 0)
                    {
                        _objectTypeExtensions ??= [];
                        _objectTypeExtensions.Add(objectTypeExtension.RuntimeType.ToFullyQualified());

                        generator.WriteRegisterTypeExtension(
                            GetAssemblyQualifiedName(objectTypeExtension.Type),
                            objectTypeExtension.RuntimeType.ToFullyQualified(),
                            objectTypeExtension.Type.ToFullyQualified());
                        hasConfigurations = true;
                    }
                    break;

                case InterfaceTypeExtensionInfo interfaceType:
                    if ((module.Options & ModuleOptions.RegisterTypes) == ModuleOptions.RegisterTypes
                        && interfaceType.Diagnostics.Length == 0)
                    {
                        _interfaceTypeExtensions ??= [];
                        _interfaceTypeExtensions.Add(interfaceType.RuntimeType.ToFullyQualified());

                        generator.WriteRegisterTypeExtension(
                            GetAssemblyQualifiedName(interfaceType.Type),
                            interfaceType.RuntimeType.ToFullyQualified(),
                            interfaceType.Type.ToFullyQualified());
                        hasConfigurations = true;
                    }
                    break;

                case RootTypeExtensionInfo rootType:
                    if ((module.Options & ModuleOptions.RegisterTypes) == ModuleOptions.RegisterTypes
                        && rootType.Diagnostics.Length == 0)
                    {
                        var operationType = rootType.OperationType;

                        generator.WriteRegisterRootTypeExtension(
                            GetAssemblyQualifiedName(rootType.Type),
                            operationType,
                            rootType.Type.ToFullyQualified());
                        hasConfigurations = true;

                        if (operationType is not OperationType.No && (operations & operationType) != operationType)
                        {
                            operations |= operationType;
                        }
                    }
                    break;
            }
        }

        if ((operations & OperationType.Query) == OperationType.Query)
        {
            generator.WriteTryAddOperationType(OperationType.Query);
            hasConfigurations = true;
        }

        if ((operations & OperationType.Mutation) == OperationType.Mutation)
        {
            generator.WriteTryAddOperationType(OperationType.Mutation);
            hasConfigurations = true;
        }

        if ((operations & OperationType.Subscription) == OperationType.Subscription)
        {
            generator.WriteTryAddOperationType(OperationType.Subscription);
            hasConfigurations = true;
        }

        if (groups is not null)
        {
            foreach (var (interfaceName, className) in groups.OrderBy(t => t.ClassName))
            {
                generator.WriteRegisterDataLoaderGroup(className, interfaceName);
            }
        }

        if (_objectTypeExtensions is not null)
        {
            foreach (var type in _objectTypeExtensions)
            {
                generator.WriteEnsureObjectTypeExtensionIsRegistered(type);
            }
        }

        if (_interfaceTypeExtensions is not null)
        {
            foreach (var type in _interfaceTypeExtensions)
            {
                generator.WriteEnsureInterfaceTypeExtensionIsRegistered(type);
            }
        }

        generator.WriteEndRegistrationMethod();
        generator.WriteEndClass();
        generator.WriteEndNamespace();

        if (hasConfigurations)
        {
            context.AddSource(WellKnownFileNames.TypeModuleFile, generator.ToSourceText());
        }
    }

    private static void WriteOperationTypes(
        SourceProductionContext context,
        List<SyntaxInfo> syntaxInfos,
        ModuleInfo module)
    {
        var operations = new List<OperationInfo>();

        foreach (var syntaxInfo in syntaxInfos)
        {
            if (syntaxInfo is OperationInfo operation)
            {
                operations.Add(operation);
            }
        }

        if (operations.Count == 0)
        {
            return;
        }

        using var generator = new OperationFieldFileBuilder();
        generator.WriteHeader();
        generator.WriteBeginNamespace("Microsoft.Extensions.DependencyInjection");

        foreach (var group in operations.GroupBy(t => t.Type))
        {
            var typeName = $"{module.ModuleName}{group.Key}Type";

            generator.WriteBeginClass(typeName);
            generator.WriteConfigureMethod(group.Key, group);
            generator.WriteEndClass();

            syntaxInfos.Add(new OperationRegistrationInfo(
                group.Key,
                $"Microsoft.Extensions.DependencyInjection.{typeName}"));
        }

        generator.WriteEndNamespace();

        context.AddSource(WellKnownFileNames.RootTypesFile, generator.ToSourceText());
    }

    public static string GetAssemblyQualifiedName(ITypeSymbol typeSymbol)
    {
        var assemblyName = typeSymbol.ContainingAssembly?.Name ?? "UnknownAssembly";
        var typeFullName = typeSymbol.ToDisplayString();
        return $"{assemblyName}::{typeFullName}";
    }
}
