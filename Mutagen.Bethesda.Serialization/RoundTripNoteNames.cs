namespace Mutagen.Bethesda.Serialization;

/// <summary>
/// The names the round-trip notes are written under. A note is not a value of the object: it is what Mutagen kept about
/// how a record or a mod was stored (RoundTripMixIn, FileLayoutMixIn, CellMixIn, PackageMixIn), written beside the fields
/// so that a plugin rebuilt from the output writes back like its file. Each is written only when the object has one, and
/// none when <see cref="SerializationMetaData.RoundTripNotes"/> is off.<br/>
/// The generated serializers write and read the notes under exactly these names, so a reader of the output can tell
/// the notes from the fields by them (a field of the same name does not exist on the objects that carry the note).
/// A translated text's string ID is written inside the text's own object; its names are
/// <see cref="Utility.StringsKeyNote.Names"/>.
/// </summary>
public static class RoundTripNoteNames
{
    /// <summary>
    /// A record's header FormID as stored, as 8 hex digits, when Mutagen would write another for its FormKey (e.g. a
    /// Small plugin's own record stored with its full index; C205)
    /// </summary>
    public const string HeaderFormIdAsStored = "HeaderFormIdAsStored";

    /// <summary>
    /// The FormIDs in a record's subrecords that were stored in another form than Mutagen writes (RoundTripMixIn's
    /// opaque value, "OF1:…"; C210)
    /// </summary>
    public const string OwnLinkFormsAsStored = "OwnLinkFormsAsStored";

    /// <summary>
    /// The labels of a record's child groups stored in another form than its header (RoundTripMixIn's opaque value,
    /// "GL1:…"; C223)
    /// </summary>
    public const string GroupLabelFormsAsStored = "GroupLabelFormsAsStored";

    /// <summary>The order a record's subrecords had in its file when it isn't Mutagen's (RoundTripMixIn's opaque value; C211)</summary>
    public const string SubrecordOrder = "SubrecordOrder";

    /// <summary>
    /// A compressed record's compressed bytes as read, base64, when Mutagen's own compression would not give them again
    /// (FileLayoutMixIn.GetOriginalCompressedDataIfNotReproduced; C213)
    /// </summary>
    public const string OriginalCompressedData = "OriginalCompressedData";

    /// <summary>
    /// On a cell: the layout its children group was read with, when it differs from Mutagen's (CellMixIn's opaque value;
    /// Starfield)
    /// </summary>
    public const string ChildGroupLayout = "ChildGroupLayout";

    /// <summary>
    /// On a package: the order of its data inputs' names and flags after the procedure tree (UNAM, BNAM, PNAM) as read,
    /// when it is not by index (Package.ProcedureTreeDataOrder; C51, C59). The text and when it is written are
    /// <see cref="Utility.RoundTripNotesHelper.ProcedureTreeDataOrderToString"/>'s.
    /// </summary>
    public const string ProcedureTreeDataOrder = "ProcedureTreeDataOrder";

    /// <summary>On a mod: the empty top-level groups its file had (FileLayoutMixIn's opaque value; C215)</summary>
    public const string EmptyTopLevelGroups = "EmptyTopLevelGroups";

    /// <summary>On a mod: the form its own FormIDs were stored in, when not the default (ModOwnFormIdStyle; C211)</summary>
    public const string OwnFormIdStyle = "OwnFormIdStyle";

    /// <summary>
    /// On a mod: the header stats it was read with, "RecordsRead;HighestOwnObjectId;NextObjectId"
    /// (<see cref="Utility.RoundTripNotesHelper.HeaderStatsToString"/>; C211)
    /// </summary>
    public const string HeaderStatsBaseline = "HeaderStatsBaseline";

    /// <summary>
    /// On a mod: the overridden forms its header listed, FormKeys joined by commas
    /// (<see cref="Utility.RoundTripNotesHelper.FormKeysToString"/>; C211)
    /// </summary>
    public const string OverriddenFormsBaseline = "OverriddenFormsBaseline";

    /// <summary>
    /// The notes a record can carry, written among its fields: <see cref="HeaderFormIdAsStored"/>,
    /// <see cref="OwnLinkFormsAsStored"/>, <see cref="GroupLabelFormsAsStored"/>, <see cref="SubrecordOrder"/>,
    /// <see cref="OriginalCompressedData"/>, and on a cell <see cref="ChildGroupLayout"/>, on a package
    /// <see cref="ProcedureTreeDataOrder"/>
    /// </summary>
    public static IReadOnlySet<string> RecordNotes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        HeaderFormIdAsStored,
        OwnLinkFormsAsStored,
        GroupLabelFormsAsStored,
        SubrecordOrder,
        OriginalCompressedData,
        ChildGroupLayout,
        ProcedureTreeDataOrder,
    };

    /// <summary>
    /// The notes a mod can carry, written among its fields: <see cref="EmptyTopLevelGroups"/>,
    /// <see cref="OwnFormIdStyle"/>, <see cref="HeaderStatsBaseline"/>, <see cref="OverriddenFormsBaseline"/>
    /// </summary>
    public static IReadOnlySet<string> ModNotes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        EmptyTopLevelGroups,
        OwnFormIdStyle,
        HeaderStatsBaseline,
        OverriddenFormsBaseline,
    };

    /// <summary>Every note name: <see cref="RecordNotes"/> and <see cref="ModNotes"/></summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(RecordNotes.Concat(ModNotes), StringComparer.Ordinal);
}
