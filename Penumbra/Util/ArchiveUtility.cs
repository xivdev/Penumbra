using SharpCompress.Archives;
using SharpCompress.Common;

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
}
