using System.Globalization;
using System.Text;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Serialization.Streams;
using Newtonsoft.Json;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Tests.KernelTests.Newtonsoft;

// JsonScalarText (C59, W14): the public text of a float, double and Half in the JSON. The kernel writes through it, so
// these tests check that the text is the one the kernel wrote before (Newtonsoft's own number text and the bit form),
// that it reads back bit for bit, that it reads back as the reader kernel reads it, and that it gives what SFSearch's
// own copies of the format gave.
public class JsonScalarTextTests
{
    private static readonly NewtonsoftJsonSerializationReaderKernel ReaderKernel = new();

    public static IEnumerable<float> Floats()
    {
        var fixedBits = new uint[]
        {
            0x00000000, 0x80000000, 0x3F800000, 0xBF800000, 0x3DCCCCCD, 0x00000001, 0x0007FFEC, 0x7F7FFFFF,
            0xFF7FFFFF, 0x7F800000, 0xFF800000, 0xFFC00000, 0x7FC00000, 0xFFFFFFFF, 0x7FFF7FFF, 0xFFE2FFF8,
            0x4B189680, 0x60AD78EC, 0x2EDBE6FF,
        };
        foreach (var b in fixedBits) yield return BitConverter.Int32BitsToSingle(unchecked((int)b));
        var random = new Random(59);
        for (int i = 0; i < 4000; i++) yield return BitConverter.Int32BitsToSingle(random.Next(int.MinValue, int.MaxValue));
        foreach (var f in new[] { 1f, 0.1f, 123456789f, 1e20f, -1e-10f, 3.4028235E+38f, 1.17549435E-38f, 0.5f, 2.25f })
            yield return f;
    }

    public static IEnumerable<double> Doubles()
    {
        var fixedBits = new ulong[]
        {
            0x0000000000000000, 0x8000000000000000, 0x3FF0000000000000, 0xFFF8000000000000, 0x7FF8000000000000,
            0xFFFFFFFFFFFFFFFF, 0x7FFF7FFF7FFF7FFF, 0x7FF0000000000000, 0xFFF0000000000000, 0x0000000000000001,
            0x7FEFFFFFFFFFFFFF,
        };
        foreach (var b in fixedBits) yield return BitConverter.Int64BitsToDouble(unchecked((long)b));
        var random = new Random(5959);
        var buf = new byte[8];
        for (int i = 0; i < 4000; i++)
        {
            random.NextBytes(buf);
            yield return BitConverter.ToDouble(buf);
        }
        foreach (var d in new[] { 1d, 0.1d, 1e20d, 1e300d, -2.5d, 1d / 3 }) yield return d;
    }

    // Every Half there is
    public static IEnumerable<Half> Halves() =>
        Enumerable.Range(0, 1 << 16).Select(i => BitConverter.UInt16BitsToHalf((ushort)i));

    // What NewtonsoftJsonSerializationWriterKernel wrote before C59 (1.38.6-fixes.4): Newtonsoft's own WriteValue, and
    // the bit form as a string
    private static string OldText(float f)
    {
        var sw = new StringWriter();
        using var w = new JsonTextWriter(sw) { Culture = TargetCulture.English };
        if (FloatSerialization.NeedsBitForm(f)) w.WriteValue(FloatSerialization.ToBitForm(f));
        else w.WriteValue(f);
        w.Flush();
        return sw.ToString();
    }

    private static string OldText(double d)
    {
        var sw = new StringWriter();
        using var w = new JsonTextWriter(sw) { Culture = TargetCulture.English };
        if (FloatSerialization.NeedsBitForm(d)) w.WriteValue(FloatSerialization.ToBitForm(d));
        else w.WriteValue(d);
        w.Flush();
        return sw.ToString();
    }

    private static string OldText(Half h)
    {
        var sw = new StringWriter();
        using var w = new JsonTextWriter(sw) { Culture = TargetCulture.English };
        if (FloatSerialization.NeedsBitForm(h)) w.WriteValue(FloatSerialization.ToBitForm(h));
        else w.WriteValue(FloatSerialization.ToDouble(h));
        w.Flush();
        return sw.ToString();
    }

    private static uint Bits(float f) => unchecked((uint)BitConverter.SingleToInt32Bits(f));
    private static ulong Bits(double d) => unchecked((ulong)BitConverter.DoubleToInt64Bits(d));
    private static ushort Bits(Half h) => BitConverter.HalfToUInt16Bits(h);

