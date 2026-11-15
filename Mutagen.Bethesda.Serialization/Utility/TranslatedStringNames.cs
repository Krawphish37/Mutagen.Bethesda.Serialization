namespace Mutagen.Bethesda.Serialization.Utility;

/// <summary>
/// How a translated text (<see cref="Mutagen.Bethesda.Strings.ITranslatedStringGetter"/>) is written: an object (a
/// mapping in YAML) holding <see cref="TargetLanguage"/>, then <see cref="Value"/> (one language) or <see cref="Values"/>
/// (a list of objects of <see cref="Language"/> and <see cref="String"/>), then its strings ID when it has one
/// (<see cref="StringsKeyNote.Names"/>). A null text is an empty object. The JSON and YAML kernels write and read these
/// names.
/// </summary>
public static class TranslatedStringNames
{
    /// <summary>The language the text is in (Language enum name)</summary>
    public const string TargetLanguage = "TargetLanguage";

    /// <summary>The text, when it holds one language</summary>
    public const string Value = "Value";

    /// <summary>The texts, when it holds more than one language: a list of <see cref="Language"/> / <see cref="String"/></summary>
    public const string Values = "Values";

    /// <summary>In an item of <see cref="Values"/>: the language</summary>
    public const string Language = "Language";

    /// <summary>In an item of <see cref="Values"/>: the text in that language</summary>
    public const string String = "String";

    /// <summary>
    /// True for the names of a written translated text's object: it has <see cref="TargetLanguage"/>, and every name is
    /// <see cref="TargetLanguage"/>, <see cref="Value"/>, <see cref="Values"/> or one of <see cref="StringsKeyNote.Names"/>
    /// </summary>
    public static bool IsTranslatedStringObject(IEnumerable<string> names)
    {
        bool hasTarget = false;
        foreach (var name in names)
        {
            switch (name)
            {
                case TargetLanguage:
                    hasTarget = true;
                    break;
                case Value:
                case Values:
                    break;
                default:
                    if (!StringsKeyNote.Names.Contains(name)) return false;
                    break;
            }
        }
        return hasTarget;
    }
}
