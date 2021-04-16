using System;
using System.IO;
using Lumina.Data;
using Penumbra.Util;

namespace Penumbra.Importer
{
    public class MagicTempFileStreamManagerAndDeleterFuckery : PenumbraSqPackStream, IDisposable
    {
        private readonly FileStream _fileStream;

        public MagicTempFileStreamManagerAndDeleterFuckery( FileStream stream ) : base( stream ) => _fileStream = stream;

        public new void Dispose()
        {
            var filePath = _fileStream.Name;

            base.Dispose();
            _fileStream.Dispose();

            File.Delete( filePath );
        }
    }
}