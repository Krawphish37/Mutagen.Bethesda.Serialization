using System.Drawing;
using System.Globalization;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Strings;
using Noggog;

namespace Mutagen.Bethesda.Serialization;

/// <summary>
/// A link of an object and the field it sits in: the field's path in the object's JSON (or YAML) as the generated
/// serializer writes it, e.g. <c>Keywords[3]</c>, <c>Effects[0].Data.MagicEffect</c>, <c>BipedObjects[2].Value.Name</c>
/// (a dictionary entry is an item of Key and Value), <c>Grid[1][2]</c>; <c>[*]</c> for every list index when paths are
/// asked for without indexes (C59, W12)
/// </summary>
public readonly record struct FormLinkPlace(string Path, FormKey FormKey);

/// <summary>
/// The links of an object with the field each sits in (<see cref="FormLinkPlace"/>), found by walking the object with
/// the serializer generated for its class (C59, W12). So a link's path is exactly its path in the object's JSON, the
/// serializer's naming, and every link the JSON holds is found; nothing is formatted or written.<br/>
/// A null link is left out, and so is a record's own FormKey (it is no link). By default a record nested in the one
/// walked (a placed object in a cell, a topic in a quest) is a record of its own: its links are not listed with its
/// holder's.<br/>
/// One walk at a time: the paths found are numbered (<see cref="Paths"/>) and kept from walk to walk, so a caller
/// walking many records can keep a path's number instead of its text. Not thread-safe: use one per thread.
/// </summary>
public sealed class FormLinkPlaceCollector : IContainStreamPackage
{
    private static readonly MutagenSerializationWriterKernel<FormLinkPlaceKernel, FormLinkPlaceCollector> Kernel = new();
    private static readonly StreamPackage NoStream = new(Stream.Null, null);

    private struct Frame
    {
        public int RestoreLen;
        public int NameLen;
        public int Count;
        public bool List;
        public bool Record;
    }

    private char[] _buf = new char[256];
    private int _len;
    private Frame[] _frames = new Frame[32];
    private int _depth;
    private string? _pending;
    private int _skipFrom = -1;
    private readonly List<string> _paths = new();
    private readonly Dictionary<string, int> _pathIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private readonly List<(int Path, FormKey FormKey)> _links = new();
    private readonly SerializationMetaData _metaData;

    /// <summary>
    /// True: an item's path carries its list index (<c>Keywords[3]</c>). False: <c>Keywords[*]</c>, so the number of
    /// different paths stays small over many records.
    /// </summary>
    public bool IndexedPaths { get; }

    /// <summary>True: the links of records nested in the walked one are listed too, under their path in it</summary>
    public bool IncludeNestedRecords { get; }

    /// <summary>Every path met so far, by number. It only grows, so a number stays good over many walks.</summary>
    public IReadOnlyList<string> Paths => _paths;

    /// <summary>The links of the object walked last, in the order the serializer writes them: the path's number and the link</summary>
    public IReadOnlyList<(int Path, FormKey FormKey)> Links => _links;

    /// <summary>The links of the object walked last with their paths</summary>
    public IEnumerable<FormLinkPlace> Places => _links.Select(l => new FormLinkPlace(_paths[l.Path], l.FormKey));

    StreamPackage IContainStreamPackage.StreamPackage => NoStream;

