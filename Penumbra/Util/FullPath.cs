using System;
using System.IO;
using Penumbra.GameData.Util;

namespace Penumbra.Util;

public readonly struct FullPath : IComparable, IEquatable< FullPath >
{
    public readonly string FullName;
    public readonly string InternalName;
    public readonly ulong  Crc64;

    public FullPath( DirectoryInfo baseDir, RelPath relPath )
    {
        FullName     = Path.Combine( baseDir.FullName, relPath );
        InternalName = FullName.Replace( '\\', '/' ).ToLowerInvariant().Trim();
        Crc64        = ComputeCrc64( InternalName );
    }

    public FullPath( FileInfo file )
    {
        FullName = file.FullName;
        InternalName = FullName.Replace( '\\', '/' ).ToLowerInvariant().Trim();
        Crc64    = ComputeCrc64( InternalName );
    }

    public bool Exists
        => File.Exists( FullName );

    public string Extension
        => Path.GetExtension( FullName );

    public string Name
        => Path.GetFileName( FullName );

    public GamePath ToGamePath( DirectoryInfo dir )
        => FullName.StartsWith(dir.FullName) ? GamePath.GenerateUnchecked( InternalName[(dir.FullName.Length+1)..]) : GamePath.GenerateUnchecked( string.Empty );

    private static ulong ComputeCrc64( string name )
    {
        if( name.Length == 0 )
        {
            return 0;
        }

        var lastSlash = name.LastIndexOf( '/' );
        if( lastSlash == -1 )
        {
            return Lumina.Misc.Crc32.Get( name );
        }

        var folder = name[ ..lastSlash ];
        var file   = name[ ( lastSlash + 1 ).. ];
        return ( ( ulong )Lumina.Misc.Crc32.Get( folder ) << 32 ) | Lumina.Misc.Crc32.Get( file );
    }

    public int CompareTo( object? obj )
        => obj switch
        {
            FullPath p => string.Compare( InternalName, p.InternalName, StringComparison.InvariantCulture ),
            FileInfo f => string.Compare( FullName, f.FullName, StringComparison.InvariantCultureIgnoreCase ),
            _          => -1,
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
        => Crc64.GetHashCode();

    public override string ToString()
        => FullName;
}