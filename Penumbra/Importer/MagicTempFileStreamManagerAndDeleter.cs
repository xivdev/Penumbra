using System;
using System.IO;
using Penumbra.Util;

namespace Penumbra.Importer
{
    public class MagicTempFileStreamManagerAndDeleter : PenumbraSqPackStream, IDisposable
    {
        private readonly FileStream _fileStream;

        public MagicTempFileStreamManagerAndDeleter( FileStream stream )
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
}