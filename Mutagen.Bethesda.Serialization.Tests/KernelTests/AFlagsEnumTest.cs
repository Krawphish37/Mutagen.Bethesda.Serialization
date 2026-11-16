using System.Reflection;
using System.Text;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Yaml;
using Noggog;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Tests.KernelTests;

// C72: [Flags] enums are written and read with the kernels' own bit walk (FlagsEnumText), not Noggog's
// EnumerateContainedFlags, which never finishes for an int-based enum with a member at bit 31 (fixes.20's
// NavigationMesh/BiomeMarker MajorFlag) and gave a spurious "0x0" for a value with the sign bit set.

[Flags]
public enum C72Int : int
{
    Low = 0x1,
    Autogen = 0x0400_0000,
    Top = unchecked((int)0x8000_0000),
}

[Flags]
public enum C72SByte : sbyte
{
    One = 1,
    Sign = unchecked((sbyte)0x80),
}

[Flags]
public enum C72Short : short
{
    One = 1,
    Sign = unchecked((short)0x8000),
}

[Flags]
public enum C72Long : long
{
    One = 1,
    Sign = long.MinValue,
}

[Flags]
public enum C72ULong : ulong
{
    One = 1,
    Top = 0x8000_0000_0000_0000,
}

// An int flag enum without a bit-31 member, as fixes.19's NavigationMesh.MajorFlag was
[Flags]
public enum C72NoTop : int
{
    Compressed = 0x0004_0000,
    Autogen = 0x0400_0000,
}

