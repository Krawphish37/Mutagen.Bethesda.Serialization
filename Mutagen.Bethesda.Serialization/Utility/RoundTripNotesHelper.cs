using System.Globalization;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;

namespace Mutagen.Bethesda.Serialization.Utility;

/// <summary>
/// The text form of what Mutagen keeps about a mod beyond its fields (RoundTripMixIn), so that a mod rebuilt from its
/// JSON writes back like the file it was read from (C211). Each is written only when Mutagen has it.
/// </summary>
public static class RoundTripNotesHelper
{
    /// <summary>
    /// "RecordsRead;HighestOwnObjectId;NextObjectId", the IDs in hex, the highest own ID empty when the mod had none
    /// </summary>
    public static string? HeaderStatsToString(ModHeaderStatsBaseline? baseline)
    {
        if (baseline == null) return null;
        return string.Create(CultureInfo.InvariantCulture,
            $"{baseline.RecordsRead};{(baseline.HighestOwnObjectId is { } highest ? highest.ToString("X") : "")};{baseline.NextObjectId:X}");
    }

    public static ModHeaderStatsBaseline? HeaderStatsFromString(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var parts = text.Split(';');
        if (parts.Length != 3) throw new FormatException($"Not a header stats baseline: {text}");
        return new ModHeaderStatsBaseline(
            uint.Parse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture),
            parts[1].Length == 0 ? null : uint.Parse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            uint.Parse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The FormKeys separated by commas, in the order given
    /// </summary>
    public static string? FormKeysToString(IReadOnlyList<FormKey>? formKeys)
    {
        return formKeys == null ? null : string.Join(",", formKeys.Select(k => k.ToString()));
    }

    public static IReadOnlyList<FormKey>? FormKeysFromString(string? text)
    {
        if (text == null) return null;
        if (text.Length == 0) return Array.Empty<FormKey>();
        return text.Split(',').Select(s => FormKey.Factory(s)).ToArray();
    }
}
