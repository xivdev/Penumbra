using System;
using System.IO;
using System.Linq;

namespace Penumbra.Util
{
    public readonly struct RelPath : IComparable
    {
        public const int MaxRelPathLength = 256;

        private readonly string _path;

        private RelPath( string path, bool _ )
            => _path = path;

        private RelPath( string? path )
        {
            if( path != null && path.Length < MaxRelPathLength )
            {
                _path = Trim( ReplaceSlash( path ) );
            }
            else
            {
                _path = "";
            }
        }

        public RelPath( FileInfo file, DirectoryInfo baseDir )
            => _path = CheckPre( file, baseDir ) ? Trim( Substring( file, baseDir ) ) : "";

        public RelPath( GamePath gamePath )
            => _path = ReplaceSlash( gamePath );

        public GamePath ToGamePath( int skipFolders = 0 )
        {
            string p = this;
            if( skipFolders > 0 )
            {
                p = string.Join( "/", p.Split( '\\' ).Skip( skipFolders ) );
                return GamePath.GenerateUncheckedLower( p );
            }

            return GamePath.GenerateUncheckedLower( p.Replace( '\\', '/' ) );
        }

        private static bool CheckPre( FileInfo file, DirectoryInfo baseDir )
            => file.FullName.StartsWith( baseDir.FullName ) && file.FullName.Length < MaxRelPathLength;

        private static string Substring( FileInfo file, DirectoryInfo baseDir )
            => file.FullName.Substring( baseDir.FullName.Length );

        private static string ReplaceSlash( string path )
            => path.Replace( '/', '\\' );

        private static string Trim( string path )
            => path.TrimStart( '\\' );

        public static implicit operator string( RelPath relPath )
            => relPath._path;

        public static explicit operator RelPath( string relPath )
            => new( relPath );

        public bool Empty
            => _path.Length == 0;

        public int CompareTo( object rhs )
        {
            return rhs switch
            {
                string path  => string.Compare( _path, path, StringComparison.InvariantCulture ),
                RelPath path => string.Compare( _path, path._path, StringComparison.InvariantCulture ),
                _            => -1,
            };
        }

        public override string ToString()
            => _path;
    }
}