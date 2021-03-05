using System.IO;

namespace Penumbra.Util
{
    public static class TempFile
    {
        public static FileInfo TempFileName( DirectoryInfo baseDir )
        {
            const uint maxTries = 15;
            for( var i = 0; i < maxTries; ++i )
            {
                var name = Path.GetRandomFileName();
                var path = new FileInfo( Path.Combine( baseDir.FullName, name ) );
                if( !path.Exists )
                {
                    return path;
                }
            }

            throw new IOException();
        }

        public static FileInfo WriteNew( DirectoryInfo baseDir, byte[] data )
        {
            var fileName = TempFileName( baseDir );
            File.WriteAllBytes( fileName.FullName, data );
            fileName.Refresh();
            return fileName;
        }
    }
}
