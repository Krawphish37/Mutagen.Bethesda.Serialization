using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;

namespace Mutagen.Bethesda.Serialization.Utility;

public static partial class SerializationHelper
{
    public static void ReadFormLinkOrIndex<TKernel, TReadObject, TMajorGetter>(
        TReadObject reader,
        TKernel kernel,
        IFormLinkOrIndex<TMajorGetter> item,
        SerializationMetaData metaData)
        where TKernel : ISerializationReaderKernel<TReadObject>
        where TMajorGetter : class, IMajorRecordGetter
    {
        if (item.UsesLink())
        {
            item.Link.SetTo(kernel.ReadFormKey(reader));
        }
        else
        {
            item.Index = kernel.ReadUInt32(reader);
        }
    }

    /// <summary>
    /// A link, or an index (an alias or a package data input) when the holder's flags say so. An index is always written,
    /// 0 included (S131, C59): 0 is a real alias or input, not a default to leave out. A null index is written as 0, the
    /// way Mutagen's binary write does and as Mutagen 0.55.0-fixes.20's FormLinkOrIndex counts it (C68: a new one has
    /// index 0, and Equals, the hash and copies count a null index as 0), so the JSON of an object read back from it is
    /// the same JSON. It is never dereferenced here.
    /// </summary>
    public static void WriteFormLinkOrIndex<TKernel, TWriteObject, TMajorGetter>(
        TWriteObject writer,
        string? fieldName, 
        IFormLinkOrIndexGetter<TMajorGetter> item,
        SerializationMetaData metaData,
        MutagenSerializationWriterKernel<TKernel, TWriteObject> kernel)
        where TKernel : ISerializationWriterKernel<TWriteObject>, new()
        where TMajorGetter : class, IMajorRecordGetter
    {
        if (item.UsesLink())
        {
            kernel.WriteFormKey(writer, fieldName, item.Link.FormKey, FormKey.Null);
        }
        else
        {
            kernel.WriteUInt32(writer, fieldName, item.Index ?? 0, 0, checkDefaults: false);
        }
    }
}