using System.Globalization;
using Newtonsoft.Json;

namespace Mutagen.Bethesda.Serialization.Newtonsoft;

/// <summary>
/// The JSON text of a float, a double and a Half exactly as <see cref="NewtonsoftJsonSerializationWriterKernel"/> writes
/// it (the kernel writes through these methods), and its reading back.
/// <list type="bullet">
/// <item>A number is a JSON number: the fewest digits that give the value back ("R"), with ".0" added to a whole number
/// (1 is "1.0", 1E+20 stays "1E+20"), -0 as "-0.0".</item>
/// <item>NaN and the infinities are JSON strings: "NaN", "Infinity", "-Infinity".</item>
/// <item>A NaN whose bits are not .NET's own NaN is a string of "NaN:" and its bits in hex
/// (<see cref="FloatSerialization.ToBitForm(float)"/>), e.g. "NaN:FFFFFFFF".</item>
/// <item>A Half is written as the double that holds the same number (every Half is exactly a double; C222).</item>
/// </list>
/// The YAML kernel writes <see cref="FloatSerialization.ToString(float, IFormatProvider)"/> instead (no ".0").
/// </summary>
public static class JsonScalarText
{
    /// <summary>The float's JSON text: a number, or a string with its quotes</summary>
    public static string Format(float value)
    {
        if (FloatSerialization.NeedsBitForm(value)) return Quote(FloatSerialization.ToBitForm(value));
        if (float.IsNaN(value) || float.IsInfinity(value)) return Quote(value.ToString("R", CultureInfo.InvariantCulture));
        return JsonConvert.ToString(value);
    }

    /// <summary>The double's JSON text: a number, or a string with its quotes</summary>
    public static string Format(double value)
    {
        if (FloatSerialization.NeedsBitForm(value)) return Quote(FloatSerialization.ToBitForm(value));
        if (double.IsNaN(value) || double.IsInfinity(value)) return Quote(value.ToString("R", CultureInfo.InvariantCulture));
        return JsonConvert.ToString(value);
    }

    /// <summary>The Half's JSON text: the double holding its number, or a string with its quotes</summary>
    public static string Format(Half value)
    {
        if (FloatSerialization.NeedsBitForm(value)) return Quote(FloatSerialization.ToBitForm(value));
        return Format(FloatSerialization.ToDouble(value));
    }

    /// <summary>
    /// A float back from its JSON text, as the JSON reader kernel reads it: a number's text, or a string's content
    /// (its quotes may be left on)
    /// </summary>
    public static bool TryParseFloat(string? text, out float value)
    {
        value = default;
        if (!TryGetContent(text, out var s)) return false;
        if (IsBitForm(s)) return FloatSerialization.TryParseBitForm(s, out value);
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return false;
        // The reader kernel is given the number as a double (Newtonsoft's parse) and narrows it
        value = (float)d;
        return true;
    }

    /// <summary>
    /// A double back from its JSON text, as the JSON reader kernel reads it: a number's text, or a string's content
    /// (its quotes may be left on)
    /// </summary>
    public static bool TryParseDouble(string? text, out double value)
    {
        value = default;
        if (!TryGetContent(text, out var s)) return false;
        if (IsBitForm(s)) return FloatSerialization.TryParseBitForm(s, out value);
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// A Half back from its JSON text, as the JSON reader kernel reads it: a number's text, or a string's content
    /// (its quotes may be left on)
    /// </summary>
    public static bool TryParseHalf(string? text, out Half value)
    {
        value = default;
        if (!TryGetContent(text, out var s)) return false;
        if (IsBitForm(s)) return FloatSerialization.TryParseBitForm(s, out value);
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return false;
        value = FloatSerialization.FromDouble(d);
        return true;
    }

    private static bool TryGetContent(string? text, out string content)
    {
        if (text == null)
        {
            content = string.Empty;
            return false;
        }
        content = Unquote(text.Trim()).Trim();
        return content.Length > 0;
    }

    private static bool IsBitForm(string s) => s.StartsWith(FloatSerialization.NaNBitsPrefix, StringComparison.OrdinalIgnoreCase);

    private static string Quote(string s) => "\"" + s + "\"";

    private static string Unquote(string s) =>
        s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s.Substring(1, s.Length - 2) : s;
}
