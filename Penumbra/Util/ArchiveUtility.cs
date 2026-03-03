using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Penumbra.Util;

public static class ArchiveUtility
{
    //private static readonly ZipWriterOptions DefaultArchiveOptions = new(CompressionType.LZMA, CompressionLevel.Level0);

    private static readonly ExtractionOptions ExtractionOptions = new()
    {
        ExtractFullPath = true,
        Overwrite       = true,
    };

    public static void CreateFromDirectory(string directoryPath, string filePath)
    {
        ZipFile.CreateFromDirectory(directoryPath, filePath, CompressionLevel.SmallestSize, false);
        //using var archive = ZipArchive.Create();
        //archive.AddAllFromDirectory(directoryPath);
        //archive.SaveTo(filePath, DefaultArchiveOptions);
    }

    public static void ExtractToDirectory(string filePath, string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        ArchiveFactory.WriteToDirectory(filePath, directoryPath, ExtractionOptions);
    }

    public static void ForEachEntry(IArchive archive, Action<ReaderShim> action)
    {
        if (archive.IsSolid || archive.Type is ArchiveType.SevenZip)
        {
            var reader = archive.ExtractAllEntries();

            while (reader.MoveToNextEntry())
                action(new(reader.Entry, reader.OpenEntryStream));
        }
        else
        {
            foreach (var entry in archive.Entries)
                action(new(entry, entry.OpenEntryStream));
        }
    }

    /// <summary> This shim imitates the parts of <see cref="IReader"/> that are used throughout the importers. </summary>
    public readonly record struct ReaderShim(IEntry Entry, Func<Stream> OpenEntryStream)
    {
        public void WriteEntryToDirectory(string directory)
        {
            var path = Path.Combine(directory, Entry.Key!);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var e = OpenEntryStream();
            using var f = File.Open(path, FileMode.Create, FileAccess.Write);
            e.CopyTo(f);
        }
    }
}
