using System.Text;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Yaml;
using Noggog;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Tests.KernelTests;

// Floats and doubles must come back bit for bit: -0.0 is not the default (so it is written), and a NaN with a
// non-standard bit pattern (e.g. FFFFFFFF, which plugins store) keeps its bits. Older files (plain numbers, "NaN")
// still read.
public abstract class AFloatBitsTest<TWriterKernel, TWriter, TReaderKernel, TReader>
    : ASerializationTest<TWriterKernel, TWriter, TReaderKernel, TReader>
    where TWriterKernel : ISerializationWriterKernel<TWriter>, new()
    where TReaderKernel : ISerializationReaderKernel<TReader>, new()
    where TWriter : IContainStreamPackage
{
    protected abstract string OldFormatText { get; }

    private string Write(Action<MutagenSerializationWriterKernel<TWriterKernel, TWriter>, TWriter> toDo)
    {
        using var memStream = new MemoryStream();
        var stream = GetStreamPackage(memStream);
        var writerObj = WriterKernel.GetNewObject(stream);
        toDo(WriterKernel, writerObj);
        WriterKernel.Finalize(stream, writerObj);
        if (writerObj is IDisposable disp) disp.Dispose();
        return Encoding.UTF8.GetString(memStream.ToArray());
    }

    private Dictionary<string, T?> Read<T>(string text, Func<TReaderKernel, TReader, T?> read)
    {
        var ret = new Dictionary<string, T?>();
        using var mem = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var readerObj = ReaderKernel.GetNewObject(GetStreamPackage(mem));
        using var disp = readerObj as IDisposable;
        while (ReaderKernel.TryGetNextField(readerObj, out var name))
        {
            ret[name] = read(ReaderKernel, readerObj);
        }
        return ret;
    }

    public static IEnumerable<object[]> FloatBits() => new[]
    {
        new object[] { 0x80000000u }, // -0.0
        new object[] { 0xFFC00000u }, // .NET's float.NaN
        new object[] { 0x7FC00000u }, // positive quiet NaN
        new object[] { 0xFFFFFFFFu }, // NaN as stored by some plugin floats
        new object[] { 0x7FFF7FFFu },
        new object[] { 0xFFE2FFF8u }, // NaN bits seen when OBND bytes are read as a float
        new object[] { 0x0007FFECu }, // denormal
        new object[] { 0x3F800000u }, // 1.0
        new object[] { 0x7F800000u }, // +infinity
    };

    [Theory]
    [MemberData(nameof(FloatBits))]
    public void Float_RoundTripsBits(uint bits)
    {
        var value = BitConverter.Int32BitsToSingle(unchecked((int)bits));
        var text = Write((k, w) => k.WriteFloat(w, "F", value, default(float)));
        var read = Read(text, (k, r) => k.ReadFloat(r));
        read.ShouldContainKey("F");
        unchecked((uint)BitConverter.SingleToInt32Bits(read["F"]!.Value)).ShouldBe(bits);
    }

    [Fact]
    public void Float_PositiveZeroIsStillTheDefault()
    {
        var text = Write((k, w) => k.WriteFloat(w, "F", 0f, default(float)));
        Read(text, (k, r) => k.ReadFloat(r)).ShouldNotContainKey("F");
    }

    public static IEnumerable<object[]> DoubleBits() => new[]
    {
        new object[] { 0x8000000000000000ul }, // -0.0
        new object[] { 0xFFF8000000000000ul }, // .NET's double.NaN
        new object[] { 0x7FF8000000000000ul },
        new object[] { 0xFFFFFFFFFFFFFFFFul },
        new object[] { 0x7FFF7FFF7FFF7FFFul },
        new object[] { 0x4000000000000000ul }, // 2.0
    };

    [Theory]
    [MemberData(nameof(DoubleBits))]
    public void Double_RoundTripsBits(ulong bits)
    {
        var value = BitConverter.Int64BitsToDouble(unchecked((long)bits));
        var text = Write((k, w) => k.WriteDouble(w, "D", value, default(double)));
        var read = Read(text, (k, r) => k.ReadDouble(r));
        read.ShouldContainKey("D");
        unchecked((ulong)BitConverter.DoubleToInt64Bits(read["D"]!.Value)).ShouldBe(bits);
    }

    [Fact]
    public void Double_PositiveZeroIsStillTheDefault()
    {
        var text = Write((k, w) => k.WriteDouble(w, "D", 0d, default(double)));
        Read(text, (k, r) => k.ReadDouble(r)).ShouldNotContainKey("D");
    }

    public static IEnumerable<object[]> P3FloatBits() => new[]
    {
        new object[] { 0x80000000u, 0u, 0u },                   // (-0, 0, 0)
        new object[] { 0xFFE2FFF8u, 0x0007FFECu, 0x0014001Eu }, // OBND-like bytes
        new object[] { 0xFFFFFFFFu, 0x7FFF7FFFu, 0x3F800000u },
    };

    [Theory]
    [MemberData(nameof(P3FloatBits))]
    public void P3Float_RoundTripsBits(uint x, uint y, uint z)
    {
        var value = new P3Float(
            BitConverter.Int32BitsToSingle(unchecked((int)x)),
            BitConverter.Int32BitsToSingle(unchecked((int)y)),
            BitConverter.Int32BitsToSingle(unchecked((int)z)));
        var text = Write((k, w) => k.WriteP3Float(w, "P", value, default(P3Float)));
        var read = Read(text, (k, r) => k.ReadP3Float(r));
        read.ShouldContainKey("P");
        var p = read["P"]!.Value;
        new[] { p.X, p.Y, p.Z }.Select(f => unchecked((uint)BitConverter.SingleToInt32Bits(f))).ShouldBe(new[] { x, y, z });
    }

    [Fact]
    public void P2Float_RoundTripsBits()
    {
        var value = new P2Float(-0f, BitConverter.Int32BitsToSingle(-1));
        var text = Write((k, w) => k.WriteP2Float(w, "P", value, default(P2Float)));
        var p = Read(text, (k, r) => k.ReadP2Float(r))["P"]!.Value;
        unchecked((uint)BitConverter.SingleToInt32Bits(p.X)).ShouldBe(0x80000000u);
        unchecked((uint)BitConverter.SingleToInt32Bits(p.Y)).ShouldBe(0xFFFFFFFFu);
    }

    // A file written before this change: plain numbers, "NaN", and a P3Float string.
    [Fact]
    public void OldFormat_StillReads()
    {
        var floats = Read(OldFormatText, (k, r) => k.ReadFloat(r));
        floats["F0"]!.Value.ShouldBe(0f);
        floats["F1"]!.Value.ShouldBe(-5f);
        float.IsNaN(floats["F2"]!.Value).ShouldBeTrue();
        floats["F3"]!.Value.ShouldBe(float.MaxValue);
    }

    [Fact]
    public void OldFormat_P3Float_StillReads()
    {
        var ret = new Dictionary<string, P3Float?>();
        using var mem = new MemoryStream(Encoding.UTF8.GetBytes(OldP3FloatText));
        var readerObj = ReaderKernel.GetNewObject(GetStreamPackage(mem));
        using var disp = readerObj as IDisposable;
        while (ReaderKernel.TryGetNextField(readerObj, out var name))
        {
            ret[name] = ReaderKernel.ReadP3Float(readerObj);
        }
        ret["P"]!.Value.ShouldBe(new P3Float(1, -3, 0));
        float.IsNaN(ret["Q"]!.Value.X).ShouldBeTrue();
    }

    protected abstract string OldP3FloatText { get; }

    // Bytes: empty is now written "0x" (one form with "0x0102..."); files written before say "[]", which still reads
    [Fact]
    public void OldFormat_EmptyBytes_StillReads()
    {
        var ret = new Dictionary<string, byte[]?>();
        using var mem = new MemoryStream(Encoding.UTF8.GetBytes(OldBytesText));
        var readerObj = ReaderKernel.GetNewObject(GetStreamPackage(mem));
        using var disp = readerObj as IDisposable;
        while (ReaderKernel.TryGetNextField(readerObj, out var name))
        {
            ret[name] = ReaderKernel.ReadBytes(readerObj)?.ToArray();
        }
        ret["Old"].ShouldBe(Array.Empty<byte>());
        ret["New"].ShouldBe(Array.Empty<byte>());
        ret["Some"].ShouldBe(new byte[] { 1, 0xFF });
    }

    protected abstract string OldBytesText { get; }
}

