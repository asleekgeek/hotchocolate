using HotChocolate.Features;
using HotChocolate.Language;
using HotChocolate.Utilities;
using static HotChocolate.Serialization.SchemaDebugFormatter;

namespace HotChocolate.Types.Mutable;

/// <summary>
/// Represents a GraphQL scalar type definition.
/// </summary>
public class MutableScalarTypeDefinition(string name)
    : INamedTypeSystemMemberDefinition<MutableScalarTypeDefinition>
    , IScalarTypeDefinition
    , IMutableTypeDefinition
    , IFeatureProvider
{
    private string _name = name.EnsureGraphQLName();
    private DirectiveCollection? _directives;
    private IFeatureCollection? _features;

    /// <inheritdoc />
    public TypeKind Kind => TypeKind.Scalar;

    /// <inheritdoc cref="IMutableTypeDefinition.Name" />
    public string Name
    {
        get => _name;
        set => _name = value.EnsureGraphQLName();
    }

    /// <inheritdoc cref="IMutableTypeDefinition.Description" />
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this scalar type is a spec scalar.
    /// </summary>
    public bool IsSpecScalar { get; set; }

    public DirectiveCollection Directives
        => _directives ??= new DirectiveCollection();

    IReadOnlyDirectiveCollection IDirectivesProvider.Directives
        => _directives ?? EmptyCollections.Directives;

    /// <inheritdoc />
    public IFeatureCollection Features
        => _features ??= new FeatureCollection();

    /// <summary>
    /// Gets the string representation of this instance.
    /// </summary>
    /// <returns>
    /// The string representation of this instance.
    /// </returns>
    public override string ToString()
        => Format(this).ToString(true);

    /// <summary>
    /// Creates a <see cref="ScalarTypeDefinitionNode"/> from a <see cref="MutableScalarTypeDefinition"/>.
    /// </summary>
    public ScalarTypeDefinitionNode ToSyntaxNode() => Format(this);

    ISyntaxNode ISyntaxNodeProvider.ToSyntaxNode() => Format(this);

    /// <inheritdoc />
    public bool Equals(IType? other)
        => Equals(other, TypeComparison.Reference);

    /// <inheritdoc />
    public bool Equals(IType? other, TypeComparison comparison)
    {
        if (comparison is TypeComparison.Reference)
        {
            return ReferenceEquals(this, other);
        }

        return other is MutableScalarTypeDefinition otherScalar
            && otherScalar.Name.Equals(Name, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public bool IsAssignableFrom(ITypeDefinition type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (type.Kind == TypeKind.Scalar)
        {
            return Equals(type, TypeComparison.Reference);
        }

        return false;
    }

    /// <summary>
    /// Creates a new instance of <see cref="MutableScalarTypeDefinition"/>.
    /// </summary>
    /// <param name="name">
    /// The name of the scalar type definition.
    /// </param>
    /// <returns>
    /// Returns a new instance of <see cref="MutableScalarTypeDefinition"/>.
    /// </returns>
    public static MutableScalarTypeDefinition Create(string name) => new(name);
}
