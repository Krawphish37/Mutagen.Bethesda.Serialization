using System.Drawing;
using System.Globalization;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Testing;
using Mutagen.Bethesda.Strings;
using Newtonsoft.Json.Linq;
using Noggog;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.Tests.C59;

// C59 W12: each link of an object with the field it sits in. SFSearch (SFSearch\Engine\LinkWalker.cs,
// agent-first-integration 7fc0119) walked every record with its own writer kernel through the JSON serializer for
// this; the library's FormLinkPlaceCollector does it now. These tests check the collector against a copy of SFSearch's
// walk (same paths, same links, same order), against the record's JSON (each path leads to its FormKey there, and
// every FormKey there is found) and against Mutagen's EnumerateFormLinks.
public class FormLinkPlaceTests
{
    private static readonly ISerializationRegistry Registry = Fallout4SerializationRegistry.Instance;

    // ------------------------------------------------------------------ SFSearch's walk, as SFSearch has it (WantName left out)
    private sealed class SfSearchLinkSink : IContainStreamPackage
    {
        private static readonly StreamPackage NoStream = new(Stream.Null, null);
        public StreamPackage StreamPackage => NoStream;

        private struct Frame { public int RestoreLen, NameLen, Count, LinksAt; public bool List, Child; }

        private char[] _buf = new char[256];
        private int _len;
        private Frame[] _frames = new Frame[32];
        private int _depth;
        private string? _pending;
        private readonly Dictionary<string, int> _fieldIds = new(StringComparer.Ordinal);

        public bool Indexed { get; }
        public List<string> Fields { get; } = new();
        public List<(int Field, FormKey Target)> Links { get; } = new();

        public SfSearchLinkSink(bool indexed) { Indexed = indexed; }

        public void Reset() { _len = 0; _depth = 0; _pending = null; Links.Clear(); }

        private void Append(ReadOnlySpan<char> s)
        {
            if (_len + s.Length > _buf.Length) Array.Resize(ref _buf, Math.Max(_buf.Length * 2, _len + s.Length));
            s.CopyTo(_buf.AsSpan(_len));
            _len += s.Length;
        }

        private void AppendName(string name) { if (_len > 0) Append("."); Append(name); }
        private string? Take(string? fieldName) { string? n = fieldName ?? _pending; _pending = null; return n; }
        private bool InList => _depth > 0 && _frames[_depth - 1].List;

        private void BeginItem()
        {
            ref var f = ref _frames[_depth - 1];
            _len = f.NameLen;
            if (Indexed)
            {
                Span<char> digits = stackalloc char[12];
                f.Count.TryFormat(digits, out int n, default, CultureInfo.InvariantCulture);
                Append("["); Append(digits[..n]); Append("]");
            }
            else Append("[*]");
            f.Count++;
        }

        private void Push(Frame f) { if (_depth == _frames.Length) Array.Resize(ref _frames, _frames.Length * 2); _frames[_depth++] = f; }

        public void Pending(string? fieldName) { if (fieldName != null) _pending = fieldName; }

        public string? Value(string? fieldName) { string? name = Take(fieldName); if (name == null && InList) BeginItem(); return name; }

        public void Link(string? fieldName, FormKey? item)
        {
            string? name = Take(fieldName);
            if (name == null && InList) BeginItem();
            if (name == "FormKey")
            {
                for (int d = _depth - 1; d >= 0; d--)
                    if (!_frames[d].List) { _frames[d].Child = true; break; }
                return;
            }
            if (item == null || item.Value.IsNull) return;
            int keep = _len;
            if (name != null) AppendName(name);
            var s = new string(_buf.AsSpan(0, _len));
            if (!_fieldIds.TryGetValue(s, out int id)) { id = Fields.Count; Fields.Add(s); _fieldIds[s] = id; }
            Links.Add((id, item.Value));
            _len = keep;
        }

        public void EnterObject(string? fieldName)
        {
            string? name = Take(fieldName);
            if (name == null && InList) BeginItem();
            int restore = _len;
            if (name != null) AppendName(name);
            Push(new Frame { RestoreLen = restore, LinksAt = Links.Count });
        }