    /// <param name="release">The game of the objects walked (the serializers compare some fields with the release's defaults)</param>
    /// <param name="indexedPaths">See <see cref="IndexedPaths"/></param>
    /// <param name="includeNestedRecords">See <see cref="IncludeNestedRecords"/></param>
    public FormLinkPlaceCollector(GameRelease release, bool indexedPaths = true, bool includeNestedRecords = false)
    {
        // No round-trip notes: none is a link, and finding them costs a write of the record
        _metaData = new SerializationMetaData(release, null, null, null, CancellationToken.None) { RoundTripNotes = false };
        IndexedPaths = indexedPaths;
        IncludeNestedRecords = includeNestedRecords;
        _lookup = _pathIds.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// Walks one object (an overlay or a full object of one of the registry's classes); afterwards <see cref="Links"/>
    /// and <see cref="Places"/> hold its links
    /// </summary>
    /// <param name="registry">The generated serializers, e.g. StarfieldSerializationRegistry.Instance</param>
    /// <param name="item">The object to walk</param>
    /// <exception cref="ArgumentException">Its class is not one of the registry's</exception>
    public void Collect(ISerializationRegistry registry, object item)
    {
        _len = 0;
        _depth = 0;
        _pending = null;
        _skipFrom = -1;
        _links.Clear();
        // The walked object itself: its own FormKey is no link
        Push(new Frame { Record = item is IMajorRecordGetter });
        // Completes before it returns: this kernel writes nothing and waits on nothing
        registry.Serialize(this, item, Kernel, _metaData).GetAwaiter().GetResult();
    }

    // ---------------------------------------------------------------- The path
    private void Append(ReadOnlySpan<char> s)
    {
        if (_len + s.Length > _buf.Length) Array.Resize(ref _buf, Math.Max(_buf.Length * 2, _len + s.Length));
        s.CopyTo(_buf.AsSpan(_len));
        _len += s.Length;
    }

    private void AppendName(string name)
    {
        if (_len > 0) Append(".");
        Append(name);
    }

    private string? Take(string? fieldName)
    {
        var n = fieldName ?? _pending;
        _pending = null;
        return n;
    }

    private bool InList => _depth > 0 && _frames[_depth - 1].List;

    /// <summary>The next item of the list on top: its path is the list's with the item's place</summary>
    private void BeginItem()
    {
        ref var f = ref _frames[_depth - 1];
        _len = f.NameLen;
        if (IndexedPaths)
        {
            Span<char> digits = stackalloc char[12];
            f.Count.TryFormat(digits, out int n, default, CultureInfo.InvariantCulture);
            Append("[");
            Append(digits[..n]);
            Append("]");
        }
        else
        {
            Append("[*]");
        }
        f.Count++;
    }

    private void Push(Frame f)
    {
        if (_depth == _frames.Length) Array.Resize(ref _frames, _frames.Length * 2);
        _frames[_depth++] = f;
    }

    /// <summary>The innermost object (not list) holding what is written now</summary>
    private bool InRecord
    {
        get
        {
            for (int d = _depth - 1; d >= 0; d--)
            {
                if (!_frames[d].List) return _frames[d].Record;
            }
            return false;
        }
    }

    // ---------------------------------------------------------------- What the kernel calls
    internal void Pending(string? fieldName)
    {
        if (fieldName != null) _pending = fieldName;
    }

    internal void Value(string? fieldName)
    {
        var name = Take(fieldName);
        if (name == null && InList) BeginItem();
    }

    internal void Link(string? fieldName, FormKey? item)
    {
        var name = Take(fieldName);
        if (name == null && InList) BeginItem();
        if (name == "FormKey" && InRecord) return; // the record's own key
        if (item == null || item.Value.IsNull || _skipFrom >= 0) return;
        var keep = _len;
        if (name != null) AppendName(name);
        var path = _buf.AsSpan(0, _len);
        if (!_lookup.TryGetValue(path, out int id))
        {
            var s = new string(path);
            id = _paths.Count;
            _paths.Add(s);
            _pathIds[s] = id;
        }
        _links.Add((id, item.Value));
        _len = keep;
    }

    internal void EnterObject(string? fieldName, bool record)
    {
        var name = Take(fieldName);
        if (name == null && InList) BeginItem();
        var restore = _len;
        if (name != null) AppendName(name);
        if (record && !IncludeNestedRecords && _skipFrom < 0) _skipFrom = _depth;
        Push(new Frame { RestoreLen = restore, Record = record });
    }

    internal void LeaveObject()
    {
        ref var f = ref _frames[--_depth];
        if (_skipFrom == _depth) _skipFrom = -1;
        _len = f.RestoreLen;
        _pending = null;
    }

    internal void EnterList(string? fieldName)
    {
        var name = Take(fieldName);
        if (name == null && InList) BeginItem();
        var restore = _len;
        if (name != null) AppendName(name);
        Push(new Frame { RestoreLen = restore, NameLen = _len, List = true });
    }

    internal void LeaveList()
    {
        _len = _frames[--_depth].RestoreLen;
        _pending = null;
    }
}

/// <summary>
/// The writer kernel of <see cref="FormLinkPlaceCollector"/>: it writes nothing and tells the collector where the
/// serializer is, so a FormKey is noted with its path. Used through <see cref="FormLinkPlaceCollector.Collect"/>.
/// </summary>
public sealed class FormLinkPlaceKernel : ISerializationWriterKernel<FormLinkPlaceCollector>
{
    public string ExpectedExtension => ".links";
    /// <summary>Not used: a collector is made by its caller, for its game (<see cref="FormLinkPlaceCollector.Collect"/>)</summary>
    public FormLinkPlaceCollector GetNewObject(StreamPackage stream) =>
        throw new NotSupportedException("A FormLinkPlaceCollector is made by its caller, with its game's release");
    public void Finalize(StreamPackage stream, FormLinkPlaceCollector writer) { }
    public Exception ConvertException(Exception ex) => ex;

