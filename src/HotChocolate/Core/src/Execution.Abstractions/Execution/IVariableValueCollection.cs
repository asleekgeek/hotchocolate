using System.Diagnostics.CodeAnalysis;
using HotChocolate.Language;

namespace HotChocolate.Execution;

/// <summary>
/// Represents a collection of coerced variables.
/// </summary>
public interface IVariableValueCollection : IEnumerable<VariableValue>
{
    /// <summary>
    /// Specifies whether the collection is empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Gets a coerced variable value from the collection.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns></returns>
    /// <exception cref="GraphQLException">
    /// A GraphQL execution error is thrown when the
    /// requested variable cannot be found or cannot
    /// be converted to the request type.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// The <paramref name="name" /> mustn't be null or
    /// <see cref="string.Empty" />.
    /// </exception>
    T GetValue<T>(string name) where T : IValueNode;

    /// <summary>
    /// Tries to get a coerced variable value from the collection.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The coerced variable value.</param>
    /// <returns>
    /// <c>true</c> if a coerced variable exists and can be converted
    /// to the requested type; otherwise, <c>false</c> will be returned.
    /// </returns>
    bool TryGetValue<T>(string name, [NotNullWhen(true)] out T? value) where T : IValueNode;
}
