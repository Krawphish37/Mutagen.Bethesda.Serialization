using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Mutagen.Bethesda.Serialization.SourceGenerator.Customizations;
using Noggog;
using Noggog.StructuredStrings;
using Noggog.StructuredStrings.CSharp;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Serialization;

/// <summary>
/// Writes, for each mod type a bootstrap is called with, a public registry of the serializers generated for that mod
/// and every object it holds, by runtime type (Mutagen.Bethesda.Serialization.ISerializationRegistry; C59, W11). The
/// per-object classes stay internal and generic; the registry is the way to them for a caller holding an object of
/// a type it does not know at compile time, e.g. an IMajorRecordGetter.
/// </summary>
public class RegistryGenerator
{
    private readonly RelatedObjectAccumulator _accumulator;
    private readonly LoquiNameRetriever _nameRetriever;
    private readonly LoquiSerializationNaming _serializationNaming;
    private readonly ObjectTypeTester _objectTypeTester;
    private readonly Blacklist _blacklist;

    public RegistryGenerator(
        RelatedObjectAccumulator accumulator,
        LoquiNameRetriever nameRetriever,
        LoquiSerializationNaming serializationNaming,
        ObjectTypeTester objectTypeTester,
        Blacklist blacklist)
    {
        _accumulator = accumulator;
        _nameRetriever = nameRetriever;
        _serializationNaming = serializationNaming;
        _objectTypeTester = objectTypeTester;
        _blacklist = blacklist;
    }

    public void Initialize(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<Bootstrapping.BootstrapInvocation> modBootstrapInvocations,
        IncrementalValueProvider<LoquiMapping> mappings,
        IncrementalValueProvider<CustomizationSpecifications> customization)
    {
        var modTypes = modBootstrapInvocations.Collect()
            .SelectMany((all, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                return all
                    .Select(x => x.ObjectRegistration)
                    .NotNull()
                    .Where(x => _objectTypeTester.IsModObject(x))
                    .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            });
        context.RegisterSourceOutput(
            modTypes.Combine(mappings).Combine(customization),
            (c, i) => Generate(c, i.Left.Left, i.Left.Right, i.Right));
    }

    /// <summary>The registry's class name for a mod type: StarfieldMod gives StarfieldSerializationRegistry</summary>
    public string RegistryName(ITypeSymbol modType)
    {
        var name = _nameRetriever.GetNames(modType).Direct;
        if (name.EndsWith("Mod") && name.Length > 3) name = name.Substring(0, name.Length - 3);
        return $"{name}SerializationRegistry";
    }

    /// <summary>The braces of a collection initializer: "{", the items one level in, "};"</summary>
    private static IDisposable Initializer(StructuredStringBuilder sb)
    {
        sb.AppendLine("{");
        var depth = sb.IncreaseDepth();
        return new EndInitializer(sb, depth);
    }

    private sealed class EndInitializer : IDisposable
    {
        private readonly StructuredStringBuilder _sb;
        private readonly IDisposable _depth;

        public EndInitializer(StructuredStringBuilder sb, IDisposable depth)
        {
            _sb = sb;
            _depth = depth;
        }

        public void Dispose()
        {
            _depth.Dispose();
            _sb.AppendLine("};");
        }
    }

