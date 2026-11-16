using System.Text;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Utility;
using Mutagen.Bethesda.Starfield;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Tests;

// S131 (SFSearch J100, C59): a condition parameter that is an alias index 0 (GetLineOfSight on alias 0 in 7 Starfield.esm
// records) was left out of the JSON as a default; read back, its FormLinkOrIndex had no index (Mutagen makes it with a
// null Index), and writing the JSON again threw "Nullable object must have a value". JSON -> object -> JSON must give
// the first JSON.
public class FormLinkOrIndexTests
{
    private static readonly MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> Writer = new();
    private static readonly NewtonsoftJsonSerializationReaderKernel Reader = new();
    private static readonly SerializationMetaData Meta = new(GameRelease.Starfield, null, null, null, CancellationToken.None);

    private static string Write(GetLineOfSightConditionData data)
    {
        using var mem = new MemoryStream();
        var package = new StreamPackage(mem, null);
        var w = Writer.GetNewObject(package);
        Writer.WriteBool(w, "UseAliases", data.UseAliases, default(bool));
        SerializationHelper.WriteFormLinkOrIndex(w, "FirstParameter", data.FirstParameter, Meta, Writer);
        Writer.Finalize(package, w);
        return Encoding.UTF8.GetString(mem.ToArray());
    }

    private static GetLineOfSightConditionData Read(string json)
    {
        var data = new GetLineOfSightConditionData();
        var r = Reader.GetNewObject(new StreamPackage(new MemoryStream(Encoding.UTF8.GetBytes(json)), null));
        while (Reader.TryGetNextField(r, out var name))
        {
            switch (name)
            {
                case "UseAliases":
                    data.UseAliases = Reader.ReadBool(r) ?? false;
                    break;
                case "FirstParameter":
                    SerializationHelper.ReadFormLinkOrIndex(r, Reader, data.FirstParameter, Meta);
                    break;
                default:
                    Reader.Skip(r);
                    break;
            }
        }
        return data;
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    public void AliasIndex_JsonObjectJson(uint alias)
    {
        var data = new GetLineOfSightConditionData { UseAliases = true };
        data.FirstParameter.Index = alias;
        var json = Write(data);
        json.ShouldContain($"\"FirstParameter\": {alias}");
        var back = Read(json);
        back.FirstParameter.UsesLink().ShouldBeFalse();
        back.FirstParameter.Index.ShouldBe(alias);
        Write(back).ShouldBe(json);
    }

    [Fact]
    public void Link_JsonObjectJson()
    {
        var data = new GetLineOfSightConditionData();
        data.FirstParameter.Link.SetTo(FormKey.Factory("012345:Starfield.esm"));
        var json = Write(data);
        var back = Read(json);
        back.FirstParameter.Link.FormKey.ShouldBe(data.FirstParameter.Link.FormKey);
        Write(back).ShouldBe(json);
    }

    // An index that is null (a FormLinkOrIndex made for an alias and never given one) is left out, and reads back null
    [Fact]
    public void NoIndex_LeftOut()
    {
        var data = new GetLineOfSightConditionData { UseAliases = true };
        data.FirstParameter.Index.ShouldBeNull();
        var json = Write(data);
        json.ShouldNotContain("FirstParameter");
        var back = Read(json);
        back.FirstParameter.Index.ShouldBeNull();
        Write(back).ShouldBe(json);
    }
}