    public void WriteType(FormLinkPlaceCollector writer, Type type) => writer.Value("MutagenObjectType");
    public void WriteChar(FormLinkPlaceCollector writer, string? fieldName, char? item) => writer.Value(fieldName);
    public void WriteBool(FormLinkPlaceCollector writer, string? fieldName, bool? item) => writer.Value(fieldName);
    public void WriteString(FormLinkPlaceCollector writer, string? fieldName, string? item) => writer.Value(fieldName);
    public void WriteInt8(FormLinkPlaceCollector writer, string? fieldName, sbyte? item) => writer.Value(fieldName);
    public void WriteInt16(FormLinkPlaceCollector writer, string? fieldName, short? item) => writer.Value(fieldName);
    public void WriteInt32(FormLinkPlaceCollector writer, string? fieldName, int? item) => writer.Value(fieldName);
    public void WriteInt64(FormLinkPlaceCollector writer, string? fieldName, long? item) => writer.Value(fieldName);
    public void WriteUInt8(FormLinkPlaceCollector writer, string? fieldName, byte? item) => writer.Value(fieldName);
    public void WriteUInt16(FormLinkPlaceCollector writer, string? fieldName, ushort? item) => writer.Value(fieldName);
    public void WriteUInt32(FormLinkPlaceCollector writer, string? fieldName, uint? item) => writer.Value(fieldName);
    public void WriteUInt64(FormLinkPlaceCollector writer, string? fieldName, ulong? item) => writer.Value(fieldName);
    public void WriteFloat(FormLinkPlaceCollector writer, string? fieldName, float? item) => writer.Value(fieldName);
    public void WriteDouble(FormLinkPlaceCollector writer, string? fieldName, double? item) => writer.Value(fieldName);
    public void WriteHalf(FormLinkPlaceCollector writer, string? fieldName, Half? item) => writer.Value(fieldName);
    public void WriteModKey(FormLinkPlaceCollector writer, string? fieldName, ModKey? item) => writer.Value(fieldName);
    public void WriteFormKey(FormLinkPlaceCollector writer, string? fieldName, FormKey? item) => writer.Link(fieldName, item);
    public void WriteRecordType(FormLinkPlaceCollector writer, string? fieldName, RecordType? item) => writer.Value(fieldName);
    public void WriteP2Int(FormLinkPlaceCollector writer, string? fieldName, P2Int? item) => writer.Value(fieldName);
    public void WriteP2Int16(FormLinkPlaceCollector writer, string? fieldName, P2Int16? item) => writer.Value(fieldName);
    public void WriteP2UInt8(FormLinkPlaceCollector writer, string? fieldName, P2UInt8? item) => writer.Value(fieldName);
    public void WriteP2Float(FormLinkPlaceCollector writer, string? fieldName, P2Float? item) => writer.Value(fieldName);
    public void WriteP3Float(FormLinkPlaceCollector writer, string? fieldName, P3Float? item) => writer.Value(fieldName);
    public void WriteP3UInt8(FormLinkPlaceCollector writer, string? fieldName, P3UInt8? item) => writer.Value(fieldName);
    public void WriteP3Int16(FormLinkPlaceCollector writer, string? fieldName, P3Int16? item) => writer.Value(fieldName);
    public void WriteP3UInt16(FormLinkPlaceCollector writer, string? fieldName, P3UInt16? item) => writer.Value(fieldName);
    public void WritePercent(FormLinkPlaceCollector writer, string? fieldName, Percent? item) => writer.Value(fieldName);
    public void WriteColor(FormLinkPlaceCollector writer, string? fieldName, Color? item) => writer.Value(fieldName);
    public void WriteTimeOnly(FormLinkPlaceCollector writer, string? fieldName, TimeOnly? item) => writer.Value(fieldName);
    public void WriteDateOnly(FormLinkPlaceCollector writer, string? fieldName, DateOnly? item) => writer.Value(fieldName);
    public void WriteGuid(FormLinkPlaceCollector writer, string? fieldName, Guid? item) => writer.Value(fieldName);
    public void WriteTranslatedString(FormLinkPlaceCollector writer, string? fieldName, ITranslatedStringGetter? item) => writer.Value(fieldName);
    public void WriteBytes(FormLinkPlaceCollector writer, string? fieldName, ReadOnlyMemorySlice<byte>? item) => writer.Value(fieldName);

    public void WriteEnum<TEnum>(FormLinkPlaceCollector writer, string? fieldName, TEnum? item)
        where TEnum : struct, Enum, IConvertible
        => writer.Value(fieldName);

