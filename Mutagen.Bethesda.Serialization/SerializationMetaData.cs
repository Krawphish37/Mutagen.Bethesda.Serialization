using System.IO.Abstractions;
using Mutagen.Bethesda.Plugins.Meta;
using Noggog;
using Noggog.IO;
using Noggog.WorkEngine;

namespace Mutagen.Bethesda.Serialization;

public class SerializationMetaData
{
    public GameRelease Release { get; }
    public GameConstants Constants { get; }
    public IWorkDropoff WorkDropoff { get; }
    public IFileSystem FileSystem { get; }
    public ICreateStream StreamCreator { get; }
    public CancellationToken Cancel { get; }

    /// <summary>
    /// Whether the round-trip notes are written: a record's header FormID and links as stored, its child-group labels,
    /// subrecord order and compressed bytes, a cell's child-group layout, and a mod's empty groups, FormID style and
    /// header baselines. They are what a plugin rebuilt from the output needs to come back byte for byte. On by default.
    /// A writer that only looks at the fields (a link walk, a search) can turn them off: finding a record's notes writes
    /// the record again into a scratch buffer and compresses a compressed record again, which costs about as much as the
    /// rest of its serialization (perf-world).
    /// </summary>
    public bool RoundTripNotes { get; init; } = true;

    public SerializationMetaData(
        GameRelease release, 
        IWorkDropoff? workDropoff,
        IFileSystem? fileSystem, 
        ICreateStream? streamCreator,
        CancellationToken cancel)
    {
        Release = release;
        Constants = GameConstants.Get(release);
        Cancel = cancel;
        WorkDropoff = workDropoff.GetOrFallback(() => InlineWorkDropoff.Instance);
        StreamCreator = streamCreator.GetOrFallback(() => NormalFileStreamCreator.Instance);
        FileSystem = fileSystem ?? IFileSystemExt.DefaultFilesystem;
    }

    // For testing
    public SerializationMetaData(GameRelease release)
    {
        Release = release;
        WorkDropoff = null!;
        Constants = null!;
        StreamCreator = null!;
        FileSystem = null!;
        Cancel = CancellationToken.None;
    }

    public SerializationMetaData(CancellationToken cancel)
    {
        Release = default;
        Constants = null!;
        WorkDropoff = null!;
        StreamCreator = null!;
        FileSystem = null!;
        Cancel = cancel;
    }
}