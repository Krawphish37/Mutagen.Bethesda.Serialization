using System.Globalization;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Strings;

namespace Mutagen.Bethesda.Serialization.Utility;

/// <summary>
/// A translated text's ID in its plugin's .strings tables (TranslatedString.StringsKey, its table and its plugin),
/// carried next to the text so a localized plugin rebuilt from JSON or YAML writes each text under the ID it had and
/// comes back as its file (C214). Written only for a text that has one, so other output is unchanged. The .strings
/// files themselves are not part of the output.
/// </summary>
public static class StringsKeyNote
{
    public const string KeyName = "StringsKey";
    public const string SourceName = "StringsSource";
    public const string PluginName = "StringsPlugin";

    /// <summary>
    /// The three names a text's strings ID is written under, inside the text's object: <see cref="KeyName"/>,
    /// <see cref="SourceName"/>, <see cref="PluginName"/>
    /// </summary>
    public static IReadOnlySet<string> Names { get; } = new HashSet<string>(StringComparer.Ordinal) { KeyName, SourceName, PluginName };

    /// <summary>
    /// The ID of a text that has one, with the name of its table and its plugin's file name
    /// </summary>
    public static bool TryGet(ITranslatedStringGetter item, out uint key, out string source, out string plugin)
    {
        if (item is TranslatedString { StringsKey: { } k, StringsKeyPlugin: { } p } translated)
        {
            key = k;
            source = translated.StringsKeySource.ToString();
            plugin = p.FileName.String;
            return true;
        }
        key = 0;
        source = string.Empty;
        plugin = string.Empty;
        return false;
    }

    /// <summary>
    /// Gives a text read back the ID written by <see cref="TryGet"/>; nothing when there was none
    /// </summary>
    /// <exception cref="FormatException">A part is missing or isn't one TryGet writes</exception>
    public static void Set(TranslatedString item, string? key, string? source, string? plugin)
    {
        if (key == null && source == null && plugin == null) return;
        if (key == null || source == null || plugin == null)
        {
            throw new FormatException($"A text's strings ID needs {KeyName}, {SourceName} and {PluginName}: {key}, {source}, {plugin}");
        }
        if (!Enum.TryParse<StringsSource>(source, out var stringsSource) || !Enum.IsDefined(stringsSource))
        {
            throw new FormatException($"Not a strings table: {source}");
        }
        item.SetStringsKey(
            uint.Parse(key, NumberStyles.None, CultureInfo.InvariantCulture),
            stringsSource,
            ModKey.FromNameAndExtension(plugin));
    }
}
