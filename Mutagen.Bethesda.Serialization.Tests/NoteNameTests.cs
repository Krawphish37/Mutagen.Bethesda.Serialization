using System.Text;
using System.Text.Json.Nodes;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Utility;
using Mutagen.Bethesda.Strings;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Tests;

// The public names of the round-trip notes and of a translated text's object (C59, W14). SFSearch kept copies of them
// (SFSearch.Records SerializerNotes, agent-first-integration 7fc0119); these tests check that the public sets hold
// what those copies held, and that the shape test gives what SFSearch's IsText gave.
public class NoteNameTests
{
    // SerializerNotes.RecordKeys and TextKeys as SFSearch had them
    private static readonly string[] SfSearchRecordKeys =
    {
        "HeaderFormIdAsStored", "OwnLinkFormsAsStored", "GroupLabelFormsAsStored", "SubrecordOrder", "OriginalCompressedData", "ChildGroupLayout",
    };

    private static readonly string[] SfSearchTextKeys = { "StringsKey", "StringsSource", "StringsPlugin" };

    private static bool SfSearchIsText(JsonObject o)
        => o.ContainsKey("TargetLanguage") && o.All(p => p.Key is "TargetLanguage" or "Value" or "Values" || SfSearchTextKeys.Contains(p.Key));

    [Fact]
    public void PublicSetsHoldWhatSfSearchCopied()
    {
        foreach (var key in SfSearchRecordKeys) RoundTripNoteNames.RecordNotes.ShouldContain(key);
        StringsKeyNote.Names.OrderBy(x => x).ShouldBe(SfSearchTextKeys.OrderBy(x => x));
        RoundTripNoteNames.ModNotes.ShouldBe(
            new[] { "EmptyTopLevelGroups", "HeaderStatsBaseline", "OverriddenFormsBaseline", "OwnFormIdStyle" },
            ignoreOrder: true);
        RoundTripNoteNames.All.Count.ShouldBe(RoundTripNoteNames.RecordNotes.Count + RoundTripNoteNames.ModNotes.Count);
        RoundTripNoteNames.RecordNotes.Intersect(RoundTripNoteNames.ModNotes).ShouldBeEmpty();
    }

    private static JsonObject WriteText(ITranslatedStringGetter? text)
    {
        var kernel = MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>.Instance;
        using var mem = new MemoryStream();
        var package = new StreamPackage(mem, null);
        var w = kernel.GetNewObject(package);
        kernel.WriteTranslatedString(w, "Name", text, null, checkDefaults: false);
        kernel.Finalize(package, w);
        return (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(mem.ToArray()))!["Name"]!;
    }

    [Fact]
    public void TranslatedStringShapeAsSfSearchsIsText()
    {
        var keyed = new TranslatedString(Language.English, "Hello");
        keyed.SetStringsKey(0x1234, StringsSource.DL, ModKey.FromNameAndExtension("Some.esm"));
        var multi = new TranslatedString(Language.English, "Hello");
        multi.Set(Language.French, "Bonjour");
        var objects = new[]
        {
            WriteText(new TranslatedString(Language.English, "Hello")),
            WriteText(keyed),
            WriteText(multi),
            WriteText(new TranslatedString(Language.German)),
            WriteText(null),
            new JsonObject { ["TargetLanguage"] = "English", ["Other"] = 1 },
            new JsonObject { ["Value"] = "x" },
        };
        objects[1].ContainsKey(StringsKeyNote.KeyName).ShouldBeTrue();
        objects[2].ContainsKey(TranslatedStringNames.Values).ShouldBeTrue();
        foreach (var o in objects)
        {
            TranslatedStringNames.IsTranslatedStringObject(o.Select(p => p.Key)).ShouldBe(SfSearchIsText(o), o.ToJsonString());
        }
        TranslatedStringNames.IsTranslatedStringObject(objects[0].Select(p => p.Key)).ShouldBeTrue();
    }
}
