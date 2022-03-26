using System;
using System.IO;
using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Penumbra.GameData.ByteString;

[JsonConverter( typeof( Utf8RelPathConverter ) )]
public readonly struct Utf8RelPath : IEquatable< Utf8RelPath >, IComparable< Utf8RelPath >, IDisposable
{
    public const int MaxRelPathLength = 250;

    public readonly        Utf8String  Path;
    public static readonly Utf8RelPath Empty = new(Utf8String.Empty);

    internal Utf8RelPath( Utf8String path )
        => Path = path;


    public static explicit operator Utf8RelPath( string s )
    {
        if( !FromString( s, out var p ) )
        {
            return Empty;
        }

        return new Utf8RelPath( p.Path.AsciiToLower() );
    }

    public static bool FromString( string? s, out Utf8RelPath path )
    {
        path = Empty;
        if( s.IsNullOrEmpty() )
        {
            return true;
        }

        var substring = s!.Replace( '/', '\\' ).TrimStart('\\');
        if( substring.Length > MaxRelPathLength )
        {
            return false;
        }

        if( substring.Length == 0 )
        {
            return true;
        }

        if( !Utf8String.FromString( substring, out var ascii, true ) || !ascii.IsAscii )
        {
            return false;
        }

        path = new Utf8RelPath( ascii );
        return true;
    }

    public static bool FromFile( FileInfo file, DirectoryInfo baseDir, out Utf8RelPath path )
    {
        path = Empty;
        if( !file.FullName.StartsWith( baseDir.FullName ) )
        {
            return false;
        }

        var substring = file.FullName[ (baseDir.FullName.Length + 1).. ];
        return FromString( substring, out path );
    }

    public static bool FromFile( FullPath file, DirectoryInfo baseDir, out Utf8RelPath path )
    {
        path = Empty;
        if( !file.FullName.StartsWith( baseDir.FullName ) )
        {
            return false;
        }

        var substring = file.FullName[ (baseDir.FullName.Length + 1).. ];
        return FromString( substring, out path );
    }

    public Utf8RelPath( Utf8GamePath gamePath )
        => Path = gamePath.Path.Replace( ( byte )'/', ( byte )'\\' );

    public unsafe Utf8GamePath ToGamePath( int skipFolders = 0 )
    {
        var idx = 0;
        while( skipFolders > 0 )
        {
            idx = Path.IndexOf( ( byte )'\\', idx ) + 1;
            --skipFolders;
            if( idx <= 0 )
            {
                return Utf8GamePath.Empty;
            }
        }

        var length = Path.Length - idx;
        var ptr    = ByteStringFunctions.CopyString( Path.Path + idx, length );
        ByteStringFunctions.Replace( ptr, length, ( byte )'\\', ( byte )'/' );
        ByteStringFunctions.AsciiToLowerInPlace( ptr, length );
        var utf = new Utf8String().Setup( ptr, length, null, true, true, true, true );
        return new Utf8GamePath( utf );
    }

    public int CompareTo( Utf8RelPath rhs )
        => Path.CompareTo( rhs.Path );

    public bool Equals( Utf8RelPath other )
        => Path.Equals( other.Path );

    public override string ToString()
        => Path.ToString();

    public void Dispose()
        => Path.Dispose();

    public class Utf8RelPathConverter : JsonConverter
    {
        public override bool CanConvert( Type objectType )
            => objectType == typeof( Utf8RelPath );

        public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
        {
            var token = JToken.Load( reader ).ToString();
            return FromString( token, out var p )
                ? p
                : throw new JsonException( $"Could not convert \"{token}\" to {nameof( Utf8RelPath )}." );
        }

        public override bool CanWrite
            => true;

        public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
        {
            if( value is Utf8RelPath p )
            {
                serializer.Serialize( writer, p.ToString() );
            }
        }
    }
}