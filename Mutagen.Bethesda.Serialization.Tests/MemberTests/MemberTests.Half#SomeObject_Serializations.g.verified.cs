//HintName: SomeObject_Serializations.g.cs
using Loqui;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Exceptions;
using Mutagen.Bethesda.Serialization;
using Mutagen.Bethesda.Serialization.Exceptions;
using Mutagen.Bethesda.Serialization.SourceGenerator.Tests;
using Mutagen.Bethesda.Serialization.Streams;
using Mutagen.Bethesda.Serialization.Utility;
using Noggog;
using Noggog.IO;
using Noggog.WorkEngine;
using System.IO.Abstractions;
using System.Threading.Tasks;

#nullable enable

#pragma warning disable CS1998 // No awaits used
#pragma warning disable CS0618 // Obsolete

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Tests;

internal static class SomeObject_Serialization
{
    public static async Task Serialize<TKernel, TWriteObject>(
        TWriteObject writer,
        Mutagen.Bethesda.Serialization.SourceGenerator.Tests.ISomeObjectGetter item,
        MutagenSerializationWriterKernel<TKernel, TWriteObject> kernel,
        SerializationMetaData metaData)
        where TKernel : ISerializationWriterKernel<TWriteObject>, new()
        where TWriteObject : IContainStreamPackage
    {
        metaData.Cancel.ThrowIfCancellationRequested();
        await SerializeFields<TKernel, TWriteObject>(
            writer: writer,
            item: item,
            kernel: kernel,
            metaData: metaData);
    }

    public static async Task SerializeFields<TKernel, TWriteObject>(
        TWriteObject writer,
        Mutagen.Bethesda.Serialization.SourceGenerator.Tests.ISomeObjectGetter item,
        MutagenSerializationWriterKernel<TKernel, TWriteObject> kernel,
        SerializationMetaData metaData)
        where TKernel : ISerializationWriterKernel<TWriteObject>, new()
        where TWriteObject : IContainStreamPackage
    {
        metaData.Cancel.ThrowIfCancellationRequested();
        kernel.WriteHalf(writer, "SomeMember0", item.SomeMember0, default(System.Half));
        kernel.WriteHalf(writer, "SomeMember1", item.SomeMember1, default(Half));
        kernel.WriteHalf(writer, "SomeMember2", item.SomeMember2, default(System.Half?));
        kernel.WriteHalf(writer, "SomeMember3", item.SomeMember3, default(Half?));
        kernel.WriteHalf(writer, "SomeMember4", item.SomeMember4, default(Nullable<System.Half>));
        kernel.WriteHalf(writer, "SomeMember5", item.SomeMember5, default(Nullable<Half>));
        kernel.WriteHalf(writer, "SomeMember6", item.SomeMember6, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember6Default);
        kernel.WriteHalf(writer, "SomeMember7", item.SomeMember7, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember7Default);
        kernel.WriteHalf(writer, "SomeMember8", item.SomeMember8, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember8Default);
        kernel.WriteHalf(writer, "SomeMember9", item.SomeMember9, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember9Default);
        kernel.WriteHalf(writer, "SomeMember10", item.SomeMember10, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember10Default);
        kernel.WriteHalf(writer, "SomeMember11", item.SomeMember11, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember11Default);
    }

    public static bool HasSerializationItems(
        Mutagen.Bethesda.Serialization.SourceGenerator.Tests.ISomeObjectGetter? item,
        SerializationMetaData metaData)
    {
        metaData.Cancel.ThrowIfCancellationRequested();
        if (item == null) return false;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember0, default(System.Half))) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember1, default(Half))) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember2, default(System.Half?))) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember3, default(Half?))) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember4, default(Nullable<System.Half>))) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember5, default(Nullable<Half>))) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember6, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember6Default)) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember7, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember7Default)) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember8, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember8Default)) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember9, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember9Default)) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember10, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember10Default)) return true;
        if (!Mutagen.Bethesda.Serialization.FloatSerialization.BitEquals(item.SomeMember11, Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject.SomeMember11Default)) return true;
        return false;
    }

    public static async Task<Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject> Deserialize<TReadObject>(
        TReadObject reader,
        ISerializationReaderKernel<TReadObject> kernel,
        SerializationMetaData metaData)
        where TReadObject : IContainStreamPackage
    {
        metaData.Cancel.ThrowIfCancellationRequested();
        var obj = new Mutagen.Bethesda.Serialization.SourceGenerator.Tests.SomeObject();
        await DeserializeInto<TReadObject>(
            reader: reader,
            kernel: kernel,
            obj: obj,
            metaData: metaData);
        return obj;
    }

    public static async Task DeserializeSingleFieldInto<TReadObject>(
        TReadObject reader,
        ISerializationReaderKernel<TReadObject> kernel,
        Mutagen.Bethesda.Serialization.SourceGenerator.Tests.ISomeObject obj,
        SerializationMetaData metaData,
        string name)
        where TReadObject : IContainStreamPackage
    {
        metaData.Cancel.ThrowIfCancellationRequested();
        switch (name)
        {
            case "SomeMember0":
                obj.SomeMember0 = SerializationHelper.StripNull(kernel.ReadHalf(reader), name: "SomeMember0");
                break;
            case "SomeMember1":
                obj.SomeMember1 = SerializationHelper.StripNull(kernel.ReadHalf(reader), name: "SomeMember1");
                break;
            case "SomeMember2":
                obj.SomeMember2 = kernel.ReadHalf(reader);
                break;
            case "SomeMember3":
                obj.SomeMember3 = kernel.ReadHalf(reader);
                break;
            case "SomeMember4":
                obj.SomeMember4 = kernel.ReadHalf(reader);
                break;
            case "SomeMember5":
                obj.SomeMember5 = kernel.ReadHalf(reader);
                break;
            case "SomeMember6":
                obj.SomeMember6 = SerializationHelper.StripNull(kernel.ReadHalf(reader), name: "SomeMember6");
                break;
            case "SomeMember7":
                obj.SomeMember7 = SerializationHelper.StripNull(kernel.ReadHalf(reader), name: "SomeMember7");
                break;
            case "SomeMember8":
                obj.SomeMember8 = kernel.ReadHalf(reader);
                break;
            case "SomeMember9":
                obj.SomeMember9 = kernel.ReadHalf(reader);
                break;
            case "SomeMember10":
                obj.SomeMember10 = kernel.ReadHalf(reader);
                break;
            case "SomeMember11":
                obj.SomeMember11 = kernel.ReadHalf(reader);
                break;
            default:
                kernel.Skip(reader);
                break;
        }
    }
    
    public static async Task DeserializeInto<TReadObject>(
        TReadObject reader,
        ISerializationReaderKernel<TReadObject> kernel,
        Mutagen.Bethesda.Serialization.SourceGenerator.Tests.ISomeObject obj,
        SerializationMetaData metaData)
        where TReadObject : IContainStreamPackage
    {
        metaData.Cancel.ThrowIfCancellationRequested();
        while (kernel.TryGetNextField(reader, out var name))
        {
            await DeserializeSingleFieldInto<TReadObject>(
                reader: reader,
                kernel: kernel,
                obj: obj,
                metaData: metaData,
                name: name);
        }

    }

}