    // The kernel's whole output for one field, as a reader of the JSON sees it
    private static string KernelJson(Action<MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>, JsonWritingUnit> write)
    {
        var kernel = MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>.Instance;
        using var mem = new MemoryStream();
        var package = new StreamPackage(mem, null);
        var w = kernel.GetNewObject(package);
        write(kernel, w);
        kernel.Finalize(package, w);
        return Encoding.UTF8.GetString(mem.ToArray());
    }

    private static T ReadBack<T>(string token, Func<JsonReadingUnit, T> read)
    {
        using var mem = new MemoryStream(Encoding.UTF8.GetBytes("{\"V\": " + token + "}"));
        var r = ReaderKernel.GetNewObject(new StreamPackage(mem, null));
        ReaderKernel.TryGetNextField(r, out var name).ShouldBeTrue();
        name.ShouldBe("V");
        return read(r);
    }

    [Fact]
    public void Float_TextIsUnchanged_ReadsBackAsTheReaderKernelDoes()
    {
        foreach (var f in Floats())
        {
            var text = JsonScalarText.Format(f);
            text.ShouldBe(OldText(f), $"bits {Bits(f):X8}");
            JsonScalarText.TryParseFloat(text, out var back).ShouldBeTrue(text);
            Bits(back).ShouldBe(Bits(f), text);
            Bits(ReadBack(text, r => ReaderKernel.ReadFloat(r)!.Value)).ShouldBe(Bits(back), text);
        }
    }

    [Fact]
    public void Double_TextIsUnchanged_ReadsBackAsTheReaderKernelDoes()
    {
        foreach (var d in Doubles())
        {
            var text = JsonScalarText.Format(d);
            text.ShouldBe(OldText(d), $"bits {Bits(d):X16}");
            JsonScalarText.TryParseDouble(text, out var back).ShouldBeTrue(text);
            Bits(back).ShouldBe(Bits(d), text);
            Bits(ReadBack(text, r => ReaderKernel.ReadDouble(r)!.Value)).ShouldBe(Bits(back), text);
        }
    }

    [Fact]
    public void Half_TextIsUnchanged_ReadsBackAsTheReaderKernelDoes()
    {
        foreach (var h in Halves())
        {
            var text = JsonScalarText.Format(h);
            text.ShouldBe(OldText(h), $"bits {Bits(h):X4}");
            JsonScalarText.TryParseHalf(text, out var back).ShouldBeTrue(text);
            Bits(back).ShouldBe(Bits(h), text);
            Bits(ReadBack(text, r => ReaderKernel.ReadHalf(r)!.Value)).ShouldBe(Bits(back), text);
        }
    }

    [Fact]
    public void KernelWritesTheText()
    {
        var f = BitConverter.Int32BitsToSingle(unchecked((int)0xFFFFFFFF));
        var json = KernelJson((k, w) =>
        {
            k.WriteFloat(w, "F", 1f, default(float));
            k.WriteFloat(w, "N", f, default(float));
            k.WriteDouble(w, "D", -0d, default(double));
            k.WriteHalf(w, "H", (Half)0.5f, default(Half));
        });
        json.ShouldContain($"\"F\": {JsonScalarText.Format(1f)}");
        json.ShouldContain($"\"N\": {JsonScalarText.Format(f)}");
        json.ShouldContain($"\"D\": {JsonScalarText.Format(-0d)}");
        json.ShouldContain($"\"H\": {JsonScalarText.Format((Half)0.5f)}");
        JsonScalarText.Format(1f).ShouldBe("1.0");
        JsonScalarText.Format(f).ShouldBe("\"NaN:FFFFFFFF\"");
        JsonScalarText.Format(-0d).ShouldBe("-0.0");
    }

    [Fact]
    public void ParseTakesStringContentWithOrWithoutQuotes()
    {
        JsonScalarText.TryParseFloat("NaN:FFFFFFFF", out var a).ShouldBeTrue();
        JsonScalarText.TryParseFloat("\"NaN:FFFFFFFF\"", out var b).ShouldBeTrue();
        Bits(a).ShouldBe(0xFFFFFFFFu);
        Bits(b).ShouldBe(0xFFFFFFFFu);
        JsonScalarText.TryParseDouble("\"-Infinity\"", out var d).ShouldBeTrue();
        double.IsNegativeInfinity(d).ShouldBeTrue();
        JsonScalarText.TryParseHalf("", out _).ShouldBeFalse();
        JsonScalarText.TryParseHalf(null, out _).ShouldBeFalse();
        JsonScalarText.TryParseHalf("x", out _).ShouldBeFalse();
    }