        public void LeaveObject()
        {
            ref var f = ref _frames[--_depth];
            if (f.Child && Links.Count > f.LinksAt) Links.RemoveRange(f.LinksAt, Links.Count - f.LinksAt);
            _len = f.RestoreLen;
            _pending = null;
        }

        public void EnterList(string? fieldName)
        {
            string? name = Take(fieldName);
            if (name == null && InList) BeginItem();
            int restore = _len;
            if (name != null) AppendName(name);
            Push(new Frame { RestoreLen = restore, NameLen = _len, List = true });
        }

        public void LeaveList() { _len = _frames[--_depth].RestoreLen; _pending = null; }
    }

    private sealed class SfSearchLinkKernel : ISerializationWriterKernel<SfSearchLinkSink>
    {
        public string ExpectedExtension => ".links";
        public SfSearchLinkSink GetNewObject(StreamPackage stream) => new(false);
        public void Finalize(StreamPackage stream, SfSearchLinkSink writer) { }
        public Exception ConvertException(Exception ex) => ex;
        public void WriteType(SfSearchLinkSink writer, Type type) => writer.Value("MutagenObjectType");
        public void WriteChar(SfSearchLinkSink writer, string? fieldName, char? item) => writer.Value(fieldName);
        public void WriteBool(SfSearchLinkSink writer, string? fieldName, bool? item) => writer.Value(fieldName);
        public void WriteString(SfSearchLinkSink writer, string? fieldName, string? item) => writer.Value(fieldName);
        public void WriteInt8(SfSearchLinkSink writer, string? fieldName, sbyte? item) => writer.Value(fieldName);
        public void WriteInt16(SfSearchLinkSink writer, string? fieldName, short? item) => writer.Value(fieldName);
        public void WriteInt32(SfSearchLinkSink writer, string? fieldName, int? item) => writer.Value(fieldName);
        public void WriteInt64(SfSearchLinkSink writer, string? fieldName, long? item) => writer.Value(fieldName);
        public void WriteUInt8(SfSearchLinkSink writer, string? fieldName, byte? item) => writer.Value(fieldName);
        public void WriteUInt16(SfSearchLinkSink writer, string? fieldName, ushort? item) => writer.Value(fieldName);
        public void WriteUInt32(SfSearchLinkSink writer, string? fieldName, uint? item) => writer.Value(fieldName);
        public void WriteUInt64(SfSearchLinkSink writer, string? fieldName, ulong? item) => writer.Value(fieldName);
        public void WriteFloat(SfSearchLinkSink writer, string? fieldName, float? item) => writer.Value(fieldName);
        public void WriteDouble(SfSearchLinkSink writer, string? fieldName, double? item) => writer.Value(fieldName);
        public void WriteHalf(SfSearchLinkSink writer, string? fieldName, Half? item) => writer.Value(fieldName);
        public void WriteModKey(SfSearchLinkSink writer, string? fieldName, ModKey? item) => writer.Value(fieldName);
        public void WriteFormKey(SfSearchLinkSink writer, string? fieldName, FormKey? item) => writer.Link(fieldName, item);
        public void WriteRecordType(SfSearchLinkSink writer, string? fieldName, RecordType? item) => writer.Value(fieldName);
        public void WriteP2Int(SfSearchLinkSink writer, string? fieldName, P2Int? item) => writer.Value(fieldName);
        public void WriteP2Int16(SfSearchLinkSink writer, string? fieldName, P2Int16? item) => writer.Value(fieldName);
        public void WriteP2UInt8(SfSearchLinkSink writer, string? fieldName, P2UInt8? item) => writer.Value(fieldName);
        public void WriteP2Float(SfSearchLinkSink writer, string? fieldName, P2Float? item) => writer.Value(fieldName);
        public void WriteP3Float(SfSearchLinkSink writer, string? fieldName, P3Float? item) => writer.Value(fieldName);
        public void WriteP3UInt8(SfSearchLinkSink writer, string? fieldName, P3UInt8? item) => writer.Value(fieldName);
        public void WriteP3Int16(SfSearchLinkSink writer, string? fieldName, P3Int16? item) => writer.Value(fieldName);
        public void WriteP3UInt16(SfSearchLinkSink writer, string? fieldName, P3UInt16? item) => writer.Value(fieldName);
        public void WritePercent(SfSearchLinkSink writer, string? fieldName, Percent? item) => writer.Value(fieldName);
        public void WriteColor(SfSearchLinkSink writer, string? fieldName, Color? item) => writer.Value(fieldName);
        public void WriteTimeOnly(SfSearchLinkSink writer, string? fieldName, TimeOnly? item) => writer.Value(fieldName);
        public void WriteDateOnly(SfSearchLinkSink writer, string? fieldName, DateOnly? item) => writer.Value(fieldName);
        public void WriteGuid(SfSearchLinkSink writer, string? fieldName, Guid? item) => writer.Value(fieldName);
        public void WriteBytes(SfSearchLinkSink writer, string? fieldName, ReadOnlyMemorySlice<byte>? item) => writer.Value(fieldName);
        public void WriteEnum<TEnum>(SfSearchLinkSink writer, string? fieldName, TEnum? item) where TEnum : struct, Enum, IConvertible => writer.Value(fieldName);
        public void WriteTranslatedString(SfSearchLinkSink writer, string? fieldName, ITranslatedStringGetter? item) => writer.Value(fieldName);