public class NewtonsoftFloatBitsTests : AFloatBitsTest<
    NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit,
    NewtonsoftJsonSerializationReaderKernel, JsonReadingUnit>
{
    protected override string OldFormatText =>
        "{\n  \"F0\": 0.0,\n  \"F1\": -5.0,\n  \"F2\": \"NaN\",\n  \"F3\": 3.4028235E+38\n}";

    protected override string OldP3FloatText =>
        "{\n  \"P\": \"1, -3, 0\",\n  \"Q\": \"NaN, NaN, NaN\"\n}";

    protected override string OldBytesText =>
        "{\n  \"Old\": \"[]\",\n  \"New\": \"0x\",\n  \"Some\": \"0x01FF\"\n}";
}

public class YamlFloatBitsTests : AFloatBitsTest<
    YamlSerializationWriterKernel, YamlWritingUnit,
    YamlSerializationReaderKernel, YamlReadingUnit>
{
    protected override string OldFormatText =>
        "F0: 0\nF1: -5\nF2: NaN\nF3: 3.4028235E+38\n";

    protected override string OldP3FloatText =>
        "P: 1, -3, 0\nQ: NaN, NaN, NaN\n";

    protected override string OldBytesText =>
        "Old: '[]'\nNew: 0x\nSome: 0x01FF\n";
}
