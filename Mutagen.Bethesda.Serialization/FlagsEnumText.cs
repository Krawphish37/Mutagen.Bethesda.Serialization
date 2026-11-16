using System.Globalization;
using System.Runtime.CompilerServices;

namespace Mutagen.Bethesda.Serialization;

/// <summary>
/// The text form of a [Flags] enum's value, as the JSON and YAML kernels write and read it (C72): a list with one item
/// per set bit, lowest bit first. A bit with a member of its own is written as that member's name (of several names for
/// one value, the one declared last among them in value order, as before); any other bit as "0x" and its hex digits,
/// e.g. "0x80000000".<br/>
/// The bits are walked over the enum's underlying value itself, for any underlying type, signed or not, all of its
/// bits. Nothing here uses Noggog's enum helpers: their flag table never finishes for an int-based enum with a member
/// at bit 31 (fixes.20's NavigationMesh/BiomeMarker MajorFlag), and for a value with the sign bit set they also gave a
/// spurious "0x0" item. Output is otherwise the same text as before, and the old text (with its "0x0") still reads.
/// </summary>
public static class FlagsEnumText
{
    private static class Of<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly bool IsFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);

        public static readonly int Bits = Unsafe.SizeOf<TEnum>() * 8;

        /// <summary>A member's name by its bits; for several names of one value, the last in value order</summary>
        public static readonly Dictionary<ulong, string> Names = BuildNames();

        private static Dictionary<ulong, string> BuildNames()
        {
            var ret = new Dictionary<ulong, string>();
            var names = Enum.GetNames<TEnum>();
            var values = Enum.GetValues<TEnum>();
            for (int i = 0; i < names.Length; i++)
            {
                ret[ToBits(values[i])] = names[i];
            }
            return ret;
        }
    }

    /// <summary>Whether the enum carries [Flags] (written as a list of flags)</summary>
    public static bool IsFlags<TEnum>()
        where TEnum : struct, Enum
        => Of<TEnum>.IsFlags;

    /// <summary>The enum value's bits, as an unsigned number of the enum's own width (an int -1 is 0xFFFFFFFF)</summary>
    public static ulong ToBits<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return Unsafe.SizeOf<TEnum>() switch
        {
            1 => Unsafe.As<TEnum, byte>(ref value),
            2 => Unsafe.As<TEnum, ushort>(ref value),
            4 => Unsafe.As<TEnum, uint>(ref value),
            _ => Unsafe.As<TEnum, ulong>(ref value),
        };
    }

    /// <summary>The enum value of these bits (bits past the enum's width are dropped)</summary>
    public static TEnum FromBits<TEnum>(ulong bits)
        where TEnum : struct, Enum
    {
        switch (Unsafe.SizeOf<TEnum>())
        {
            case 1:
            {
                var b = unchecked((byte)bits);
                return Unsafe.As<byte, TEnum>(ref b);
            }
            case 2:
            {
                var s = unchecked((ushort)bits);
                return Unsafe.As<ushort, TEnum>(ref s);
            }
            case 4:
            {
                var i = unchecked((uint)bits);
                return Unsafe.As<uint, TEnum>(ref i);
            }
            default:
                return Unsafe.As<ulong, TEnum>(ref bits);
        }
    }

    /// <summary>One item per set bit, lowest first: the bit's member name, or "0x" and its hex digits</summary>
    public static IEnumerable<string> ToTexts<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var bits = ToBits(value);
        var width = Of<TEnum>.Bits;
        for (int i = 0; i < width && bits != 0; i++)
        {
            var bit = 1UL << i;
            if ((bits & bit) == 0) continue;
            bits &= ~bit;
            yield return Of<TEnum>.Names.TryGetValue(bit, out var name)
                ? name
                : "0x" + bit.ToString("X", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Adds one item of the list to <paramref name="bits"/>: a member name (or a comma list of them), "0x" and hex
    /// digits, or a decimal number (also negative). False for anything else.
    /// </summary>
    public static bool TryAdd<TEnum>(ReadOnlySpan<char> text, ref ulong bits)
        where TEnum : struct, Enum
    {
        var s = text.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!ulong.TryParse(s[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex)) return false;
            bits |= hex;
            return true;
        }
        if (Enum.TryParse<TEnum>(s, out var named))
        {
            bits |= ToBits(named);
            return true;
        }
        if (long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            bits |= ToBits(FromBits<TEnum>(unchecked((ulong)number)));
            return true;
        }
        if (ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var unsigned))
        {
            bits |= unsigned;
            return true;
        }
        return false;
    }
}