        private static void Wait(Task t) { if (!t.IsCompletedSuccessfully) t.GetAwaiter().GetResult(); }

        public void WriteWithName<TKernel, TObject>(MutagenSerializationWriterKernel<TKernel, SfSearchLinkSink> kernel, SfSearchLinkSink writer, string? fieldName, TObject item,
            SerializationMetaData serializationMetaData, WriteAsync<TKernel, SfSearchLinkSink, TObject> writeCall) where TKernel : ISerializationWriterKernel<SfSearchLinkSink>, new()
        {
            writer.Pending(fieldName);
            Wait(writeCall(writer, item, kernel, serializationMetaData));
        }

        public void WriteLoqui<TKernel, TObject>(MutagenSerializationWriterKernel<TKernel, SfSearchLinkSink> kernel, SfSearchLinkSink writer, string? fieldName, TObject item,
            SerializationMetaData serializationMetaData, Write<TKernel, SfSearchLinkSink, TObject> writeCall) where TKernel : ISerializationWriterKernel<SfSearchLinkSink>, new()
        {
            writer.EnterObject(fieldName);
            writeCall(writer, item, kernel, serializationMetaData);
            writer.LeaveObject();
        }

        public Task WriteLoqui<TKernel, TObject>(MutagenSerializationWriterKernel<TKernel, SfSearchLinkSink> kernel, SfSearchLinkSink writer, string? fieldName, TObject item,
            SerializationMetaData serializationMetaData, WriteAsync<TKernel, SfSearchLinkSink, TObject> writeCall) where TKernel : ISerializationWriterKernel<SfSearchLinkSink>, new()
        {
            writer.EnterObject(fieldName);
            Wait(writeCall(writer, item, kernel, serializationMetaData));
            writer.LeaveObject();
            return Task.CompletedTask;
        }

        public void StartListSection(SfSearchLinkSink writer, string? fieldName) => writer.EnterList(fieldName);
        public void EndListSection(SfSearchLinkSink writer) => writer.LeaveList();
        public void StartDictionarySection(SfSearchLinkSink writer, string? fieldName) => writer.EnterList(fieldName);
        public void EndDictionarySection(SfSearchLinkSink writer) => writer.LeaveList();
        public void StartDictionaryItem(SfSearchLinkSink writer) => writer.EnterObject(null);
        public void EndDictionaryItem(SfSearchLinkSink writer) => writer.LeaveObject();
        public void StartDictionaryKey(SfSearchLinkSink writer) => writer.Pending("Key");
        public void EndDictionaryKey(SfSearchLinkSink writer) { }
        public void StartDictionaryValue(SfSearchLinkSink writer) => writer.Pending("Value");
        public void EndDictionaryValue(SfSearchLinkSink writer) { }
        public void StartArray2dSection(SfSearchLinkSink writer, string? fieldName) => writer.EnterList(fieldName);
        public void EndArray2dSection(SfSearchLinkSink writer) => writer.LeaveList();
        public void StartArray2dXItem(SfSearchLinkSink writer) { }
        public void EndArray2dXItem(SfSearchLinkSink writer) { }
        public void StartArray2dYSection(SfSearchLinkSink writer) => writer.EnterList(null);
        public void EndArray2dYSection(SfSearchLinkSink writer) => writer.LeaveList();
    }

