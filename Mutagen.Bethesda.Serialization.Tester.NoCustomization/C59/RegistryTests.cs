using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Loqui;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Testing;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.Tests.C59;

/// <summary>
/// SFSearch's way to the generated serializers (SFSearch.Records Serializers.cs, agent-first-integration 7fc0119): the
/// internal "{Class}_Serialization" class found by name, its generic Serialize/Deserialize made for the JSON kernel and
/// invoked by reflection. Copied here so the registry can be checked against it.
/// </summary>
internal static class SfSearchWay
{
    private static readonly Assembly Asm = typeof(SfSearchWay).Assembly;
    private static readonly MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> WriterKernel = new();
    private static readonly NewtonsoftJsonSerializationReaderKernel ReaderKernel = new();

    public static string TypeName(object o) => ((ILoquiObject)o).Registration.ClassType.Name;

    public static bool Knows(string typeName) => Asm.GetType($"Mutagen.Bethesda.Fallout4.{typeName}_Serialization", false) != null;

    /// <summary>Knows, for a class of any namespace (the generated class sits in its object's namespace)</summary>
    public static bool Knows(Type type) => Asm.GetType($"{type.Namespace}.{type.Name.Split('`')[0]}_Serialization", false) != null;

    private static (MethodInfo Ser, MethodInfo Des) For(string n)
    {
        var t = Asm.GetType($"Mutagen.Bethesda.Fallout4.{n}_Serialization", true)!;
        var s = t.GetMethods().First(m => m.Name == "Serialize" && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(NewtonsoftJsonSerializationWriterKernel), typeof(JsonWritingUnit));
        var d = t.GetMethods().First(m => m.Name == "Deserialize" && m.GetGenericArguments().Length == 1)
            .MakeGenericMethod(typeof(JsonReadingUnit));
        return (s, d);
    }

    /// <summary>The object's fields into a writer that is in an object (Serializers.cs RecordSerializer.Serialize)</summary>
    public static void Serialize(JsonWritingUnit w, object o, SerializationMetaData meta)
    {
        try { ((Task)For(TypeName(o)).Ser.Invoke(null, new object[] { w, o, WriterKernel, meta })!).GetAwaiter().GetResult(); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); }
    }

    public static string ToJson(object o, SerializationMetaData meta)
    {
        using var ms = new MemoryStream();
        var pkg = new StreamPackage(ms, null);
        var w = WriterKernel.GetNewObject(pkg);
        try { ((Task)For(TypeName(o)).Ser.Invoke(null, new object[] { w, o, WriterKernel, meta })!).GetAwaiter().GetResult(); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); }
        WriterKernel.Finalize(pkg, w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static object FromJson(string json, string typeName, SerializationMetaData meta)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var r = ReaderKernel.GetNewObject(new StreamPackage(ms, null));
        Task task;
        try { task = (Task)For(typeName).Des.Invoke(null, new object[] { r, ReaderKernel, meta })!; }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        task.GetAwaiter().GetResult();
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }
}

// C59 W11: the generated public registry gives the serializer of an object by its runtime type, with no reflection,
// and the same JSON and objects as SFSearch's reflection did
public class RegistryTests
{
    internal static readonly SerializationMetaData Meta = new(GameRelease.Fallout4, null, null, null, CancellationToken.None);
    private static readonly MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> WriterKernel = new();
    private static readonly NewtonsoftJsonSerializationReaderKernel ReaderKernel = new();
    private static readonly ISerializationRegistry Registry = Fallout4SerializationRegistry.Instance;

