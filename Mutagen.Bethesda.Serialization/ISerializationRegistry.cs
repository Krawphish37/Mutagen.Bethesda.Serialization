using System.Diagnostics.CodeAnalysis;
using Loqui;
using Mutagen.Bethesda.Serialization.Streams;

namespace Mutagen.Bethesda.Serialization;

/// <summary>
/// The serializers the source generator wrote into an assembly for one mod type and every object that mod holds, by
/// runtime type (C59, W11). The generator writes one public registry for each mod type a bootstrap is called with,
/// beside the bootstrap's mix-ins, in the mod's namespace: <c>StarfieldSerializationRegistry.Instance</c> for
/// <c>MutagenJsonConverter.Instance.Serialize(IStarfieldModGetter, …)</c>.<br/>
/// It lets a caller holding an object of a type it does not know at compile time (an <c>IMajorRecordGetter</c>, an
/// overlay or a full object) serialize it with the serializer generated for its class, and read an object of a class
/// it names at run time, with any kernel. The generated per-class serializers stay internal.<br/>
/// The methods return tasks, as the generated serializers do; for one object with the default (inline) work dropoff and
/// the JSON or YAML kernel they complete before they return. The JSON package's <c>JsonObjectConverter</c> gives
/// synchronous calls on top of this.
/// </summary>
public interface ISerializationRegistry
{
    /// <summary>The concrete classes it has serializers for (records and the objects they hold; not mods or groups)</summary>
    IReadOnlyCollection<Type> ClassTypes { get; }

    /// <summary>The class of one of <see cref="ClassTypes"/>, given the class or its getter or setter interface</summary>
    bool TryGetClassType(Type type, [MaybeNullWhen(false)] out Type classType);

    /// <summary>
    /// The class of one of <see cref="ClassTypes"/> by its full name ("Mutagen.Bethesda.Starfield.Weapon") or, when no
    /// other class has it, its plain name ("Weapon")
    /// </summary>
    bool TryGetClassType(string name, [MaybeNullWhen(false)] out Type classType);

    /// <summary>
    /// The value each field of a class is left out at when it holds it (the writer leaves a field out when its written
    /// value equals its default, unless the kernel writes defaults: <see cref="MutagenSerializationWriterKernel{TKernel,TWriterObject}.WriteDefaults"/>),
    /// by field name, for the class (or its getter or setter interface) and its base classes (C59, S124). It holds the
    /// fields compared with a default: numbers, texts, enums (as the enum value), links (as the FormKey written, null for
    /// a nullable link), asset paths, byte arrays and the like, with Mutagen's own defaults where a class has them
    /// (e.g. BookTeachesNothing's RawContent: uint.MaxValue). Not listed: fields always written (FormVersion), lists
    /// (left out when empty), and sub-objects (left out when all their fields are left out).
    /// </summary>
    bool TryGetFieldDefaults(Type type, [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object?> defaults);

    /// <summary>
    /// Writes the object's fields with the serializer generated for its class (<see cref="SerializationRegistry.ClassTypeOf"/>),
    /// into the current object of <paramref name="writer"/>, as the class's own generated Serialize does: no type tag.
    /// </summary>
    /// <exception cref="ArgumentException">Its class is not one of <see cref="ClassTypes"/></exception>
    Task Serialize<TKernel, TWriteObject>(
        TWriteObject writer,
        object item,
        MutagenSerializationWriterKernel<TKernel, TWriteObject> kernel,
        SerializationMetaData metaData)
        where TKernel : ISerializationWriterKernel<TWriteObject>, new()
        where TWriteObject : IContainStreamPackage;

    /// <summary>
    /// Reads a new object of the class <paramref name="type"/> names (the class, or its getter or setter interface) with
    /// the deserializer generated for it. A major record is made with <paramref name="metaData"/>'s release, so that must
    /// be the record's game. For an object in hand (an overlay's type is none of these) give
    /// <see cref="SerializationRegistry.ClassTypeOf"/> of it.
    /// </summary>
    /// <exception cref="ArgumentException">The type is not one of <see cref="ClassTypes"/> or their interfaces</exception>
    Task<object> Deserialize<TReadObject>(
        TReadObject reader,
        Type type,
        ISerializationReaderKernel<TReadObject> kernel,
        SerializationMetaData metaData)
        where TReadObject : IContainStreamPackage;
}

/// <summary>What the generated <see cref="ISerializationRegistry"/> classes share</summary>
public static class SerializationRegistry
{
    /// <summary>
    /// The class an object is serialized as: its Loqui registration's class (an overlay is written as the class it is
    /// read as, e.g. WeaponBinaryOverlay as Weapon), or its own type for an object that isn't a Loqui object
    /// </summary>
    public static Type ClassTypeOf(object item) =>
        item is ILoquiObject loqui ? loqui.Registration.ClassType : item.GetType();

    /// <summary>The classes by full name, and by plain name where no two classes share it</summary>
    public static Dictionary<string, Type> NamesOf(IEnumerable<Type> classes)
    {
        var ret = new Dictionary<string, Type>(StringComparer.Ordinal);
        var all = classes.ToArray();
        foreach (var type in all)
        {
            if (type.FullName is { } full) ret[full] = type;
        }
        foreach (var group in all.GroupBy(t => t.Name, StringComparer.Ordinal))
        {
            if (group.Count() == 1 && !ret.ContainsKey(group.Key)) ret[group.Key] = group.First();
        }
        return ret;
    }

    /// <summary>A generated deserializer's task as a task of object</summary>
    public static Task<object> AsObject<T>(Task<T> task)
        where T : class
    {
        if (task.IsCompletedSuccessfully) return Task.FromResult<object>(task.Result);
        return Await(task);

        static async Task<object> Await(Task<T> t) => await t.ConfigureAwait(false);
    }

    /// <summary>The error for a type a registry has no serializer for</summary>
    public static ArgumentException NotRegistered(Type type, ISerializationRegistry registry) =>
        new($"{registry.GetType().Name} has no serializer for {type.FullName}");
}
