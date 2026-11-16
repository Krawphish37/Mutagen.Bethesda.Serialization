using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Newtonsoft.Json.Linq;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.Tests.C59;

// S151 (SFSearch J113): ObjectTemplate<T>.AddonIndex has the default -1 (Mutagen XML default="-1", filled by
// OBTSLogic). The serializer compares with ObjectTemplate<T>.AddonIndexDefault, so -1 is left out and 0 is written;
// what was missing for a reader of the JSON was the default itself: ObjectTemplate<T> is generic, and FieldDefaults
// (S124) and the registry (W11) left generic classes out. Now FieldDefaults<T>() and the registry's closed generic
// classes (ObjectTemplate<Weapon.Property>, ...) report -1.
public class ObjectTemplateDefaultsTests
{
    private static readonly ISerializationRegistry Registry = Fallout4SerializationRegistry.Instance;

    private static readonly short[] AddonIndexes = { -1, 0, 7, short.MinValue, short.MaxValue };

    private static Weapon WeaponWithTemplates()
    {
        var weapon = new Weapon(new FormKey(ModKey.FromFileName("C59.esp"), 0x800), Fallout4Release.Fallout4)
        {
            ObjectTemplates = new()
        };
        foreach (var index in AddonIndexes)
        {
            weapon.ObjectTemplates.Add(new ObjectTemplate<Weapon.Property> { AddonIndex = index, LevelMin = 1 });
        }
        return weapon;
    }

    public static IEnumerable<object[]> Converters() => new[]
    {
        new object[] { false },
        new object[] { true },
    };

    private static JsonObjectConverter Converter(bool writeDefaults) => new(Registry, RegistryTests.Meta,
        writeDefaults ? new MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> { WriteDefaults = true } : null);

    [Theory]
    [MemberData(nameof(Converters))]
    public void AddonIndex_RecordJsonRecord_And_JsonRecordJson(bool writeDefaults)
    {
        var converter = Converter(writeDefaults);
        var weapon = WeaponWithTemplates();
        var json = converter.ToJson(weapon);
        var back = (Weapon)converter.FromJson(json, typeof(Weapon));
        back.ObjectTemplates!.Select(t => t.AddonIndex).ShouldBe(AddonIndexes);
        converter.ToJson(back).ShouldBe(json);

        // In the JSON: -1 is left out (it is the default) unless defaults are written; every other value is there
        var templates = (JArray)JObject.Parse(json)["ObjectTemplates"]!;
        for (int i = 0; i < AddonIndexes.Length; i++)
        {
            var token = templates[i]["AddonIndex"];
            if (AddonIndexes[i] == -1 && !writeDefaults) token.ShouldBeNull();
            else ((short)token!).ShouldBe(AddonIndexes[i]);
        }
    }

    [Fact]
    public void FieldDefaultsReportMinusOne()
    {
        var closed = typeof(ObjectTemplate<Weapon.Property>);
        Registry.ClassTypes.ShouldContain(closed);
        Registry.ClassTypes.ShouldContain(typeof(ObjectTemplate<Armor.Property>));
        Registry.TryGetFieldDefaults(closed, out var defaults).ShouldBeTrue();
        defaults!["AddonIndex"].ShouldBe((short)-1);
        defaults["AddonIndex"].ShouldBe(ObjectTemplate<Weapon.Property>.AddonIndexDefault);
        Registry.TryGetFieldDefaults(typeof(IObjectTemplateGetter<Weapon.Property>), out var byGetter).ShouldBeTrue();
        byGetter.ShouldBeSameAs(defaults);

        // A template alone, by its runtime type
        var template = new ObjectTemplate<Weapon.Property> { AddonIndex = 3 };
        SerializationRegistry.ClassTypeOf(template).ShouldBe(closed);
        var converter = Converter(false);
        var json = converter.ToJson(template);
        ((ObjectTemplate<Weapon.Property>)converter.FromJson(json, closed)).AddonIndex.ShouldBe((short)3);
        ((ObjectTemplate<Weapon.Property>)converter.FromJson("{}", closed)).AddonIndex.ShouldBe((short)-1);
    }
}
