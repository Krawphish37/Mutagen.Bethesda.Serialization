using System.Drawing;
using System.Globalization;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Utility;
using Mutagen.Bethesda.Strings;
using Newtonsoft.Json;
using Noggog;

namespace Mutagen.Bethesda.Serialization.Newtonsoft;

public class JsonWritingUnit : IDisposable, IContainStreamPackage
{
    private readonly StreamWriter _streamWriter;

    /// <summary>The JSON writer; a new one after each <see cref="Reset"/></summary>
    public JsonTextWriter Writer { get; private set; }

    public StreamPackage StreamPackage { get; }

    public JsonWritingUnit(
        StreamPackage stream)
    {
        StreamPackage = stream;
        _streamWriter = new StreamWriter(stream.Stream, leaveOpen: true);
        Writer = NewWriter(_streamWriter);
    }

    private static JsonTextWriter NewWriter(TextWriter textWriter) =>
        new(textWriter)
        {
            Formatting = Formatting.Indented,
            Culture = TargetCulture.English
        };

    /// <summary>
    /// Starts the unit over, keeping its buffers (C59, V4): what was written is dropped, the stream is emptied (it must
    /// be seekable, e.g. a MemoryStream) and a new JSON writer starts on the same text writer. A unit used for object
    /// after object this way writes each as a new unit would, without a new StreamWriter and its buffers for each. It
    /// also clears a unit an error left in the middle of an object.
    /// </summary>
    public void Reset()
    {
        Writer.Flush();
        StreamPackage.Stream.SetLength(0);
        StreamPackage.Stream.Position = 0;
        Writer = NewWriter(_streamWriter);
    }

    public void Dispose()
    {
        IDisposable disp = Writer;
        disp.Dispose();
    }

    public void WriteName(string? fieldName)
    {
        if (fieldName == null)
        {
            return;
        }
        Writer.WritePropertyName(fieldName);
    }
}

public class NewtonsoftJsonSerializationWriterKernel : ISerializationWriterKernel<JsonWritingUnit>
{
    public string ExpectedExtension => ".json";
    
    public JsonWritingUnit GetNewObject(StreamPackage stream)
    {
        var ret = new JsonWritingUnit(stream);
        ret.Writer.WriteStartObject();
        return ret;
    }

    public void Finalize(StreamPackage stream, JsonWritingUnit writer)
    {
        writer.Writer.WriteEndObject();
        writer.Dispose();
    }

    public void WriteType(JsonWritingUnit writer, Type type)
    {
        var classType = TypeHelper.GetTypeToSerialize(type);
        WriteString(writer, "MutagenObjectType", classType.GetNameWithDeclaringType());
    }

    public void WriteChar(JsonWritingUnit writer, string? fieldName, char? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteBool(JsonWritingUnit writer, string? fieldName, bool? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteString(JsonWritingUnit writer, string? fieldName, string? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteStartObject();
            writer.Writer.WriteEndObject();
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteInt8(JsonWritingUnit writer, string? fieldName, sbyte? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteInt16(JsonWritingUnit writer, string? fieldName, short? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteInt32(JsonWritingUnit writer, string? fieldName, int? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteInt64(JsonWritingUnit writer, string? fieldName, long? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteUInt8(JsonWritingUnit writer, string? fieldName, byte? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteUInt16(JsonWritingUnit writer, string? fieldName, ushort? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteUInt32(JsonWritingUnit writer, string? fieldName, uint? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteUInt64(JsonWritingUnit writer, string? fieldName, ulong? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item);
        }
    }

    public void WriteFloat(JsonWritingUnit writer, string? fieldName, float? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            // Its text from JsonScalarText, the public form of what is written here
            writer.Writer.WriteRawValue(JsonScalarText.Format(item.Value));
        }
    }

    public void WriteHalf(JsonWritingUnit writer, string? fieldName, Half? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            // The double holding the same number: written exactly, read back exactly (C222); JsonScalarText.Format
            writer.Writer.WriteRawValue(JsonScalarText.Format(item.Value));
        }
    }

    public void WriteDouble(JsonWritingUnit writer, string? fieldName, double? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            // Its text from JsonScalarText, the public form of what is written here
            writer.Writer.WriteRawValue(JsonScalarText.Format(item.Value));
        }
    }

    public void WriteModKey(JsonWritingUnit writer, string? fieldName, ModKey? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item.Value.FileName);
        }
    }

    public void WriteFormKey(JsonWritingUnit writer, string? fieldName, FormKey? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item.Value.ToString());
        }
    }

    public void WriteRecordType(JsonWritingUnit writer, string? fieldName, RecordType? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else if (item.Value.TypeInt == RecordType.Null.TypeInt)
        {
            writer.Writer.WriteValue("null");
        }
        else
        {
            writer.Writer.WriteValue(item.Value.Type);
        }
    }

    public void WriteP2Int(JsonWritingUnit writer, string? fieldName, P2Int? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue($"{item.Value.X.ToString(TargetCulture.English)}, {item.Value.Y.ToString(TargetCulture.English)}");
        }
    }

