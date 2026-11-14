using Microsoft.CodeAnalysis;
using Noggog.StructuredStrings;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Serialization.Fields;

/// <summary>
/// System.Half fields (C222; Starfield AreaLight.Unused, TraversalReference.Unknown): written and read with the
/// kernel's WriteHalf/ReadHalf, bit for bit like floats
/// </summary>
public class HalfFieldGenerator : PrimitiveFieldGenerator
{
    public new static readonly string[] AssociatedTypes = new string[]
    {
        "System.Half",
        "Half",
    };

    public HalfFieldGenerator()
        : base("Half", AssociatedTypes)
    {
    }

    // Compare bits: -0 is not the default, and every NaN payload counts as a value
    public override void GenerateForHasSerialize(CompilationUnit compilation, LoquiTypeSet obj, ITypeSymbol field, string? fieldName,
        string fieldAccessor, string? defaultValueAccessor, string metaAccessor, StructuredStringBuilder sb,
        CancellationToken cancel)
    {
        sb.AppendLine($"if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals({fieldAccessor}, {defaultValueAccessor ?? $"default({field})"})) return true;");
    }
}
