using System.Reflection;
using Microsoft.CodeAnalysis;
using Mutagen.Bethesda.Serialization.Tests.SourceGenerators;
using Shouldly;
using Xunit.Abstractions;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Tests.Bootstrap;

/// <summary>The generator run once on the whole Starfield mod (with its mix-ins), as a consumer's build runs it</summary>
public sealed class GeneratedStarfield
{
    public IReadOnlyDictionary<string, string> Sources { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public GeneratedStarfield()
    {
        var source = @"
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda.Plugins;

namespace Mutagen.Bethesda.Serialization.Tests.SerializationTests;

public class SerializationTests
{
    public void Trigger(IStarfieldModGetter mod)
    {
        MutagenJsonConverter.Instance.Serialize(mod, ""x"");
    }
}";
        var result = TestHelper.RunSourceGenerator(source);
        Diagnostics = result.Diagnostics;
        Sources = result.Results
            .SelectMany(r => r.GeneratedSources)
            .ToDictionary(s => s.HintName, s => s.SourceText.ToString());
    }

    public string Get(string hintName)
    {
        Sources.TryGetValue(hintName, out var text).ShouldBeTrue(hintName);
        return text!;
    }
}

public class GeneratedStarfieldTests : IClassFixture<GeneratedStarfield>
{
    private readonly GeneratedStarfield _gen;

    private readonly ITestOutputHelper _output;

    public GeneratedStarfieldTests(GeneratedStarfield gen, ITestOutputHelper output)
    {
        _gen = gen;
        _output = output;
    }

    [Fact]
    public void NoErrors()
    {
        _gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        _gen.Sources.Count.ShouldBeGreaterThan(1000);
    }

    // C59 W14: the notes are written and read under the public RoundTripNoteNames, never under a literal of their own
    [Fact]
    public void NotesUseThePublicNames()
    {
        var majorRecord = _gen.Get("MajorRecord_Serializations.g.cs");
        foreach (var name in new[]
                 {
                     nameof(RoundTripNoteNames.HeaderFormIdAsStored), nameof(RoundTripNoteNames.OwnLinkFormsAsStored),
                     nameof(RoundTripNoteNames.GroupLabelFormsAsStored), nameof(RoundTripNoteNames.SubrecordOrder),
                     nameof(RoundTripNoteNames.OriginalCompressedData),
                 })
        {
            majorRecord.ShouldContain($"case Mutagen.Bethesda.Serialization.RoundTripNoteNames.{name}:");
            majorRecord.ShouldContain($"kernel.WriteString(writer, Mutagen.Bethesda.Serialization.RoundTripNoteNames.{name},");
        }
        _gen.Get("Cell_Serializations.g.cs").ShouldContain("RoundTripNoteNames.ChildGroupLayout");
        var mod = _gen.Get("StarfieldMod_Serializations.g.cs");
        foreach (var name in RoundTripNoteNames.ModNotes)
        {
            mod.ShouldContain($"case Mutagen.Bethesda.Serialization.RoundTripNoteNames.{name}:");
        }
        foreach (var source in _gen.Sources.Values)
        {
            foreach (var name in RoundTripNoteNames.All)
            {
                source.ShouldNotContain($"\"{name}\"");
            }
        }
    }

    // C59 W13: a package writes and reads its ProcedureTreeDataOrder note
    [Fact]
    public void PackageCarriesItsOrder()
    {
        var package = _gen.Get("Package_Serializations.g.cs");
        package.ShouldContain("RoundTripNotesHelper.ProcedureTreeDataOrderToString(Mutagen.Bethesda.Starfield.PackageMixIn.GetProcedureTreeDataOrder(item))");
        package.ShouldContain("kernel.WriteString(writer, Mutagen.Bethesda.Serialization.RoundTripNoteNames.ProcedureTreeDataOrder,");
        package.ShouldContain("case Mutagen.Bethesda.Serialization.RoundTripNoteNames.ProcedureTreeDataOrder:");
        package.ShouldContain("procedureTreeDataOrderPackage.ProcedureTreeDataOrder = procedureTreeDataOrder;");
    }

