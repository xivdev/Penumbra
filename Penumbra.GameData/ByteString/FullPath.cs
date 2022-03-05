using System;
using System.IO;
using Penumbra.GameData.Util;

namespace Penumbra.GameData.ByteString;

public readonly struct FullPath : IComparable, IEquatable< FullPath >
{
    public readonly string     FullName;
    public readonly Utf8String InternalName;
    public readonly ulong      Crc64;


    public FullPath( DirectoryInfo baseDir, NewRelPath relPath )
        : this( Path.Combine( baseDir.FullName, relPath.ToString() ) )
    { }

    public FullPath( FileInfo file )
        : this( file.FullName )
    { }

    public FullPath( string s )
    {
        FullName     = s;
        InternalName = Utf8String.FromString( FullName, out var name, true ) ? name : Utf8String.Empty;
        Crc64        = Functions.ComputeCrc64( InternalName.Span );
    }

    public bool Exists
        => File.Exists( FullName );

    public string Extension
        => Path.GetExtension( FullName );

    public string Name
        => Path.GetFileName( FullName );

    public bool ToGamePath( DirectoryInfo dir, out NewGamePath path )
    {
        path = NewGamePath.Empty;
        if( !InternalName.IsAscii || !FullName.StartsWith( dir.FullName ) )
        {
            return false;
        }

        var substring = InternalName.Substring( dir.FullName.Length + 1 );

        path = new NewGamePath( substring.Replace( ( byte )'\\', ( byte )'/' ) );
        return true;
    }

    public bool ToRelPath( DirectoryInfo dir, out NewRelPath path )
    {
        path = NewRelPath.Empty;
        if( !FullName.StartsWith( dir.FullName ) )
        {
            return false;
        }

        var substring = InternalName.Substring( dir.FullName.Length + 1 );

        path = new NewRelPath( substring );
        return true;
    }

    public int CompareTo( object? obj )
        => obj switch
        {
            FullPath p   => InternalName.CompareTo( p.InternalName ),
            FileInfo f   => string.Compare( FullName, f.FullName, StringComparison.InvariantCultureIgnoreCase ),
            Utf8String u => InternalName.CompareTo( u ),
            string s     => string.Compare( FullName, s, StringComparison.InvariantCultureIgnoreCase ),
            _            => -1,
        };

    public bool Equals( FullPath other )
    {
        if( Crc64 != other.Crc64 )
        {
            return false;
        }

        if( FullName.Length == 0 || other.FullName.Length == 0 )
        {
            return true;
        }

        return InternalName.Equals( other.InternalName );
    }

    public override int GetHashCode()
        => InternalName.Crc32;

    public override string ToString()
        => FullName;
}