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
                suffix.Length > 0 ? name[ ..name.LastIndexOf( '.' ) ] + suffix : name ) );
            if( !path.Exists )
            {
                return path;
            }
        }

        throw new IOException();
    }
}