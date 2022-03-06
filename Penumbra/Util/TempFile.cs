using System.IO;
using System.Linq;

namespace Penumbra.Util;

public static class TempFile
{
    public static FileInfo TempFileName( DirectoryInfo baseDir, string suffix = "" )
    {
        const uint maxTries = 15;
        for( var i = 0; i < maxTries; ++i )
        {
            var name = Path.GetRandomFileName();
            var path = new FileInfo( Path.Combine( baseDir.FullName,
                suffix.Any() ? name.Substring( 0, name.LastIndexOf( '.' ) ) + suffix : name ) );
            if( !path.Exists )
            {
                return path;
            }
        }

        throw new IOException();
    }

    public static FileInfo WriteNew( DirectoryInfo baseDir, byte[] data, string suffix = "" )
    {
        var       fileName = TempFileName( baseDir, suffix );
        using var stream   = fileName.OpenWrite();
        stream.Write( data, 0, data.Length );
        fileName.Refresh();
        return fileName;
    }
}