using Microsoft.CodeAnalysis;
using Mutagen.Bethesda.Serialization.Tests.SourceGenerators;
using Shouldly;

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

    public GeneratedStarfieldTests(GeneratedStarfield gen)
    {
        _gen = gen;
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
}