    // C59 W11: one public registry for the mod, by runtime type, over the internal per-class serializers
    [Fact]
    public void RegistryForTheMod()
    {
        var registry = _gen.Get("StarfieldSerializationRegistry.g.cs");
        registry.ShouldContain("public sealed class StarfieldSerializationRegistry : ISerializationRegistry");
        registry.ShouldContain("[typeof(global::Mutagen.Bethesda.Starfield.Weapon)] = static (w, i, k, m) => global::Mutagen.Bethesda.Starfield.Weapon_Serialization.Serialize<TKernel, TWriteObject>(w, (global::Mutagen.Bethesda.Starfield.IWeaponGetter)i, k, m),");
        registry.ShouldContain("[typeof(global::Mutagen.Bethesda.Starfield.Weapon)] = static (r, k, m) => SerializationRegistry.AsObject(global::Mutagen.Bethesda.Starfield.Weapon_Serialization.Deserialize<TReadObject>(r, k, m)),");
        registry.ShouldContain("[typeof(global::Mutagen.Bethesda.Starfield.IWeaponGetter)] = typeof(global::Mutagen.Bethesda.Starfield.Weapon),");
        // Sub-objects too; no mod, group or abstract class
        registry.ShouldContain("[typeof(global::Mutagen.Bethesda.Starfield.PackageDataBool)] = static (w");
        registry.ShouldNotContain("typeof(global::Mutagen.Bethesda.Starfield.StarfieldMod)");
        registry.ShouldNotContain("typeof(global::Mutagen.Bethesda.Starfield.StarfieldMajorRecord)");
        registry.ShouldNotContain("Group<");
        registry.ShouldNotContain("typeof(global::Mutagen.Bethesda.Plugins.Records.MajorRecord)");
        // Each class it lists has its generated serializer file
        foreach (var line in registry.Split('\n').Where(l => l.Contains("_Serialization.Serialize<TKernel, TWriteObject>")))
        {
            var start = line.IndexOf("global::", line.IndexOf("=>")) + "global::".Length;
            var end = line.IndexOf("_Serialization.Serialize<", start);
            var cls = line.Substring(start, end - start);
            _gen.Sources.ContainsKey(cls.Substring(cls.LastIndexOf('.') + 1) + "_Serializations.g.cs").ShouldBeTrue(cls);
        }
    }

    // C59 W18: a sub-object whose fields are all default and an empty list are left out unless the kernel writes defaults
    [Fact]
    public void GuardsAskTheKernelAboutDefaults()
    {
        var all = string.Join("\n", _gen.Sources.Values);
        all.ShouldContain("(kernel.WriteDefaults || ");
        all.ShouldContain(".Count > 0)");
        foreach (var line in all.Split('\n').Where(l => l.Contains(".Count > 0)") && l.TrimStart().StartsWith("&& checked")))
        {
            line.ShouldContain("kernel.WriteDefaults || ");
        }
    }

    // C59 S124: each class lists the value each field is left out at, with Mutagen's own defaults, merged with its base's
    [Fact]
    public void FieldDefaultsAreTheComparedDefaults()
    {
        var book = _gen.Get("BookTeachesNothing_Serializations.g.cs");
        book.ShouldContain("ret[\"RawContent\"] = Mutagen.Bethesda.Starfield.BookTeachesNothing.RawContentDefault;");
        book.ShouldContain("kernel.WriteUInt32(writer, \"RawContent\", item.RawContent, Mutagen.Bethesda.Starfield.BookTeachesNothing.RawContentDefault);");
        _gen.Get("Weapon_Serializations.g.cs").ShouldContain("var ret = Mutagen.Bethesda.Starfield.StarfieldMajorRecord_Serialization.BuildFieldDefaults();");
        _gen.Get("StarfieldMajorRecord_Serializations.g.cs").ShouldNotContain("ret[\"FormVersion\"]");
        _gen.Get("StarfieldSerializationRegistry.g.cs").ShouldContain("[typeof(global::Mutagen.Bethesda.Starfield.BookTeachesNothing)] = static () => global::Mutagen.Bethesda.Starfield.BookTeachesNothing_Serialization.FieldDefaults,");
    }