    private static void Wait(Task t)
    {
        if (!t.IsCompletedSuccessfully) t.GetAwaiter().GetResult();
    }

    public void WriteWithName<TKernel, TObject>(
        MutagenSerializationWriterKernel<TKernel, FormLinkPlaceCollector> kernel,
        FormLinkPlaceCollector writer,
        string? fieldName,
        TObject item,
        SerializationMetaData serializationMetaData,
        WriteAsync<TKernel, FormLinkPlaceCollector, TObject> writeCall)
        where TKernel : ISerializationWriterKernel<FormLinkPlaceCollector>, new()
    {
        writer.Pending(fieldName);
        Wait(writeCall(writer, item, kernel, serializationMetaData));
    }

    public void WriteLoqui<TKernel, TObject>(
        MutagenSerializationWriterKernel<TKernel, FormLinkPlaceCollector> kernel,
        FormLinkPlaceCollector writer,
        string? fieldName,
        TObject item,
        SerializationMetaData serializationMetaData,
        Write<TKernel, FormLinkPlaceCollector, TObject> writeCall)
        where TKernel : ISerializationWriterKernel<FormLinkPlaceCollector>, new()
    {
        writer.EnterObject(fieldName, item is IMajorRecordGetter);
        writeCall(writer, item, kernel, serializationMetaData);
        writer.LeaveObject();
    }

    public Task WriteLoqui<TKernel, TObject>(
        MutagenSerializationWriterKernel<TKernel, FormLinkPlaceCollector> kernel,
        FormLinkPlaceCollector writer,
        string? fieldName,
        TObject item,
        SerializationMetaData serializationMetaData,
        WriteAsync<TKernel, FormLinkPlaceCollector, TObject> writeCall)
        where TKernel : ISerializationWriterKernel<FormLinkPlaceCollector>, new()
    {
        writer.EnterObject(fieldName, item is IMajorRecordGetter);
        Wait(writeCall(writer, item, kernel, serializationMetaData));
        writer.LeaveObject();
        return Task.CompletedTask;
    }

    public void StartListSection(FormLinkPlaceCollector writer, string? fieldName) => writer.EnterList(fieldName);
    public void EndListSection(FormLinkPlaceCollector writer) => writer.LeaveList();

    public void StartDictionarySection(FormLinkPlaceCollector writer, string? fieldName) => writer.EnterList(fieldName);
    public void EndDictionarySection(FormLinkPlaceCollector writer) => writer.LeaveList();
    public void StartDictionaryItem(FormLinkPlaceCollector writer) => writer.EnterObject(null, record: false);
    public void EndDictionaryItem(FormLinkPlaceCollector writer) => writer.LeaveObject();
    public void StartDictionaryKey(FormLinkPlaceCollector writer) => writer.Pending("Key");
    public void EndDictionaryKey(FormLinkPlaceCollector writer) { }
    public void StartDictionaryValue(FormLinkPlaceCollector writer) => writer.Pending("Value");
    public void EndDictionaryValue(FormLinkPlaceCollector writer) { }

    public void StartArray2dSection(FormLinkPlaceCollector writer, string? fieldName) => writer.EnterList(fieldName);
    public void EndArray2dSection(FormLinkPlaceCollector writer) => writer.LeaveList();
    public void StartArray2dXItem(FormLinkPlaceCollector writer) { }
    public void EndArray2dXItem(FormLinkPlaceCollector writer) { }
    public void StartArray2dYSection(FormLinkPlaceCollector writer) => writer.EnterList(null);
    public void EndArray2dYSection(FormLinkPlaceCollector writer) => writer.LeaveList();
}

/// <summary>Each link of an object with the field it sits in (C59, W12)</summary>
public static class FormLinkPlaceMixIn
{
    /// <summary>
    /// The links of an object (an overlay or a full object of one of the registry's classes) with their paths in its
    /// JSON, as <see cref="FormLinkPlaceCollector"/> finds them. For many objects keep one collector and call
    /// <see cref="FormLinkPlaceCollector.Collect"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Its class is not one of the registry's</exception>
    public static IReadOnlyList<FormLinkPlace> EnumerateFormLinkPlaces(
        this ISerializationRegistry registry,
        object item,
        GameRelease release,
        bool indexedPaths = true,
        bool includeNestedRecords = false)
    {
        var collector = new FormLinkPlaceCollector(release, indexedPaths, includeNestedRecords);
        collector.Collect(registry, item);
        return collector.Places.ToArray();
    }
}