    public void WriteP2Int16(JsonWritingUnit writer, string? fieldName, P2Int16? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue($"{item.Value.X.ToString(TargetCulture.English)}, {item.Value.Y.ToString(TargetCulture.English)}");
        }
    }

    public void WriteP2UInt8(JsonWritingUnit writer, string? fieldName, P2UInt8? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue($"{item.Value.X.ToString(TargetCulture.English)}, {item.Value.Y.ToString(TargetCulture.English)}");
        }
    }

    public void WriteP2Float(JsonWritingUnit writer, string? fieldName, P2Float? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(FloatSerialization.ToString(item.Value, TargetCulture.English));
        }
    }

    public void WriteP3Float(JsonWritingUnit writer, string? fieldName, P3Float? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(FloatSerialization.ToString(item.Value, TargetCulture.English));
        }
    }

    public void WriteP3UInt8(JsonWritingUnit writer, string? fieldName, P3UInt8? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue($"{item.Value.X.ToString(TargetCulture.English)}, {item.Value.Y.ToString(TargetCulture.English)}, {item.Value.Z.ToString(TargetCulture.English)}");
        }
    }

    public void WriteP3Int16(JsonWritingUnit writer, string? fieldName, P3Int16? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue($"{item.Value.X.ToString(TargetCulture.English)}, {item.Value.Y.ToString(TargetCulture.English)}, {item.Value.Z.ToString(TargetCulture.English)}");
        }
    }

    public void WriteP3UInt16(JsonWritingUnit writer, string? fieldName, P3UInt16? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue($"{item.Value.X.ToString(TargetCulture.English)}, {item.Value.Y.ToString(TargetCulture.English)}, {item.Value.Z.ToString(TargetCulture.English)}");
        }
    }

    public void WritePercent(JsonWritingUnit writer, string? fieldName, Percent? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item.Value);
        }
    }

    public void WriteColor(JsonWritingUnit writer, string? fieldName, Color? item)
    {
        writer.WriteName(fieldName);
        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item.Value.ToHexString());
        }
    }

    public void WriteTimeOnly(JsonWritingUnit writer, string? fieldName, TimeOnly? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(TimeOnlyHelper.TimeOnlyPrinter(item.Value));
        }
    }

    public void WriteDateOnly(JsonWritingUnit writer, string? fieldName, DateOnly? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            writer.Writer.WriteValue(item.Value.ToString("MM/dd/yyyy"));
        }
    }

    public void WriteGuid(JsonWritingUnit writer, string? fieldName, Guid? item)
    {
        WriteString(writer, fieldName, item?.ToString());
    }

    public void WriteTranslatedString(JsonWritingUnit writer, string? fieldName, ITranslatedStringGetter? item)
    {
        writer.WriteName(fieldName);

        if (item == null)
        {
            writer.Writer.WriteStartObject();
            writer.Writer.WriteEndObject();
            return;
        }
        
        writer.Writer.WriteStartObject();
        writer.Writer.WritePropertyName(TranslatedStringNames.TargetLanguage);
        writer.Writer.WriteValue(item.TargetLanguage.ToStringFast());

        if (item.NumLanguages <= 1)
        {
            if (item.String is { } str)
            {
                writer.Writer.WritePropertyName(TranslatedStringNames.Value);
                writer.Writer.WriteValue($"{str}");
            }
        }
        else
        {
            writer.Writer.WritePropertyName(TranslatedStringNames.Values);
            writer.Writer.WriteStartArray();
            foreach (var entry in item)
            {
                writer.Writer.WriteStartObject();
                writer.Writer.WritePropertyName(TranslatedStringNames.Language);
                writer.Writer.WriteValue(entry.Key.ToStringFast());
                writer.Writer.WritePropertyName(TranslatedStringNames.String);
                writer.Writer.WriteValue(entry.Value);
                writer.Writer.WriteEndObject();
            }
            writer.Writer.WriteEndArray();
        }
        // The text's ID in its plugin's .strings tables, so a localized plugin rebuilt from JSON keeps it (C214)
        if (StringsKeyNote.TryGet(item, out var stringsKey, out var stringsSource, out var stringsPlugin))
        {
            writer.Writer.WritePropertyName(StringsKeyNote.KeyName);
            writer.Writer.WriteValue(stringsKey);
            writer.Writer.WritePropertyName(StringsKeyNote.SourceName);
            writer.Writer.WriteValue(stringsSource);
            writer.Writer.WritePropertyName(StringsKeyNote.PluginName);
            writer.Writer.WriteValue(stringsPlugin);
        }
        writer.Writer.WriteEndObject();
    }

    public void WriteBytes(JsonWritingUnit writer, string? fieldName, ReadOnlyMemorySlice<byte>? item)
    {
        writer.WriteName(fieldName);
        if (item == null)
        {
            writer.Writer.WriteValue(string.Empty);
        }
        else
        {
            // One form for bytes: "0x" and the hex digits, "0x" alone when there are none ("[]" is still read)
            writer.Writer.WriteValue($"0x{Convert.ToHexString(item.Value)}");
        }
    }

    public void WriteEnum<TEnum>(JsonWritingUnit writer, string? fieldName, TEnum? item)
        where TEnum : struct, Enum, IConvertible
    {
        writer.WriteName(fieldName);
        if (item == null)
        {
            writer.Writer.WriteValue("");
        }
        else if (!Enums<TEnum>.IsFlagsEnum)
        {
            writer.Writer.WriteValue(item.Value.ToStringFast());
        }
        else
        {
            writer.Writer.WriteStartArray();
            foreach (var flag in item.Value.EnumerateContainedFlags(includeUndefined: true))
            {
                writer.Writer.WriteValue(flag.ToStringFast());
            }
            writer.Writer.WriteEndArray();
        }
    }

    public void WriteWithName<TKernel, TObject>(
        MutagenSerializationWriterKernel<TKernel, JsonWritingUnit> kernel, 
        JsonWritingUnit writer,
        string? fieldName,
        TObject item, 
        SerializationMetaData serializationMetaData,
        WriteAsync<TKernel, JsonWritingUnit, TObject> writeCall) 
        where TKernel : ISerializationWriterKernel<JsonWritingUnit>, new()
    {
        writer.WriteName(fieldName);
        writeCall(writer, item, kernel, serializationMetaData);
    }

    public async Task WriteLoqui<TKernel, TObject>(
        MutagenSerializationWriterKernel<TKernel, JsonWritingUnit> kernel,
        JsonWritingUnit writer, 
        string? fieldName,
        TObject item,
        SerializationMetaData serializationMetaData,
        WriteAsync<TKernel, JsonWritingUnit, TObject> writeCall) 
        where TKernel : ISerializationWriterKernel<JsonWritingUnit>, new()
    {
        writer.WriteName(fieldName);
        writer.Writer.WriteStartObject();
        await writeCall(writer, item, kernel, serializationMetaData);
        writer.Writer.WriteEndObject();
    }

    public void WriteLoqui<TKernel, TObject>(
        MutagenSerializationWriterKernel<TKernel, JsonWritingUnit> kernel,
        JsonWritingUnit writer, 
        string? fieldName,
        TObject item,
        SerializationMetaData serializationMetaData,
        Write<TKernel, JsonWritingUnit, TObject> writeCall) 
        where TKernel : ISerializationWriterKernel<JsonWritingUnit>, new()
    {
        writer.WriteName(fieldName);
        writer.Writer.WriteStartObject();
        writeCall(writer, item, kernel, serializationMetaData);
        writer.Writer.WriteEndObject();
    }

    public void StartListSection(JsonWritingUnit writer, string? fieldName)
    {
        writer.WriteName(fieldName);
        writer.Writer.WriteStartArray();
    }

    public void EndListSection(JsonWritingUnit writer)
    {
        writer.Writer.WriteEndArray();
    }

    public void StartDictionarySection(JsonWritingUnit writer, string? fieldName)
    {
        writer.WriteName(fieldName);
        writer.Writer.WriteStartArray();
    }

    public void EndDictionarySection(JsonWritingUnit writer)
    {
        writer.Writer.WriteEndArray();
    }

    public void StartDictionaryItem(JsonWritingUnit writer)
    {
        writer.Writer.WriteStartObject();
    }

    public void EndDictionaryItem(JsonWritingUnit writer)
    {
        writer.Writer.WriteEndObject();
    }

    public void StartDictionaryKey(JsonWritingUnit writer)
    {
        writer.WriteName("Key");
    }

    public void EndDictionaryKey(JsonWritingUnit writer)
    {
    }

    public void StartDictionaryValue(JsonWritingUnit writer)
    {
        writer.WriteName("Value");
    }

    public void EndDictionaryValue(JsonWritingUnit writer)
    {
    }

    public void StartArray2dSection(JsonWritingUnit writer, string? fieldName)
    {
        writer.WriteName(fieldName);
        writer.Writer.WriteStartArray();
    }

    public void EndArray2dSection(JsonWritingUnit writer)
    {
        writer.Writer.WriteEndArray();
    }

    public void StartArray2dXItem(JsonWritingUnit writer)
    {
    }

    public void EndArray2dXItem(JsonWritingUnit writer)
    {
    }

    public void StartArray2dYSection(JsonWritingUnit writer)
    {
        writer.Writer.WriteStartArray();
        writer.Writer.Formatting = Formatting.None;
    }

    public void EndArray2dYSection(JsonWritingUnit writer)
    {
        writer.Writer.WriteEndArray();
        writer.Writer.Formatting = Formatting.Indented;
    }

    public Exception ConvertException(Exception ex)
    {
        return ex;
    }
}