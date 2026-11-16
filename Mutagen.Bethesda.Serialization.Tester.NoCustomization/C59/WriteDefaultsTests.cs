using System.Reflection;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Testing;
using Newtonsoft.Json.Linq;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.Tests.C59;

// C59 W18: a full-profile JSON (a writer kernel with WriteDefaults) holds every field that has a value. SFSearch filled
// the fields the JSON left out from their defaults (SFSearch.Records Card.cs CardDefaults, FieldLookup.DefaultOf); these
// tests check that the full JSON holds those fields with exactly the record's values (it reads back as the record), and
// that a field is missing from it only where the record has no value (null).
public class WriteDefaultsTests
{
    private static readonly MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> FullKernel = new() { WriteDefaults = true };

    private static JsonObjectConverter Full() => new(Fallout4SerializationRegistry.Instance, RegistryTests.Meta, FullKernel);
    private static JsonObjectConverter Normal() => new(Fallout4SerializationRegistry.Instance, RegistryTests.Meta);

    // The value at a JSON path in the object, by property names; unknown when a step isn't a property of the class
    private static bool TryGet(object root, string path, out object? value)
    {
        value = root;
        foreach (var step in path.Split('.'))
        {
            if (value == null) return true;
            var prop = value.GetType().GetProperty(step, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || prop.GetIndexParameters().Length > 0) return false;
            value = prop.GetValue(value);
            // A link with no FormKey is a field with no value (written as nothing)
            if (value is IFormLinkGetter { FormKeyNullable: null }) value = null;
        }
        return true;
    }

    private static IEnumerable<string> ObjectPaths(JContainer token) =>
        token.Descendants().OfType<JProperty>().Select(p => p.Path).Where(p => !p.Contains('['));

    [Fact]
    public void EveryRecordClass_FullJsonReadsBackAsTheRecord()
    {
        var full = Full();
        var normal = Normal();
        foreach (var record in RegistryTests.DefaultRecords())
        {
            var fullJson = full.ToJson(record);
            var normalJson = normal.ToJson(record);
            fullJson.Length.ShouldBeGreaterThanOrEqualTo(normalJson.Length);
            // What the normal JSON holds, the full JSON holds with the same values
            var fullObj = JObject.Parse(fullJson);
            foreach (var path in ObjectPaths(JObject.Parse(normalJson)))
            {
                fullObj.SelectToken(path).ShouldNotBeNull($"{record.GetType().Name} {path}");
            }
            full.FromJson(fullJson, record.GetType()).Equals(record).ShouldBeTrue(record.GetType().Name);
        }
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void FieldsAtTheirDefaultAreThere(Ammunition ammo, ArmorAddon armorAddon, Faction faction, Keyword keyword)
    {
        var full = Full();
        var normal = Normal();
        foreach (var filled in new IMajorRecord[] { ammo, armorAddon, faction, keyword })
        {
            // A record of the same class with every field at its default
            var empty = (IMajorRecord)System.Activator.CreateInstance(filled.GetType(), filled.FormKey, Fallout4Release.Fallout4)!;
            var emptyFull = JObject.Parse(full.ToJson(empty));
            var emptyNormal = JObject.Parse(normal.ToJson(empty));
            int added = 0;
            // Every field the filled record's JSON has is in the empty record's full JSON, where the empty record has a
            // value there (a null field is absent); the normal JSON leaves the defaults out
            foreach (var path in ObjectPaths(JObject.Parse(normal.ToJson(filled))))
            {
                if (!TryGet(empty, path, out var value) || value == null) continue;
                emptyFull.SelectToken(path).ShouldNotBeNull($"{filled.GetType().Name} {path}");
                if (emptyNormal.SelectToken(path) == null) added++;
            }
            added.ShouldBeGreaterThan(0, filled.GetType().Name);
            full.FromJson(emptyFull.ToString(), filled.GetType()).Equals(empty).ShouldBeTrue();
            // The filled record's full JSON reads back as its normal JSON does
            var normalJson = normal.ToJson(filled);
            normal.ToJson(full.FromJson(full.ToJson(filled), filled.GetType())).ShouldBe(normalJson);
        }
    }
}
