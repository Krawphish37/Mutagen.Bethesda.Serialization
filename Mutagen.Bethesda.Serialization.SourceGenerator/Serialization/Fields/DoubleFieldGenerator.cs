using Microsoft.CodeAnalysis;
using Noggog.StructuredStrings;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Serialization.Fields;

public class DoubleFieldGenerator : PrimitiveFieldGenerator
{
    public new static readonly string[] AssociatedTypes = new string[]
    {
        "double",
    };
    
    public DoubleFieldGenerator()
        : base("Double", AssociatedTypes)
    {
    }

    // Compare bits: -0.0 is not the default, and every NaN payload counts as a value
    public override void GenerateForHasSerialize(CompilationUnit compilation, LoquiTypeSet obj, ITypeSymbol field, string? fieldName,
        string fieldAccessor, string? defaultValueAccessor, string metaAccessor, StructuredStringBuilder sb,
        CancellationToken cancel)
    {
        sb.AppendLine($"if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals({fieldAccessor}, {defaultValueAccessor ?? $"default({field})"})) return true;");
    }
}