public abstract class AFlagsEnumTest<TWriterKernel, TWriter, TReaderKernel, TReader>
    : ASerializationTest<TWriterKernel, TWriter, TReaderKernel, TReader>
    where TWriterKernel : ISerializationWriterKernel<TWriter>, new()
    where TReaderKernel : ISerializationReaderKernel<TReader>, new()
    where TWriter : IContainStreamPackage
{
    private static void Within(TimeSpan time, Action action)
    {
        var task = Task.Run(action);
        task.Wait(time).ShouldBeTrue("did not finish");
        task.GetAwaiter().GetResult();
    }

    protected string Write<TEnum>(TEnum value)
        where TEnum : struct, Enum, IConvertible
    {
        using var memStream = new MemoryStream();
        var stream = GetStreamPackage(memStream);
        var writerObj = WriterKernel.GetNewObject(stream);
        WriterKernel.WriteEnum<TEnum>(writerObj, "F", value, default, checkDefaults: false);
        WriterKernel.Finalize(stream, writerObj);
        if (writerObj is IDisposable disp) disp.Dispose();
        return Encoding.UTF8.GetString(memStream.ToArray());
    }

    protected TEnum? Read<TEnum>(string text)
        where TEnum : struct, Enum, IConvertible
    {
        using var mem = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var readerObj = ReaderKernel.GetNewObject(GetStreamPackage(mem));
        using var disp = readerObj as IDisposable;
        ReaderKernel.TryGetNextField(readerObj, out var name).ShouldBeTrue();
        name.ShouldBe("F");
        return ReaderKernel.ReadEnum<TEnum>(readerObj);
    }

    private void RoundTrip<TEnum>(TEnum value, params string[] texts)
        where TEnum : struct, Enum, IConvertible
    {
        FlagsEnumText.ToTexts(value).ShouldBe(texts);
        string written = null!;
        Within(TimeSpan.FromSeconds(10), () => written = Write(value));
        foreach (var t in texts) written.ShouldContain(t);
        written.ShouldNotContain("0x0\"");
        Read<TEnum>(written).ShouldBe(value);
    }

    [Fact]
    public void NegativeMember_WritesAndReadsBack()
    {
        RoundTrip(C72Int.Top, "Top");
        RoundTrip(C72Int.Autogen | C72Int.Top, "Autogen", "Top");
        RoundTrip(C72Int.Low | C72Int.Top | (C72Int)0x2, "Low", "0x2", "Top");
        RoundTrip((C72Int)(-1), Enumerable.Range(0, 32).Select(i => i switch
        {
            0 => "Low",
            26 => "Autogen",
            31 => "Top",
            _ => "0x" + (1UL << i).ToString("X"),
        }).ToArray());
        RoundTrip(C72SByte.One | C72SByte.Sign, "One", "Sign");
        RoundTrip((C72SByte)(-1), "One", "0x2", "0x4", "0x8", "0x10", "0x20", "0x40", "Sign");
        RoundTrip(C72Short.Sign, "Sign");
        RoundTrip(C72Long.One | C72Long.Sign, "One", "Sign");
        RoundTrip(C72ULong.Top | (C72ULong)0x1_0000_0000, "0x100000000", "Top");
        RoundTrip(default(C72Int));
    }

    // The real enums that name bit 31: int-based in Mutagen fixes.20 (Noggog never finished), uint from fixes.21 (C71)
    [Fact]
    public void Fixes20NavmeshAndBiomeMarkerFlags()
    {
        var navmesh = Mutagen.Bethesda.Starfield.NavigationMesh.MajorFlag.NavmeshGenCell;
        var navmeshTexts = FlagsEnumText.ToTexts(navmesh).ToArray();
        navmeshTexts.ShouldBe(new[] { nameof(Mutagen.Bethesda.Starfield.NavigationMesh.MajorFlag.NavmeshGenCell) });
        string written = null!;
        Within(TimeSpan.FromSeconds(10), () => written = Write(navmesh));
        Read<Mutagen.Bethesda.Starfield.NavigationMesh.MajorFlag>(written).ShouldBe(navmesh);
        var biome = Mutagen.Bethesda.Starfield.BiomeMarker.MajorFlag.MustBeUnique;
        Within(TimeSpan.FromSeconds(10), () => written = Write(biome));
        Read<Mutagen.Bethesda.Starfield.BiomeMarker.MajorFlag>(written).ShouldBe(biome);
    }

    // The bit-31 case of an int enum without such a member (fixes.19's NavigationMesh, 13 Starfield.esm NAVM): an
    // unnamed "0x80000000" and never a "0x0"; the old text, with Noggog's spurious "0x0", still reads back
    [Fact]
    public void SignBitWithoutMember_NoZeroItem_OldTextReads()
    {
        var value = C72NoTop.Compressed | (C72NoTop)unchecked((int)0x8000_0000);
        RoundTrip(value, "Compressed", "0x80000000");
        var old = value.EnumerateContainedFlags(includeUndefined: true).Select(f => f.ToStringFast()).ToArray();
        old.ShouldBe(new[] { "Compressed", "0x80000000", "0x0" });
        Read<C72NoTop>(OldText(old)).ShouldBe(value);
    }

    // Starfield.esm's furniture markers (S162): 3,904 EntryTypes/ExitTypes values hold all 32 bits of an int flag enum
    // whose members end at 0x10. No "0x0" item; the old text, with it, reads back
    [Fact]
    public void FurnitureMarkerAllBits()
    {
        var value = (Mutagen.Bethesda.Starfield.Furniture.EntryParameterType)(-1);
        var texts = new[] { "Front", "Behind", "Right", "Left", "Other" }
            .Concat(Enumerable.Range(5, 27).Select(i => "0x" + (1UL << i).ToString("X")))
            .ToArray();
        RoundTrip(value, texts);
        var old = value.EnumerateContainedFlags(includeUndefined: true).Select(f => f.ToStringFast()).ToArray();
        old.ShouldBe(texts.Append("0x0"));
        Read<Mutagen.Bethesda.Starfield.Furniture.EntryParameterType>(OldText(old)).ShouldBe(value);
    }

    protected abstract string OldText(string[] items);

    // Every [Flags] enum of Mutagen's game and core assemblies that Noggog can handle (no member with the sign bit):
    // the same items as Noggog's EnumerateContainedFlags + ToStringFast gave, for each member, all members together,
    // every bit below the sign bit, and seeded random values
    [Fact]
    public void SameItemsAsBefore_ForEveryMutagenFlagsEnum()
    {
        var assemblies = new[]
        {
            typeof(Mutagen.Bethesda.Plugins.FormKey).Assembly,
            typeof(Mutagen.Bethesda.Starfield.StarfieldMod).Assembly,
            typeof(Mutagen.Bethesda.Fallout4.Fallout4Mod).Assembly,
            typeof(Mutagen.Bethesda.Skyrim.SkyrimMod).Assembly,
            typeof(Mutagen.Bethesda.Oblivion.OblivionMod).Assembly,
        };
        var method = typeof(AFlagsEnumTest<TWriterKernel, TWriter, TReaderKernel, TReader>)
            .GetMethod(nameof(CompareWithNoggog), BindingFlags.NonPublic | BindingFlags.Static)!;
        int enums = 0, values = 0;
        foreach (var type in assemblies.SelectMany(a => a.GetTypes())
                     .Where(t => t.IsEnum && t.IsDefined(typeof(FlagsAttribute), false)))
        {
            if (HasSignBitMember(type)) continue;
            values += (int)method.MakeGenericMethod(type).Invoke(null, null)!;
            enums++;
        }
        enums.ShouldBeGreaterThan(300);
        values.ShouldBeGreaterThan(10000);
    }

    private static bool HasSignBitMember(Type type)
    {
        var underlying = Enum.GetUnderlyingType(type);
        if (underlying == typeof(byte) || underlying == typeof(ushort) || underlying == typeof(uint) || underlying == typeof(ulong)) return false;
        return Enum.GetValues(type).Cast<object>().Any(v => Convert.ToInt64(v) < 0);
    }

    private static ulong SignBit(Type type)
    {
        var underlying = Enum.GetUnderlyingType(type);
        if (underlying == typeof(byte) || underlying == typeof(ushort) || underlying == typeof(uint) || underlying == typeof(ulong)) return 0;
        return 1UL << (System.Runtime.InteropServices.Marshal.SizeOf(underlying) * 8 - 1);
    }

    private static int CompareWithNoggog<TEnum>()
        where TEnum : struct, Enum, IConvertible
    {
        var width = System.Runtime.CompilerServices.Unsafe.SizeOf<TEnum>() * 8;
        var sign = SignBit(typeof(TEnum));
        var mask = (width == 64 ? ulong.MaxValue : (1UL << width) - 1) & ~sign;
        var members = Enum.GetValues<TEnum>().Select(FlagsEnumText.ToBits).ToArray();
        var candidates = new List<ulong> { 0, members.Aggregate(0UL, (a, b) => a | b) & mask };
        candidates.AddRange(members.Select(m => m & mask));
        for (int i = 0; i < width; i++) candidates.Add((1UL << i) & mask);
        var random = new Random(72 + width);
        for (int i = 0; i < 20; i++) candidates.Add((ulong)random.NextInt64() & mask);
        foreach (var bits in candidates)
        {
            var value = FlagsEnumText.FromBits<TEnum>(bits);
            var before = value.EnumerateContainedFlags(includeUndefined: true).Select(f => f.ToStringFast()).ToArray();
            FlagsEnumText.ToTexts(value).ShouldBe(before, $"{typeof(TEnum).FullName} 0x{bits:X}");
        }
        return candidates.Count;
    }
}

public class NewtonsoftFlagsEnumTests : AFlagsEnumTest<
    NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit,
    NewtonsoftJsonSerializationReaderKernel, JsonReadingUnit>
{
    protected override string OldText(string[] items) =>
        "{\n  \"F\": [\n" + string.Join(",\n", items.Select(i => $"    \"{i}\"")) + "\n  ]\n}";
}

public class YamlFlagsEnumTests : AFlagsEnumTest<
    YamlSerializationWriterKernel, YamlWritingUnit,
    YamlSerializationReaderKernel, YamlReadingUnit>
{
    protected override string OldText(string[] items) =>
        "F:\n" + string.Join("", items.Select(i => $"- {i}\n"));
}