    internal static string ToJson(object o)
    {
        using var ms = new MemoryStream();
        var pkg = new StreamPackage(ms, null);
        var w = WriterKernel.GetNewObject(pkg);
        Registry.Serialize(w, o, WriterKernel, Meta).GetAwaiter().GetResult();
        WriterKernel.Finalize(pkg, w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    internal static object FromJson(string json, Type type)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var r = ReaderKernel.GetNewObject(new StreamPackage(ms, null));
        return Registry.Deserialize(r, type, ReaderKernel, Meta).GetAwaiter().GetResult();
    }

    private static IEnumerable<Type> MajorRecordClasses() =>
        Registry.ClassTypes.Where(t => typeof(IMajorRecord).IsAssignableFrom(t)).OrderBy(t => t.Name);

    // A record of each class, all fields at their default
    internal static IEnumerable<IMajorRecord> DefaultRecords()
    {
        uint id = 0x800;
        foreach (var type in MajorRecordClasses())
        {
            yield return (IMajorRecord)System.Activator.CreateInstance(type, new FormKey(ModKey.FromFileName("C59.esp"), id++), Fallout4Release.Fallout4)!;
        }
    }

    [Fact]
    public void ClassTypesAreWhatSfSearchFoundByName()
    {
        Warmup.Init();
        Registry.ClassTypes.Count.ShouldBeGreaterThan(500);
        foreach (var type in Registry.ClassTypes)
        {
            SfSearchWay.Knows(type).ShouldBeTrue(type.FullName);
            type.IsAbstract.ShouldBeFalse();
            Registry.TryGetClassType(type, out var same).ShouldBeTrue();
            same.ShouldBe(type);
            Registry.TryGetClassType(type.FullName!, out var byFull).ShouldBeTrue();
            byFull.ShouldBe(type);
            if (!type.IsGenericType)
            {
                // A closed generic class (ObjectTemplate<Weapon.Property>) has no plain name of its own
                Registry.TryGetClassType(type.Name, out var byName).ShouldBeTrue();
                byName.ShouldBe(type);
            }
            LoquiRegistration.TryGetRegister(type.IsGenericType ? type.GetGenericTypeDefinition() : type, out var registration).ShouldBeTrue(type.Name);
            var getter = registration!.GetterType.IsGenericTypeDefinition ? registration.GetterType.MakeGenericType(type.GetGenericArguments()) : registration.GetterType;
            var setter = registration.SetterType.IsGenericTypeDefinition ? registration.SetterType.MakeGenericType(type.GetGenericArguments()) : registration.SetterType;
            Registry.TryGetClassType(getter, out var byGetter).ShouldBeTrue();
            byGetter.ShouldBe(type);
            Registry.TryGetClassType(setter, out var bySetter).ShouldBeTrue();
            bySetter.ShouldBe(type);
        }
        // Every major record class of the game is there
        var gameRecords = typeof(Fallout4Mod).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericType: false }
                        && t.Namespace == "Mutagen.Bethesda.Fallout4"
                        && typeof(IMajorRecord).IsAssignableFrom(t)
                        && SfSearchWay.Knows(t.Name))
            .ToArray();
        gameRecords.Length.ShouldBeGreaterThan(100);
        foreach (var t in gameRecords) Registry.ClassTypes.ShouldContain(t);
        Registry.ClassTypes.ShouldNotContain(typeof(Fallout4Mod));
        Registry.TryGetClassType("NoSuchClass", out _).ShouldBeFalse();
        Registry.TryGetClassType(typeof(string), out _).ShouldBeFalse();
    }

    [Fact]
    public void EveryRecordClass_SameJsonAndObjectAsSfSearchsReflection()
    {
        foreach (var record in DefaultRecords())
        {
            CheckAgainstSfSearch(record);
        }
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void FilledRecords_SameJsonAndObjectAsSfSearchsReflection(Ammunition ammo, ArmorAddon armorAddon, Faction faction, Keyword keyword)
    {
        foreach (var record in new IMajorRecord[] { ammo, armorAddon, faction, keyword })
        {
            CheckAgainstSfSearch(record);
        }
    }

    [Fact]
    public void OverlayRecords_SameJsonAsSfSearchsReflection()
    {
        var mod = new Fallout4Mod(ModKey.FromFileName("C59Overlay.esp"), Fallout4Release.Fallout4);
        var keyword = mod.Keywords.AddNew("C59Keyword");
        var weapon = mod.Weapons.AddNew("C59Weapon");
        weapon.Name = "Gun";
        weapon.Keywords = new() { keyword.ToLink() };
        weapon.Value = 12;
        var faction = mod.Factions.AddNew("C59Faction");
        faction.Name = "Group";
        using var mem = new MemoryStream();
        mod.WriteToBinary(mem);
        using var read = Fallout4Mod.CreateFromBinaryOverlay(new MemoryStream(mem.ToArray()), Fallout4Release.Fallout4, mod.ModKey);
        var records = read.EnumerateMajorRecords().ToArray();
        records.Length.ShouldBe(3);
        foreach (var record in records)
        {
            record.GetType().Name.ShouldEndWith("BinaryOverlay");
            ToJson(record).ShouldBe(SfSearchWay.ToJson(record, Meta));
        }
        ToJson(records.OfType<IWeaponGetter>().Single()).ShouldContain(keyword.FormKey.ToString());
    }

    private static void CheckAgainstSfSearch(IMajorRecordGetter record)
    {
        var json = ToJson(record);
        json.ShouldBe(SfSearchWay.ToJson(record, Meta), record.GetType().Name);
        var name = SfSearchWay.TypeName(record);
        var theirs = SfSearchWay.FromJson(json, name, Meta);
        var registration = ((ILoquiObject)record).Registration;
        foreach (var type in new[] { registration.ClassType, registration.GetterType, registration.SetterType })
        {
            var ours = FromJson(json, type);
            ours.GetType().ShouldBe(registration.ClassType);
            ours.Equals(theirs).ShouldBeTrue($"{name} read as {type.Name}");
            ToJson(ours).ShouldBe(json, $"{name} read as {type.Name}");
        }
    }

    [Fact]
    public void UnknownTypesAreRefused()
    {
        Should.Throw<ArgumentException>(() => ToJson("not a record"));
        Should.Throw<ArgumentException>(() => FromJson("{}", typeof(string)));
        Should.Throw<ArgumentException>(() => FromJson("{}", typeof(IMajorRecordGetter)));
    }
}