    // S151: the default of a generic class's field (ObjectTemplate<T>.AddonIndex = -1) is compared with and listed
    [Fact]
    public void GenericClassDefaults()
    {
        var template = _gen.Get("ObjectTemplate_Serializations.g.cs");
        template.ShouldContain("kernel.WriteInt16(writer, \"AddonIndex\", item.AddonIndex, Mutagen.Bethesda.Starfield.ObjectTemplate<T>.AddonIndexDefault);");
        var fieldDefaults = template.Split('\n').FirstOrDefault(l => l.Contains("FieldDefaults<T>()"));
        _output.WriteLine(fieldDefaults ?? "(no FieldDefaults<T>)");
        fieldDefaults.ShouldNotBeNull();
        fieldDefaults.ShouldContain("public static IReadOnlyDictionary<string, object?> FieldDefaults<T>() where T :");
        fieldDefaults.ShouldContain("=> FieldDefaultsOf<T>.Value;");
        template.ShouldContain("ret[\"AddonIndex\"] = Mutagen.Bethesda.Starfield.ObjectTemplate<T>.AddonIndexDefault;");
        var registry = _gen.Get("StarfieldSerializationRegistry.g.cs");
        registry.ShouldContain("[typeof(global::Mutagen.Bethesda.Starfield.ObjectTemplate<global::Mutagen.Bethesda.Starfield.Weapon.Property>)] = static () => global::Mutagen.Bethesda.Starfield.ObjectTemplate_Serialization.FieldDefaults<global::Mutagen.Bethesda.Starfield.Weapon.Property>(),");
        registry.ShouldContain("[typeof(global::Mutagen.Bethesda.Starfield.ObjectTemplate<global::Mutagen.Bethesda.Starfield.Weapon.Property>)] = static (w, i, k, m) => global::Mutagen.Bethesda.Starfield.ObjectTemplate_Serialization.Serialize<TKernel, TWriteObject, global::Mutagen.Bethesda.Starfield.Weapon.Property>(w, (global::Mutagen.Bethesda.Starfield.IObjectTemplateGetter<global::Mutagen.Bethesda.Starfield.Weapon.Property>)i, k, m),");
    }

    // S151 census: every Mutagen Starfield class field with a {Name}Default of its own (48 in fixes.19, -1, 1, 0.5f,
    // uint.MaxValue, ...) is compared with that default where the generated code writes it, and listed in FieldDefaults
    // with it; none falls back to default(T)
    [Fact]
    public void EveryOwnDefaultIsComparedAndListed()
    {
        var census = 0;
        var failures = new List<string>();
        foreach (var type in typeof(Mutagen.Bethesda.Starfield.StarfieldMod).Assembly.GetTypes()
                     .Where(t => t.IsClass && t.Namespace == "Mutagen.Bethesda.Starfield")
                     .OrderBy(t => t.FullName))
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(f => f.IsInitOnly && f.Name.EndsWith("Default") && f.Name.Length > "Default".Length))
            {
                var name = field.Name.Substring(0, field.Name.Length - "Default".Length);
                if (type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) == null) continue;
                var plain = type.Name.Split('`')[0];
                if (!_gen.Sources.TryGetValue($"{plain}_Serializations.g.cs", out var source)) continue;
                var typeText = type.IsGenericTypeDefinition
                    ? $"{type.Namespace}.{plain}<{string.Join(", ", type.GetGenericArguments().Select(a => a.Name))}>"
                    : $"{type.Namespace}.{plain}";
                var defaultText = $"{typeText}.{field.Name}";
                census++;
                var value = type.IsGenericTypeDefinition ? "(generic)" : field.GetValue(null)?.ToString();
                _output.WriteLine($"{typeText}.{name} = {value}");
                if (!source.Contains($"ret[\"{name}\"] = {defaultText};")) failures.Add($"{typeText}.{name}: not listed with {defaultText}");
                if (!source.Contains($", item.{name}, {defaultText})")) failures.Add($"{typeText}.{name}: not compared with {defaultText}");
            }
        }
        _output.WriteLine($"{census} fields with a default of their own");
        census.ShouldBeGreaterThan(40);
        failures.ShouldBeEmpty();
    }
}
