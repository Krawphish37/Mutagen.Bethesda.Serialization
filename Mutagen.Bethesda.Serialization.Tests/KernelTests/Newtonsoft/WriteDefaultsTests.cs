using System.Text;
using System.Text.Json.Nodes;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Tests.KernelTests.Newtonsoft;

// C59 W18: a writer kernel with WriteDefaults writes a value equal to its default; it still leaves out a null
public class WriteDefaultsTests
{
    private static JsonObject Write(
        MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> kernel,
        Action<MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>, JsonWritingUnit> write)
    {
        using var mem = new MemoryStream();
        var package = new StreamPackage(mem, null);
        var w = kernel.GetNewObject(package);
        write(kernel, w);
        kernel.Finalize(package, w);
        return (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(mem.ToArray()))!;
    }

    private static void Fields(MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> k, JsonWritingUnit w)
    {
        k.WriteInt32(w, "Int", 0, default(int));
        k.WriteFloat(w, "Float", 0f, default(float));
        k.WriteBool(w, "Bool", false, default(bool));
        k.WriteFormKey(w, "Link", FormKey.Null, FormKey.Null);
        k.WriteString(w, "Text", "", "");
        k.WriteEnum<GameRelease>(w, "Enum", GameRelease.Oblivion, default(GameRelease));
        k.WriteInt32(w, "NullInt", null, default(int?));
        k.WriteString(w, "NullText", null, default(string));
        k.WriteFloat(w, "NullFloat", null, default(float?));
        k.WriteInt32(w, "NotDefault", 5, default(int));
    }

    [Fact]
    public void WritesDefaults_LeavesOutNulls()
    {
        var full = Write(new MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> { WriteDefaults = true }, Fields);
        full.Select(p => p.Key).ShouldBe(new[] { "Int", "Float", "Bool", "Link", "Text", "Enum", "NotDefault" });
        full["Int"]!.GetValue<int>().ShouldBe(0);
        full["Float"]!.GetValue<double>().ShouldBe(0d);
        full["Bool"]!.GetValue<bool>().ShouldBeFalse();
        full["Link"]!.GetValue<string>().ShouldBe(FormKey.Null.ToString());
        full["Enum"]!.GetValue<string>().ShouldBe(nameof(GameRelease.Oblivion));
    }

    [Fact]
    public void OffByDefault()
    {
        MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>.Instance.WriteDefaults.ShouldBeFalse();
        var normal = Write(MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>.Instance, Fields);
        normal.Select(p => p.Key).ShouldBe(new[] { "NotDefault" });
    }
}
