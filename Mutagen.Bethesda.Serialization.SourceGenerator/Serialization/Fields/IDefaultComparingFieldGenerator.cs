using Microsoft.CodeAnalysis;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Serialization.Fields;

/// <summary>
/// A field generator whose written value is compared with a default and left out when equal (unless the writer kernel
/// writes defaults). The generated FieldDefaults of a class lists these fields (C59, S124).
/// </summary>
public interface IDefaultComparingFieldGenerator
{
    /// <summary>
    /// The C# expression of the value the field's written value is compared with: exactly the default its
    /// GenerateForSerialize passes to the kernel
    /// </summary>
    string ComparedDefault(ITypeSymbol field, string? defaultValueAccessor);
}
