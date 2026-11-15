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
}
