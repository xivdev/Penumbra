using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Util;

namespace Penumbra.GameData.ByteString;

[JsonConverter( typeof( FullPathConverter ) )]
public readonly struct FullPath : IComparable, IEquatable< FullPath >
{
    public readonly string     FullName;
    public readonly Utf8String InternalName;
    public readonly ulong      Crc64;

    public static readonly FullPath Empty   = new(string.Empty);

    public FullPath( DirectoryInfo baseDir, Utf8RelPath relPath )
        : this( Path.Combine( baseDir.FullName, relPath.ToString() ) )
    { }

    public FullPath( FileInfo file )
        : this( file.FullName )
    { }


    public FullPath( string s )
    {
        FullName     = s;
        InternalName = Utf8String.FromString( FullName.Replace( '\\', '/' ), out var name, true ) ? name : Utf8String.Empty;
        Crc64        = Functions.ComputeCrc64( InternalName.Span );
    }

    public bool Exists
        => File.Exists( FullName );

    public string Extension
        => Path.GetExtension( FullName );

    public string Name
        => Path.GetFileName( FullName );

    public bool ToGamePath( DirectoryInfo dir, out Utf8GamePath path )
    {
        path = Utf8GamePath.Empty;
        if( !InternalName.IsAscii || !FullName.StartsWith( dir.FullName ) )
        {
            return false;
        }

        var substring = InternalName.Substring( dir.FullName.Length + 1 );

        path = new Utf8GamePath( substring );
        return true;
    }

    public bool ToRelPath( DirectoryInfo dir, out Utf8RelPath path )
    {
        path = Utf8RelPath.Empty;
        if( !FullName.StartsWith( dir.FullName ) )
        {
            return false;
        }

        var substring = InternalName.Substring( dir.FullName.Length + 1 );

        path = new Utf8RelPath( substring.Replace( ( byte )'/', ( byte )'\\' ) );
        return true;
    }

    public int CompareTo( object? obj )
        => obj switch
        {
            FullPath p   => InternalName?.CompareTo( p.InternalName ) ?? -1,
            FileInfo f   => string.Compare( FullName, f.FullName, StringComparison.OrdinalIgnoreCase ),
            Utf8String u => InternalName?.CompareTo( u ) ?? -1,
            string s     => string.Compare( FullName, s, StringComparison.OrdinalIgnoreCase ),
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

    public bool IsRooted
        => new Utf8GamePath( InternalName ).IsRooted();

    public override int GetHashCode()
        => InternalName.Crc32;

    public override string ToString()
        => FullName;

    public class FullPathConverter : JsonConverter
    {
        public override bool CanConvert( Type objectType )
            => objectType == typeof( FullPath );

        public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
        {
            var token = JToken.Load( reader ).ToString();
            return new FullPath( token );
        }

        public override bool CanWrite
            => true;

        public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
        {
            if( value is FullPath p )
            {
                serializer.Serialize( writer, p.ToString() );
            }
        }
    }
}