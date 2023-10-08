using Penumbra.Util;

namespace Penumbra.Import.Structs;

// Create an automatically disposing SqPack stream.
public class StreamDisposer : PenumbraSqPackStream
{
    private readonly FileStream _fileStream;

    public StreamDisposer(FileStream stream)
        : base(stream)
        => _fileStream = stream;

    protected override void Dispose(bool _)
    {
        var filePath = _fileStream.Name;
        _fileStream.Dispose();
        File.Delete(filePath);
    }
}
