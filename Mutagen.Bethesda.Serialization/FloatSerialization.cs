using System.Globalization;
using Noggog;

namespace Mutagen.Bethesda.Serialization;

/// <summary>
/// Bit-exact handling of floats and doubles for serialization.
/// <list type="bullet">
/// <item>Default checks compare bits, so -0.0 is not taken for the default 0.0 (and left out).</item>
/// <item>A NaN whose bits differ from .NET's own NaN (float FFC00000, double FFF8000000000000) is written as
/// "NaN:" plus its bits in hex (e.g. "NaN:FFFFFFFF"), so its payload survives. Every other value is written as before,
/// and older files (plain numbers, "NaN") still read.</item>
/// </list>
/// </summary>
public static class FloatSerialization
{
    public const string NaNBitsPrefix = "NaN:";

    private static readonly int StandardFloatNaNBits = BitConverter.SingleToInt32Bits(float.NaN);
    private static readonly long StandardDoubleNaNBits = BitConverter.DoubleToInt64Bits(double.NaN);

    public static bool BitEquals(float a, float b) =>
        BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

    public static bool BitEquals(float? a, float? b) =>
        a.HasValue ? b.HasValue && BitEquals(a.Value, b.Value) : !b.HasValue;

    public static bool BitEquals(double a, double b) =>
        BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    public static bool BitEquals(double? a, double? b) =>
        a.HasValue ? b.HasValue && BitEquals(a.Value, b.Value) : !b.HasValue;

    public static bool BitEquals(P2Float a, P2Float b) => BitEquals(a.X, b.X) && BitEquals(a.Y, b.Y);

    public static bool BitEquals(P2Float? a, P2Float? b) =>
        a.HasValue ? b.HasValue && BitEquals(a.Value, b.Value) : !b.HasValue;

    public static bool BitEquals(P3Float a, P3Float b) =>
        BitEquals(a.X, b.X) && BitEquals(a.Y, b.Y) && BitEquals(a.Z, b.Z);

    public static bool BitEquals(P3Float? a, P3Float? b) =>
        a.HasValue ? b.HasValue && BitEquals(a.Value, b.Value) : !b.HasValue;

    /// <summary>True for a NaN that a plain "NaN" would not bring back bit for bit.</summary>
    public static bool NeedsBitForm(float f) =>
        float.IsNaN(f) && BitConverter.SingleToInt32Bits(f) != StandardFloatNaNBits;

    public static bool NeedsBitForm(double d) =>
        double.IsNaN(d) && BitConverter.DoubleToInt64Bits(d) != StandardDoubleNaNBits;

    public static string ToBitForm(float f) =>
        NaNBitsPrefix + unchecked((uint)BitConverter.SingleToInt32Bits(f)).ToString("X8", CultureInfo.InvariantCulture);

    public static string ToBitForm(double d) =>
        NaNBitsPrefix + unchecked((ulong)BitConverter.DoubleToInt64Bits(d)).ToString("X16", CultureInfo.InvariantCulture);

    public static bool TryParseBitForm(string? str, out float f)
    {
        f = default;
        if (str == null) return false;
        var s = str.Trim();
        if (!s.StartsWith(NaNBitsPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        if (!uint.TryParse(s.AsSpan(NaNBitsPrefix.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits)) return false;
        f = BitConverter.Int32BitsToSingle(unchecked((int)bits));
        return true;
    }

    public static bool TryParseBitForm(string? str, out double d)
    {
        d = default;
        if (str == null) return false;
        var s = str.Trim();
        if (!s.StartsWith(NaNBitsPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        if (!ulong.TryParse(s.AsSpan(NaNBitsPrefix.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits)) return false;
        d = BitConverter.Int64BitsToDouble(unchecked((long)bits));
        return true;
    }

    public static string ToString(float f, IFormatProvider provider) =>
        NeedsBitForm(f) ? ToBitForm(f) : f.ToString(provider);

    public static string ToString(double d, IFormatProvider provider) =>
        NeedsBitForm(d) ? ToBitForm(d) : d.ToString(provider);

    public static float ParseFloat(string str, IFormatProvider? provider = null)
    {
        if (TryParseBitForm(str, out float f)) return f;
        if (float.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, provider, out f)) return f;
        // "Infinity" / "-Infinity" as Newtonsoft writes them (the current culture may use another symbol)
        return float.Parse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
    }

    public static double ParseDouble(string str, IFormatProvider? provider = null)
    {
        if (TryParseBitForm(str, out double d)) return d;
        if (double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, provider, out d)) return d;
        return double.Parse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string str, out float f, IFormatProvider provider) =>
        TryParseBitForm(str, out f)
        || float.TryParse(str.Trim(), NumberStyles.Float, provider, out f)
        || float.TryParse(str.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);

    /// <summary>"X, Y", with the NaN bit form where needed.</summary>
    public static string ToString(P2Float p, IFormatProvider provider) =>
        $"{ToString(p.X, provider)}, {ToString(p.Y, provider)}";

    /// <summary>"X, Y, Z", with the NaN bit form where needed.</summary>
    public static string ToString(P3Float p, IFormatProvider provider) =>
        $"{ToString(p.X, provider)}, {ToString(p.Y, provider)}, {ToString(p.Z, provider)}";

    public static bool TryParse(string str, out P2Float p, IFormatProvider provider)
    {
        if (!str.Contains(NaNBitsPrefix, StringComparison.OrdinalIgnoreCase) && P2Float.TryParse(str, out p, provider)) return true;
        p = default;
        var parts = str.Split(',');
        if (parts.Length == 2
            && TryParse(parts[0], out float x, provider)
            && TryParse(parts[1], out float y, provider))
        {
            p = new P2Float(x, y);
            return true;
        }
        return false;
    }

    public static bool TryParse(string str, out P3Float p, IFormatProvider provider)
    {
        if (!str.Contains(NaNBitsPrefix, StringComparison.OrdinalIgnoreCase) && P3Float.TryParse(str, out p, provider)) return true;
        p = default;
        var parts = str.Split(',');
        if (parts.Length == 3
            && TryParse(parts[0], out float x, provider)
            && TryParse(parts[1], out float y, provider)
            && TryParse(parts[2], out float z, provider))
        {
            p = new P3Float(x, y, z);
            return true;
        }
        return false;
    }
}
