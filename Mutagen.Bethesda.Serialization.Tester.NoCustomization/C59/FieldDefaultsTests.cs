using System.Collections;
using System.Reflection;
using Loqui;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Testing;
using Newtonsoft.Json.Linq;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.Tests.C59;

// C59 S124 (SFSearch J104): the serializer exposes the value each field is left out at (registry.TryGetFieldDefaults,
// the generated FieldDefaults), so SFSearch no longer re-derives each class's {Name}Default. For every generated
// Fallout4 class: a field is left out of the JSON exactly when its value equals the exposed default, and every
// scalar field the JSON leaves out is listed.
public class FieldDefaultsTests
{
    private static readonly ISerializationRegistry Registry = Fallout4SerializationRegistry.Instance;

    // A new object of each class, fields at their initial values
    private static IEnumerable<object> NewObjects()
    {
        uint id = 0x800;
        foreach (var type in Registry.ClassTypes.OrderBy(t => t.FullName))
        {
            if (typeof(IMajorRecord).IsAssignableFrom(type))
            {
                yield return System.Activator.CreateInstance(type, new FormKey(ModKey.FromFileName("C59.esp"), id++), Fallout4Release.Fallout4)!;
            }
            else if (type.GetConstructor(Type.EmptyTypes) != null)
            {
                yield return System.Activator.CreateInstance(type)!;
            }
        }
    }

    // A field's value as the serializer writes it (what it compares with the default)
    private static object? Written(object? value) => value switch
    {
        IFormLinkGetter link => link.FormKeyNullable,
        IAssetLinkGetter asset => asset.GivenPath,
        IEDIDLinkGetter edid => edid.EDID.Type,
        _ => value,
    };

    private static bool TryGetProperty(object o, string name, out object? value)
    {
        value = null;
        var prop = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null || prop.GetIndexParameters().Length > 0) return false;
        value = prop.GetValue(o);
        return true;
    }

    private static bool SameAsDefault(object? value, object? def)
    {
        value = Written(value);
        if (value is float f && def is float g) return FloatSerialization.BitEquals(f, g);
        if (value is double d && def is double e) return FloatSerialization.BitEquals(d, e);
        if (value is byte[] || def is byte[] || value is IEnumerable<byte>) return value == null && def == null || value is IEnumerable<byte> a && def is IEnumerable<byte> b && a.SequenceEqual(b);
        return Equals(value, def);
    }

    private static void Check(object o, JsonObjectConverter converter, ref int fieldsChecked)
    {
        var type = o.GetType();
        Registry.TryGetFieldDefaults(type, out var defaults).ShouldBeTrue(type.Name);
        var json = JObject.Parse(converter.ToJson(o));
        foreach (var (name, def) in defaults!)
        {
            TryGetProperty(o, name, out var value).ShouldBeTrue($"{type.Name}.{name}");
            var leftOut = json.Property(name) == null;
            leftOut.ShouldBe(SameAsDefault(value, def), $"{type.Name}.{name}: value {Written(value) ?? "null"}, exposed default {def ?? "null"}");
            fieldsChecked++;
        }
        // Every field the JSON leaves out that holds a single value is listed
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0 || json.Property(prop.Name) != null) continue;
            var value = prop.GetValue(o);
            if (value == null || value is ILoquiObject || value is IEnumerable and not string || value is IFormLinkGetter { IsNull: true } || value is IEDIDLinkGetter) continue;
            if (!SerializedFieldNames(type).Contains(prop.Name)) continue;
            defaults!.ShouldContainKey(prop.Name, $"{type.Name}.{prop.Name} = {value} is left out but not listed");
        }
    }

    // The fields the serializer writes for a class: the keys its full-profile JSON has
    private static readonly Dictionary<Type, HashSet<string>> Serialized = new();
    private static readonly JsonObjectConverter Full = new(Registry, RegistryTests.Meta,
        new MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> { WriteDefaults = true });

    private static HashSet<string> SerializedFieldNames(Type type)
    {
        if (!Serialized.TryGetValue(type, out var names))
        {
            var o = typeof(IMajorRecord).IsAssignableFrom(type)
                ? System.Activator.CreateInstance(type, new FormKey(ModKey.FromFileName("C59.esp"), 0x800), Fallout4Release.Fallout4)!
                : System.Activator.CreateInstance(type)!;
            names = JObject.Parse(Full.ToJson(o)).Properties().Select(p => p.Name).ToHashSet();
            Serialized[type] = names;
        }
        return names;
    }

    [Fact]
    public void EveryClass_LeftOutExactlyAtTheExposedDefault()
    {
        var converter = new JsonObjectConverter(Registry, RegistryTests.Meta);
        int objects = 0, fieldsChecked = 0;
        foreach (var o in NewObjects())
        {
            Check(o, converter, ref fieldsChecked);
            objects++;
        }
        objects.ShouldBeGreaterThan(500);
        fieldsChecked.ShouldBeGreaterThan(1000);
        // Mutagen's own defaults are the ones exposed (e.g. a placed object's scale is 1)
        Registry.TryGetFieldDefaults(typeof(IAmbientColorsGetter), out _).ShouldBeTrue();
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void FilledRecords_LeftOutExactlyAtTheExposedDefault(Ammunition ammo, ArmorAddon armorAddon, Faction faction, Keyword keyword)
    {
        var converter = new JsonObjectConverter(Registry, RegistryTests.Meta);
        int fieldsChecked = 0;
        foreach (var o in new object[] { ammo.DeepCopy(), armorAddon.DeepCopy(), faction.DeepCopy(), keyword.DeepCopy() })
        {
            Check(o, converter, ref fieldsChecked);
        }
        fieldsChecked.ShouldBeGreaterThan(20);
    }

    [Fact]
    public void NotListed_ForUnknownTypes()
    {
        Registry.TryGetFieldDefaults(typeof(string), out _).ShouldBeFalse();
        Registry.TryGetFieldDefaults(typeof(Weapon), out var weapon).ShouldBeTrue();
        weapon!.ShouldContainKey("EditorID");
        weapon.ShouldNotContainKey("FormVersion");
        Registry.TryGetFieldDefaults(typeof(IWeaponGetter), out var byGetter).ShouldBeTrue();
        byGetter.ShouldBeSameAs(weapon);
    }
}
