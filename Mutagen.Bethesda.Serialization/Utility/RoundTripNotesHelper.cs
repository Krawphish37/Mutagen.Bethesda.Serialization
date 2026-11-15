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

    /// <summary>
    /// The text of a package's <c>ProcedureTreeDataOrder</c> note (C51, C59): the order its data inputs' names and flags
    /// (UNAM, BNAM, PNAM) had after the procedure tree as read, the indexes as signed decimals joined by commas
    /// ("2,0,-1"). Null, so that no note is written, when there is no order or it is by index both ways: ascending as
    /// signed and as unsigned bytes (e.g. 0,1,3; not 1,-1 nor -1,1). Without an order Mutagen writes the inputs by index,
    /// as unsigned bytes for Starfield and as signed ones for Skyrim and Fallout4; an order ascending both ways is that
    /// order for every game, so leaving it out changes nothing written. Any other order is written, also one that does
    /// not hold the package's indexes (Mutagen's write passes over it as it does over the one read).
    /// </summary>
    public static string? ProcedureTreeDataOrderToString(IReadOnlyList<sbyte>? order)
    {
        if (order == null) return null;
        bool byIndex = true;
        for (int i = 1; i < order.Count; i++)
        {
            if (order[i - 1] >= order[i] || (byte)order[i - 1] >= (byte)order[i])
            {
                byIndex = false;
                break;
            }
        }
        if (byIndex) return null;
        return string.Join(",", order.Select(i => i.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>The order written by <see cref="ProcedureTreeDataOrderToString"/>; null for no text</summary>
    /// <exception cref="FormatException">An index is not a signed byte</exception>
    public static IReadOnlyList<sbyte>? ProcedureTreeDataOrderFromString(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        return text.Split(',').Select(s => sbyte.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)).ToArray();
    }
}
