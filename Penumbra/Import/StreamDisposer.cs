using System;
using System.IO;
using Penumbra.Util;

namespace Penumbra.Import;

// Create an automatically disposing SqPack stream.
public class StreamDisposer : PenumbraSqPackStream, IDisposable
{
    private readonly FileStream _fileStream;

    public StreamDisposer( FileStream stream )
        : base( stream )
        => _fileStream = stream;

    public new void Dispose()
    {
        var filePath = _fileStream.Name;

        base.Dispose();
        _fileStream.Dispose();

        File.Delete( filePath );
    }
}