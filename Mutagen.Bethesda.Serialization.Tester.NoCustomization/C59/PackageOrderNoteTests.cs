using System.Text;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.Tests.C59;

// C59 W13: a package's ProcedureTreeDataOrder travels in its JSON as a round-trip note. SFSearch (Verify.cs GiveBack,
// agent-first-integration 7fc0119) copied it from the record as read onto the record rebuilt from JSON; the JSON alone
// now gives the same package, and both write back as the file.
public class PackageOrderNoteTests
{
    private static readonly ModKey ModKey = ModKey.FromFileName("C59Packages.esp");

    // The plugin's bytes with one package whose names/flags list (UNAM, BNAM) is in the given order
    private static byte[] PluginWithPackage(sbyte[] keys, sbyte[]? order)
    {
        var mod = new Fallout4Mod(ModKey, Fallout4Release.Fallout4);
        var package = mod.Packages.AddNew("C59Package");
        foreach (var key in keys)
        {
            package.Data[key] = new PackageDataBool { Name = $"Input{key}", Data = key % 2 == 0 };
        }
        package.ProcedureTreeDataOrder = order;
        using var mem = new MemoryStream();
        mod.WriteToBinary(mem);
        return mem.ToArray();
    }

    private static IFallout4ModDisposableGetter Read(byte[] bytes) =>
        Fallout4Mod.CreateFromBinaryOverlay(new MemoryStream(bytes), Fallout4Release.Fallout4, ModKey);

    private static string ToJson(IPackageGetter package, bool notes)
    {
        var kernel = MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>.Instance;
        var meta = new SerializationMetaData(GameRelease.Fallout4, null, null, null, CancellationToken.None) { RoundTripNotes = notes };
        using var mem = new MemoryStream();
        var stream = new StreamPackage(mem, null);
        var w = kernel.GetNewObject(stream);
        Package_Serialization.Serialize(w, package, kernel, meta).GetAwaiter().GetResult();
        kernel.Finalize(stream, w);
        return Encoding.UTF8.GetString(mem.ToArray());
    }

    private static Package FromJson(string json)
    {
        var kernel = new NewtonsoftJsonSerializationReaderKernel();
        var meta = new SerializationMetaData(GameRelease.Fallout4, null, null, null, CancellationToken.None);
        var r = kernel.GetNewObject(new StreamPackage(new MemoryStream(Encoding.UTF8.GetBytes(json)), null));
        return Package_Serialization.Deserialize(r, kernel, meta).GetAwaiter().GetResult();
    }

    private static byte[] Rebuilt(IFallout4ModGetter read, Package package)
    {
        var mod = new Fallout4Mod(ModKey, Fallout4Release.Fallout4);
        mod.ModHeader.DeepCopyIn(read.ModHeader);
        mod.Packages.Add(package);
        using var mem = new MemoryStream();
        mod.WriteToBinary(mem);
        return mem.ToArray();
    }

    public static IEnumerable<object?[]> Orders() => new[]
    {
        new object?[] { new sbyte[] { 0, 1, 2 }, new sbyte[] { 2, 0, 1 }, "2,0,1" },
        new object?[] { new sbyte[] { 1, -1 }, new sbyte[] { 1, -1 }, "1,-1" },   // by index for unsigned bytes only
        new object?[] { new sbyte[] { 1, -1 }, new sbyte[] { -1, 1 }, "-1,1" },   // by index for signed bytes only (Fallout4's)
        new object?[] { new sbyte[] { 0, 1, 2 }, null, null },                    // by index both ways: no note
    };

    [Theory]
    [MemberData(nameof(Orders))]
    public void JsonCarriesTheOrder_AsSfSearchsGiveBackDid(sbyte[] keys, sbyte[]? order, string? note)
    {
        var bytes = PluginWithPackage(keys, order);
        using var read = Read(bytes);
        var package = read.Packages.Single();
        var readOrder = package.GetProcedureTreeDataOrder();
        readOrder.ShouldNotBeNull();
        if (order != null) readOrder.ShouldBe(order);

        // Now: the JSON alone
        var json = ToJson(package, notes: true);
        if (note == null) json.ShouldNotContain(RoundTripNoteNames.ProcedureTreeDataOrder);
        else json.ShouldContain($"\"{RoundTripNoteNames.ProcedureTreeDataOrder}\": \"{note}\"");
        var fromJson = FromJson(json);
        var now = Rebuilt(read, fromJson);

        // SFSearch's way: the JSON without the note, the order copied from the record as read
        var before = FromJson(ToJson(package, notes: false));
        if (package.GetProcedureTreeDataOrder() is { } dataOrder) before.ProcedureTreeDataOrder = dataOrder.ToList();
        var sfSearch = Rebuilt(read, before);

        now.ShouldBe(sfSearch);
        now.ShouldBe(bytes);
    }
}