    // SFSearch.Query DirectColumns.Real (agent-first-integration 7fc0119): "a float as the serializer writes it ...
    // read back as SQLite reads a JSON number", copied here as SFSearch has it
    private static object SfSearchReal(object v)
    {
        string text;
        switch (v)
        {
            case float f:
                if (float.IsNaN(f)) return "NaN";
                if (float.IsPositiveInfinity(f)) return "Infinity";
                if (float.IsNegativeInfinity(f)) return "-Infinity";
                text = f.ToString("R", CultureInfo.InvariantCulture);
                break;
            case double x:
                if (double.IsNaN(x)) return "NaN";
                if (double.IsPositiveInfinity(x)) return "Infinity";
                if (double.IsNegativeInfinity(x)) return "-Infinity";
                text = x.ToString("R", CultureInfo.InvariantCulture);
                break;
            case Half h:
                if (Half.IsNaN(h)) return BitConverter.HalfToUInt16Bits(h) == BitConverter.HalfToUInt16Bits(Half.NaN) ? "NaN" : "NaN:" + BitConverter.HalfToUInt16Bits(h).ToString("X4", CultureInfo.InvariantCulture);
                if (Half.IsPositiveInfinity(h)) return "Infinity";
                if (Half.IsNegativeInfinity(h)) return "-Infinity";
                text = ((double)h).ToString("R", CultureInfo.InvariantCulture);
                break;
            default: throw new InvalidOperationException();
        }
        return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    // The same with JsonScalarText: a JSON number's text read as a double, a JSON string as its content
    private static object FromLibrary(string token)
    {
        if (token.StartsWith('"')) return token.Substring(1, token.Length - 2);
        JsonScalarText.TryParseDouble(token, out var d).ShouldBeTrue();
        return d;
    }

    [Fact]
    public void GivesWhatSfSearchsCopyOfTheFormatGave()
    {
        foreach (var f in Floats())
        {
            // SFSearch's copy has no float bit form: its NaN payloads were all "NaN"
            if (FloatSerialization.NeedsBitForm(f)) continue;
            FromLibrary(JsonScalarText.Format(f)).ShouldBe(SfSearchReal(f), $"bits {Bits(f):X8}");
        }
        foreach (var d in Doubles())
        {
            if (FloatSerialization.NeedsBitForm(d)) continue;
            FromLibrary(JsonScalarText.Format(d)).ShouldBe(SfSearchReal(d), $"bits {Bits(d):X16}");
        }
        foreach (var h in Halves())
        {
            FromLibrary(JsonScalarText.Format(h)).ShouldBe(SfSearchReal(h), $"bits {Bits(h):X4}");
        }
    }

    // SFSearch.Records FieldLookup.TryReadHalf (7fc0119): the serializer's Half text parsed back, as SFSearch has it
    private static bool SfSearchTryReadHalf(System.Text.Json.JsonElement el, out Half h)
    {
        h = default;
        if (el.ValueKind == System.Text.Json.JsonValueKind.Number) { h = (Half)el.GetDouble(); return true; }
        if (el.ValueKind != System.Text.Json.JsonValueKind.String) return false;
        string s = el.GetString()!.Trim();
        if (s.StartsWith("NaN:", StringComparison.OrdinalIgnoreCase))
        {
            if (!ushort.TryParse(s.AsSpan(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort bits)) return false;
            h = BitConverter.UInt16BitsToHalf(bits);
            return true;
        }
        switch (s)
        {
            case "Infinity": h = Half.PositiveInfinity; return true;
            case "-Infinity": h = Half.NegativeInfinity; return true;
            case "NaN": h = Half.NaN; return true;
        }
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return false;
        h = (Half)d;
        return true;
    }

    [Fact]
    public void HalfReadsAsSfSearchsCopyReadIt()
    {
        foreach (var h in Halves())
        {
            var token = JsonScalarText.Format(h);
            using var doc = System.Text.Json.JsonDocument.Parse("{\"V\": " + token + "}");
            var el = doc.RootElement.GetProperty("V");
            SfSearchTryReadHalf(el, out var theirs).ShouldBeTrue(token);
            var text = el.ValueKind == System.Text.Json.JsonValueKind.Number ? el.GetRawText() : el.GetString();
            JsonScalarText.TryParseHalf(text, out var ours).ShouldBeTrue(token);
            Bits(ours).ShouldBe(Bits(theirs), token);
        }
    }
}
