using System.Runtime.InteropServices;
using System.Text;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Testing;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.Tests.C59;

// C59 V4 (and W11's synchronous calls): JsonObjectConverter writes object after object into one kept buffer
// (JsonWritingUnit.Reset) and gives the JSON a new writer gives, and that SFSearch's own kept writer gave
public class JsonObjectConverterTests
{
    /// <summary>
    /// SFSearch's kept writer (SFSearch.Records Serializers.cs Scribe and ToUtf8, agent-first-integration 7fc0119):
    /// one JsonWritingUnit over one MemoryStream, emptied for each object, with the reflection call
    /// </summary>
    private sealed class SfSearchScribe
    {
        private readonly MemoryStream _stream = new(4096);
        private readonly JsonWritingUnit _unit;

        public SfSearchScribe()
        {
            _unit = new JsonWritingUnit(new StreamPackage(_stream, null));
        }

        public string ToJson(object o)
        {
            _stream.SetLength(0);
            _unit.Writer.WriteStartObject();
            SfSearchWay.Serialize(_unit, o, RegistryTests.Meta);
            _unit.Writer.WriteEndObject();
            _unit.Writer.Flush();
            return Encoding.UTF8.GetString(_stream.GetBuffer(), 0, (int)_stream.Length);
        }
    }

    private static JsonObjectConverter NewConverter(int keep = 1 << 20) =>
        new(Fallout4SerializationRegistry.Instance, RegistryTests.Meta, keepBufferBytes: keep);

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void ObjectAfterObject_AsANewWriterAndAsSfSearchsScribe(Ammunition ammo, ArmorAddon armorAddon, Faction faction)
    {
        var converter = NewConverter();
        var scribe = new SfSearchScribe();
        var records = RegistryTests.DefaultRecords().Cast<IMajorRecordGetter>()
            .Concat(new IMajorRecordGetter[] { ammo, armorAddon, faction })
            .Concat(RegistryTests.DefaultRecords().Take(20))
            .ToArray();
        foreach (var record in records)
        {
            var json = converter.ToJson(record);
            json.ShouldBe(RegistryTests.ToJson(record), record.GetType().Name);
            json.ShouldBe(scribe.ToJson(record), record.GetType().Name);
        }
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void KeepsItsBuffer(Ammunition ammo, ArmorAddon armorAddon)
    {
        var converter = NewConverter();
        MemoryMarshal.TryGetArray(converter.ToUtf8(ammo), out var first).ShouldBeTrue();
        MemoryMarshal.TryGetArray(converter.ToUtf8(armorAddon), out var second).ShouldBeTrue();
        second.Array.ShouldBeSameAs(first.Array);
        Encoding.UTF8.GetString(second).ShouldBe(RegistryTests.ToJson(armorAddon));

        // A buffer grown past the limit is not kept, and what is written is the same
        var small = NewConverter(keep: 16);
        MemoryMarshal.TryGetArray(small.ToUtf8(ammo), out var a).ShouldBeTrue();
        MemoryMarshal.TryGetArray(small.ToUtf8(ammo), out var b).ShouldBeTrue();
        b.Array.ShouldNotBeSameAs(a.Array);
        Encoding.UTF8.GetString(b).ShouldBe(RegistryTests.ToJson(ammo));
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void AnErrorLeavesNothingBehind(ArmorAddon armorAddon)
    {
        var converter = NewConverter();
        converter.ToJson(armorAddon);
        Should.Throw<ArgumentException>(() => converter.ToJson("not a record"));
        converter.ToJson(armorAddon).ShouldBe(RegistryTests.ToJson(armorAddon));
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void ReadsBackAsSfSearchsReflection(Ammunition ammo, ArmorAddon armorAddon)
    {
        var converter = NewConverter();
        var ammoJson = converter.ToJson(ammo);
        converter.FromJson(ammoJson, typeof(Ammunition)).Equals(SfSearchWay.FromJson(ammoJson, "Ammunition", RegistryTests.Meta)).ShouldBeTrue();
        var addonJson = converter.ToJson(armorAddon);
        var theirs = SfSearchWay.FromJson(addonJson, "ArmorAddon", RegistryTests.Meta);
        converter.FromUtf8(converter.ToUtf8(armorAddon), typeof(IArmorAddonGetter)).Equals(theirs).ShouldBeTrue();
        converter.FromJson<IArmorAddonGetter>(addonJson).Equals(theirs).ShouldBeTrue();
        foreach (var record in RegistryTests.DefaultRecords())
        {
            converter.FromJson(converter.ToJson(record), record.GetType()).Equals(record).ShouldBeTrue(record.GetType().Name);
        }
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void UnitResetWritesAsANewUnit(Ammunition ammo, ArmorAddon armorAddon)
    {
        using var mem = new MemoryStream();
        var unit = new JsonWritingUnit(new StreamPackage(mem, null));
        foreach (var record in new IMajorRecordGetter[] { ammo, armorAddon, ammo })
        {
            unit.Reset();
            unit.Writer.WriteStartObject();
            Fallout4SerializationRegistry.Instance
                .Serialize(unit, record, MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>.Instance, RegistryTests.Meta)
                .GetAwaiter().GetResult();
            unit.Writer.WriteEndObject();
            unit.Writer.Flush();
            Encoding.UTF8.GetString(mem.ToArray()).ShouldBe(RegistryTests.ToJson(record));
        }
    }
}