    private static string Full(ITypeSymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static bool IsGeneric(ITypeSymbol? symbol) => symbol is INamedTypeSymbol { TypeArguments.Length: > 0 };

    /// <summary>
    /// The objects with a generated Serialize and Deserialize of the plain shape: concrete, not generic, not a mod or a
    /// group (a mod's and a group's serializers take other arguments)
    /// </summary>
    private bool Registrable(LoquiTypeSet typeSet)
    {
        if (_blacklist.ShouldSkip(typeSet)) return false;
        if (typeSet.Direct is not INamedTypeSymbol { IsAbstract: false } direct) return false;
        if (typeSet.Getter == null) return false;
        if (IsGeneric(direct) || IsGeneric(typeSet.Getter) || IsGeneric(typeSet.Setter)) return false;
        if (_objectTypeTester.IsModObject(typeSet.Getter)) return false;
        if (_objectTypeTester.IsGroup(typeSet.Setter) || _objectTypeTester.IsGroup(direct)) return false;
        return true;
    }

    public void Generate(
        SourceProductionContext context,
        INamedTypeSymbol modType,
        LoquiMapping mapping,
        CustomizationSpecifications customization)
    {
        var cancel = context.CancellationToken;
        var objects = _accumulator.GetRelatedObjects(mapping, modType, customization, cancel)
            .Where(Registrable)
            .Select(t => (TypeSet: t, Direct: Full(t.Direct!)))
            .OrderBy(t => t.Direct, StringComparer.Ordinal)
            .ToArray();

        var registryName = RegistryName(modType);
        var modNames = _nameRetriever.GetNames(modType);

        var sb = new StructuredStringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Mutagen.Bethesda.Serialization;");
        sb.AppendLine("using Mutagen.Bethesda.Serialization.Streams;");
        sb.AppendLine();
        sb.AppendLine("#pragma warning disable CS0618 // Obsolete");
        sb.AppendLine();
        using (sb.Namespace(modType.ContainingNamespace.ToString()))
        {
        }

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// The serializers generated into this assembly for {modNames.Direct} and every object it holds ({objects.Length} classes), by runtime type.");
        sb.AppendLine("/// See <see cref=\"ISerializationRegistry\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {registryName} : ISerializationRegistry");
        using (sb.CurlyBrace())
        {
            sb.AppendLine($"public static readonly {registryName} Instance = new();");
            sb.AppendLine();
            sb.AppendLine($"private {registryName}()");
            using (sb.CurlyBrace())
            {
            }
            sb.AppendLine();

            sb.AppendLine("private static readonly Type[] Classes =");
            using (Initializer(sb))
            {
                foreach (var o in objects)
                {
                    sb.AppendLine($"typeof({o.Direct}),");
                }
            }
            sb.AppendLine();

            // The class, its getter and its setter interface, each to the class
            sb.AppendLine("private static readonly Dictionary<Type, Type> ClassOf = new()");
            using (Initializer(sb))
            {
                foreach (var o in objects)
                {
                    foreach (var t in new[] { o.TypeSet.Direct, o.TypeSet.Getter, o.TypeSet.Setter }
                                 .NotNull()
                                 .Select(Full)
                                 .Distinct())
                    {
                        sb.AppendLine($"[typeof({t})] = typeof({o.Direct}),");
                    }
                }
            }
            sb.AppendLine();

            sb.AppendLine("private static readonly Dictionary<string, Type> ByName = SerializationRegistry.NamesOf(Classes);");
            sb.AppendLine();

            sb.AppendLine("/// <inheritdoc />");
            sb.AppendLine("public IReadOnlyCollection<Type> ClassTypes => Classes;");
            sb.AppendLine();

            sb.AppendLine("/// <inheritdoc />");
            sb.AppendLine("public bool TryGetClassType(Type type, [MaybeNullWhen(false)] out Type classType) => ClassOf.TryGetValue(type, out classType);");
            sb.AppendLine();

            sb.AppendLine("/// <inheritdoc />");
            sb.AppendLine("public bool TryGetClassType(string name, [MaybeNullWhen(false)] out Type classType) => ByName.TryGetValue(name, out classType);");
            sb.AppendLine();

            sb.AppendLine("private static class Writers<TKernel, TWriteObject>");
            using (sb.IncreaseDepth())
            {
                sb.AppendLine("where TKernel : ISerializationWriterKernel<TWriteObject>, new()");
                sb.AppendLine("where TWriteObject : IContainStreamPackage");
            }
            using (sb.CurlyBrace())
            {
                sb.AppendLine("public static readonly Dictionary<Type, Func<TWriteObject, object, MutagenSerializationWriterKernel<TKernel, TWriteObject>, SerializationMetaData, Task>> Map = new()");
                using (Initializer(sb))
                {
                    foreach (var o in objects)
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (!_serializationNaming.TryGetSerializationItems(o.TypeSet.GetAny(), out var items)) continue;
                        sb.AppendLine($"[typeof({o.Direct})] = static (w, i, k, m) => global::{items.SerializationCall()}<TKernel, TWriteObject>(w, ({Full(o.TypeSet.SerializeSymbol)})i, k, m),");
                    }
                }
            }
            sb.AppendLine();

            sb.AppendLine("private static class Readers<TReadObject>");
            using (sb.IncreaseDepth())
            {
                sb.AppendLine("where TReadObject : IContainStreamPackage");
            }
            using (sb.CurlyBrace())
            {
                sb.AppendLine("public static readonly Dictionary<Type, Func<TReadObject, ISerializationReaderKernel<TReadObject>, SerializationMetaData, Task<object>>> Map = new()");
                using (Initializer(sb))
                {
                    foreach (var o in objects)
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (!_serializationNaming.TryGetSerializationItems(o.TypeSet.GetAny(), out var items)) continue;
                        sb.AppendLine($"[typeof({o.Direct})] = static (r, k, m) => SerializationRegistry.AsObject(global::{items.DeserializationCall()}<TReadObject>(r, k, m)),");
                    }
                }
            }
            sb.AppendLine();

            sb.AppendLine("/// <inheritdoc />");
            sb.AppendLine("public Task Serialize<TKernel, TWriteObject>(TWriteObject writer, object item, MutagenSerializationWriterKernel<TKernel, TWriteObject> kernel, SerializationMetaData metaData)");
            using (sb.IncreaseDepth())
            {
                sb.AppendLine("where TKernel : ISerializationWriterKernel<TWriteObject>, new()");
                sb.AppendLine("where TWriteObject : IContainStreamPackage");
            }
            using (sb.CurlyBrace())
            {
                sb.AppendLine("var type = SerializationRegistry.ClassTypeOf(item);");
                sb.AppendLine("if (!Writers<TKernel, TWriteObject>.Map.TryGetValue(type, out var write))");
                using (sb.CurlyBrace())
                {
                    sb.AppendLine("throw SerializationRegistry.NotRegistered(type, this);");
                }
                sb.AppendLine("return write(writer, item, kernel, metaData);");
            }
            sb.AppendLine();

            sb.AppendLine("/// <inheritdoc />");
            sb.AppendLine("public Task<object> Deserialize<TReadObject>(TReadObject reader, Type type, ISerializationReaderKernel<TReadObject> kernel, SerializationMetaData metaData)");
            using (sb.IncreaseDepth())
            {
                sb.AppendLine("where TReadObject : IContainStreamPackage");
            }
            using (sb.CurlyBrace())
            {
                sb.AppendLine("if (!ClassOf.TryGetValue(type, out var classType)");
                sb.AppendLine("    || !Readers<TReadObject>.Map.TryGetValue(classType, out var read))");
                using (sb.CurlyBrace())
                {
                    sb.AppendLine("throw SerializationRegistry.NotRegistered(type, this);");
                }
                sb.AppendLine("return read(reader, kernel, metaData);");
            }
        }
        sb.AppendLine();

        context.AddSource($"{registryName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
