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
    /// A link, or an index (an alias or a package data input) when the holder's flags say so. An index is written
    /// whenever it has one, 0 included (S131, C59): it is not left out as a default, because a FormLinkOrIndex read back
    /// without its field has no index at all (Mutagen makes it with a null Index), and 0 is a real alias or input. An index
    /// that is null is left out, so it reads back null as it was; it is never dereferenced here.
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
        else if (item.Index is { } index)
        {
            kernel.WriteUInt32(writer, fieldName, index, 0, checkDefaults: false);
        }
    }
}