    private static readonly MutagenSerializationWriterKernel<SfSearchLinkKernel, SfSearchLinkSink> SfSearchKernel = new();
    private static readonly SerializationMetaData NoNotes = new(GameRelease.Fallout4, null, null, null, CancellationToken.None) { RoundTripNotes = false };

    // SFSearch's RecordLinks.Walk, with the registry in place of its reflection
    private static List<(string Path, FormKey FormKey)> SfSearchWalk(object record, bool indexed)
    {
        var sink = new SfSearchLinkSink(indexed);
        sink.Reset();
        Registry.Serialize(sink, record, SfSearchKernel, NoNotes).GetAwaiter().GetResult();
        return sink.Links.Select(l => (sink.Fields[l.Field], l.Target)).ToList();
    }

    // ------------------------------------------------------------------ Test records
    private static Cell CellWithNested(Fallout4Mod mod, out PlacedObject placed)
    {
        var cell = new Cell(mod.GetNextFormKey(), Fallout4Release.Fallout4)
        {
            EditorID = "C59Cell",
            Location = new FormLinkNullable<ILocationGetter>(FormKey.Factory("000123:Fallout4.esm")),
        };
        placed = new PlacedObject(mod.GetNextFormKey(), Fallout4Release.Fallout4)
        {
            Base = new FormLinkNullable<IPlaceableObjectGetter>(FormKey.Factory("000456:Fallout4.esm")),
        };
        cell.Temporary.Add(placed);
        return cell;
    }

    private static IEnumerable<IMajorRecordGetter> Records(params IMajorRecordGetter[] filled)
    {
        var mod = new Fallout4Mod(ModKey.FromFileName("C59Links.esp"), Fallout4Release.Fallout4);
        return filled.Append(CellWithNested(mod, out _)).Concat(RegistryTests.DefaultRecords());
    }

