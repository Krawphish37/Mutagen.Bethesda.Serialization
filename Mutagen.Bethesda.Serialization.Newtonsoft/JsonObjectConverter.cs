using System.Runtime.InteropServices;
using System.Text;
using Mutagen.Bethesda.Serialization.Streams;

namespace Mutagen.Bethesda.Serialization.Newtonsoft;

/// <summary>
/// Any object of a registry's classes (an <see cref="ISerializationRegistry"/> the source generator wrote, e.g.
/// <c>StarfieldSerializationRegistry.Instance</c>) to JSON and back by its runtime type, with synchronous calls, writing
/// into one buffer it keeps from one object to the next (C59, W11 and V4).<br/>
/// The JSON of an object is what its class's generated serializer writes between "{" and "}", indented: byte for byte
/// what <see cref="NewtonsoftJsonSerializationWriterKernel"/>'s GetNewObject and Finalize give around the same serializer.<br/>
/// Not thread-safe: use one per thread.
/// </summary>
public sealed class JsonObjectConverter
{
    private static readonly NewtonsoftJsonSerializationReaderKernel ReaderKernel = new();

    private readonly MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit> _writerKernel;
    private readonly int _keepBufferBytes;
    private MemoryStream _buffer;
    private JsonWritingUnit _unit;
    private bool _writing;

    /// <summary>The serializers it uses</summary>
    public ISerializationRegistry Registry { get; }

    /// <summary>What it gives the serializers (release, round-trip notes)</summary>
    public SerializationMetaData MetaData { get; }

    /// <param name="registry">The serializers to use</param>
    /// <param name="metaData">Given to the serializers; for reading, its release must be the records' game</param>
    /// <param name="writerKernel">The writer kernel: the shared one when null, or e.g. one that writes defaults</param>
    /// <param name="keepBufferBytes">A buffer one large object made larger than this is not kept for the next one</param>
    public JsonObjectConverter(
        ISerializationRegistry registry,
        SerializationMetaData metaData,
        MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>? writerKernel = null,
        int keepBufferBytes = 1 << 20)
    {
        Registry = registry;
        MetaData = metaData;
        _writerKernel = writerKernel ?? MutagenSerializationWriterKernel<NewtonsoftJsonSerializationWriterKernel, JsonWritingUnit>.Instance;
        _keepBufferBytes = keepBufferBytes;
        _buffer = new MemoryStream(4096);
        _unit = new JsonWritingUnit(new StreamPackage(_buffer, null));
    }

    /// <summary>
    /// The object's JSON as UTF-8. The bytes are this converter's buffer: use or copy them before its next call.
    /// </summary>
    /// <exception cref="ArgumentException">Its class is not one of the registry's</exception>
    public ReadOnlyMemory<byte> ToUtf8(object item)
    {
        if (_writing)
        {
            // A call made while this converter writes (from inside a serializer) gets a writer of its own
            using var mem = new MemoryStream();
            using var unit = new JsonWritingUnit(new StreamPackage(mem, null));
            Write(unit, item);
            return mem.ToArray();
        }
        if (_buffer.Capacity > _keepBufferBytes)
        {
            _buffer = new MemoryStream(4096);
            _unit = new JsonWritingUnit(new StreamPackage(_buffer, null));
        }
        _writing = true;
        try
        {
            _unit.Reset();
            Write(_unit, item);
        }
        finally
        {
            _writing = false;
        }
        return new ReadOnlyMemory<byte>(_buffer.GetBuffer(), 0, (int)_buffer.Length);
    }

    /// <summary>The object's JSON</summary>
    /// <exception cref="ArgumentException">Its class is not one of the registry's</exception>
    public string ToJson(object item) => Encoding.UTF8.GetString(ToUtf8(item).Span);

    private void Write(JsonWritingUnit unit, object item)
    {
        unit.Writer.WriteStartObject();
        // Completes before it returns for one object with the inline work dropoff; waited on otherwise
        Registry.Serialize(unit, item, _writerKernel, MetaData).GetAwaiter().GetResult();
        unit.Writer.WriteEndObject();
        unit.Writer.Flush();
    }

    /// <summary>A new object of the class <paramref name="type"/> names (the class or its getter or setter interface)</summary>
    /// <exception cref="ArgumentException">The type is not one of the registry's classes or their interfaces</exception>
    public object FromUtf8(ReadOnlyMemory<byte> json, Type type)
    {
        using var stream = MemoryMarshal.TryGetArray(json, out var segment) && segment.Array != null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(json.ToArray(), writable: false);
        using var reader = ReaderKernel.GetNewObject(new StreamPackage(stream, null));
        return Registry.Deserialize(reader, type, ReaderKernel, MetaData).GetAwaiter().GetResult();
    }

    /// <summary>A new object of the class <paramref name="type"/> names (the class or its getter or setter interface)</summary>
    /// <exception cref="ArgumentException">The type is not one of the registry's classes or their interfaces</exception>
    public object FromJson(string json, Type type) => FromUtf8(Encoding.UTF8.GetBytes(json), type);

    /// <summary>A new object of class <typeparamref name="T"/>, or of the class whose getter or setter interface it is</summary>
    /// <exception cref="ArgumentException">The type is not one of the registry's classes or their interfaces</exception>
    public T FromJson<T>(string json)
        where T : class
        => (T)FromJson(json, typeof(T));
}