    // Every FormKey written in the JSON, where it is (own FormKeys and nulls left out by the caller)
    private static IEnumerable<(string Path, FormKey FormKey)> FormKeysIn(JContainer token)
    {
        foreach (var value in token.DescendantsAndSelf().OfType<JValue>())
        {
            if (value.Type != JTokenType.String) continue;
            if (FormKey.TryFactory((string)value!, out var key) && !key.IsNull) yield return (value.Path, key);
        }
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void SameAsSfSearchsWalk(Ammunition ammo, ArmorAddon armorAddon, Faction faction, Keyword keyword)
    {
        foreach (var indexed in new[] { true, false })
        {
            var collector = new FormLinkPlaceCollector(GameRelease.Fallout4, indexedPaths: indexed);
            foreach (var record in Records(ammo, armorAddon, faction, keyword))
            {
                collector.Collect(Registry, record);
                collector.Places.Select(p => (p.Path, p.FormKey)).ToList()
                    .ShouldBe(SfSearchWalk(record, indexed), $"{record.GetType().Name} indexed {indexed}");
            }
        }
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void EachPathLeadsToItsFormKeyInTheJson_AndEveryFormKeyThereIsFound(Ammunition ammo, ArmorAddon armorAddon, Faction faction, Keyword keyword)
    {
        foreach (var record in new IMajorRecordGetter[] { ammo, armorAddon, faction, keyword })
        {
            var json = JObject.Parse(RegistryTests.ToJson(record));
            var places = Registry.EnumerateFormLinkPlaces(record, GameRelease.Fallout4);
            places.Count.ShouldBeGreaterThan(0, record.GetType().Name);
            foreach (var place in places)
            {
                ((string?)json.SelectToken(place.Path)).ShouldBe(place.FormKey.ToString(), place.Path);
            }
            var inJson = FormKeysIn(json)
                .Where(k => !(k.Path == "FormKey" && k.FormKey == record.FormKey))
                .ToList();
            places.Select(p => (p.Path, p.FormKey)).ShouldBe(inJson, ignoreOrder: true, record.GetType().Name);
        }
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void SameLinksAsMutagensEnumerateFormLinks(Ammunition ammo, ArmorAddon armorAddon, Faction faction, Keyword keyword)
    {
        // Copies: AutoFixture's links in a list give a new FormKey each time they are read; a copy holds plain links
        foreach (var record in new IMajorRecordGetter[] { ammo.DeepCopy(), armorAddon.DeepCopy(), faction.DeepCopy(), keyword.DeepCopy() })
        {
            var placeList = Registry.EnumerateFormLinkPlaces(record, GameRelease.Fallout4);
            var mutagenLinks = record.EnumerateFormLinks().Where(l => !l.IsNull).ToList();
            var onlyPlaces = placeList.Where(p => !mutagenLinks.Any(l => l.FormKey == p.FormKey)).Select(p => p.Path);
            var onlyMutagen = mutagenLinks.Where(l => !placeList.Any(p => p.FormKey == l.FormKey)).Select(l => $"{l.Type.Name} {l.FormKey}");
            placeList.Select(p => p.FormKey).OrderBy(k => k.ToString()).ShouldBe(
                mutagenLinks.Select(l => l.FormKey).OrderBy(k => k.ToString()),
                $"{record.GetType().Name}: only in the places {string.Join(", ", onlyPlaces)}; only in EnumerateFormLinks {string.Join(", ", onlyMutagen)}");
        }
    }

    [Fact]
    public void NestedRecordsAreTheirOwn()
    {
        var mod = new Fallout4Mod(ModKey.FromFileName("C59Links.esp"), Fallout4Release.Fallout4);
        var cell = CellWithNested(mod, out var placed);
        var own = Registry.EnumerateFormLinkPlaces(cell, GameRelease.Fallout4);
        own.ShouldBe(new[] { new FormLinkPlace("Location", cell.Location.FormKey) });

        var all = Registry.EnumerateFormLinkPlaces(cell, GameRelease.Fallout4, includeNestedRecords: true);
        all.ShouldBe(new[]
        {
            new FormLinkPlace("Location", cell.Location.FormKey),
            new FormLinkPlace("Temporary[0].Base", placed.Base.FormKey),
        }, ignoreOrder: true);
        Registry.EnumerateFormLinkPlaces(cell, GameRelease.Fallout4, indexedPaths: false, includeNestedRecords: true)
            .Select(p => p.Path).ShouldBe(new[] { "Location", "Temporary[*].Base" }, ignoreOrder: true);

        // The nested record walked alone has its link, and no record's own FormKey is a link
        Registry.EnumerateFormLinkPlaces(placed, GameRelease.Fallout4).ShouldBe(new[] { new FormLinkPlace("Base", placed.Base.FormKey) });
        all.Select(p => p.FormKey).ShouldNotContain(placed.FormKey);
        all.Select(p => p.FormKey).ShouldNotContain(cell.FormKey);
    }

    [Theory, TestAutoData(ConfigureMembers: true)]
    public void PathNumbersHoldOverWalks(Ammunition ammo, Faction faction)
    {
        var collector = new FormLinkPlaceCollector(GameRelease.Fallout4, indexedPaths: false);
        collector.Collect(Registry, ammo);
        var first = collector.Paths.ToArray();
        var ammoLinks = collector.Places.ToArray();
        collector.Collect(Registry, faction);
        collector.Paths.Take(first.Length).ShouldBe(first);
        collector.Collect(Registry, ammo);
        collector.Places.ShouldBe(ammoLinks);
        collector.Paths.Count.ShouldBe(collector.Paths.Distinct().Count());
    }
}
