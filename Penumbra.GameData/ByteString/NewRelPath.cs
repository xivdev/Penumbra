using System;
using System.IO;
using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Penumbra.GameData.ByteString;

[JsonConverter( typeof( NewRelPathConverter ) )]
public readonly struct NewRelPath : IEquatable< NewRelPath >, IComparable< NewRelPath >, IDisposable
{
    public const int MaxRelPathLength = 250;

    public readonly        Utf8String Path;
    public static readonly NewRelPath Empty = new(Utf8String.Empty);

    internal NewRelPath( Utf8String path )
        => Path = path;

    public static bool FromString( string? s, out NewRelPath path )
    {
        path = Empty;
        if( s.IsNullOrEmpty() )
        {
            return true;
        }

        var substring = s!.Replace( '/', '\\' );
        substring.TrimStart( '\\' );
        if( substring.Length > MaxRelPathLength )
        {
            return false;
        }

        if( substring.Length == 0 )
        {
            return true;
        }

        if( !Utf8String.FromString( substring, out var ascii ) || !ascii.IsAscii )
        {
            return false;
        }

        path = new NewRelPath( ascii );
        return true;
    }

    public static bool FromFile( FileInfo file, DirectoryInfo baseDir, out NewRelPath path )
    {
        path = Empty;
        if( !file.FullName.StartsWith( baseDir.FullName ) )
        {
            return false;
        }

        var substring = file.FullName[ baseDir.FullName.Length.. ];
        return FromString( substring, out path );
    }

    public static bool FromFile( FullPath file, DirectoryInfo baseDir, out NewRelPath path )
    {
        path = Empty;
        if( !file.FullName.StartsWith( baseDir.FullName ) )
        {
            return false;
        }

        var substring = file.FullName[ baseDir.FullName.Length.. ];
        return FromString( substring, out path );
    }

    public NewRelPath( NewGamePath gamePath )
        => Path = gamePath.Path.Replace( ( byte )'/', ( byte )'\\' );

    public unsafe NewGamePath ToGamePath( int skipFolders = 0 )
    {
        var idx = 0;
        while( skipFolders > 0 )
        {
            idx = Path.IndexOf( ( byte )'\\', idx ) + 1;
            --skipFolders;
            if( idx <= 0 )
            {
                return NewGamePath.Empty;
            }
        }

        var length = Path.Length - idx;
        var ptr    = ByteStringFunctions.CopyString( Path.Path + idx, length );
        ByteStringFunctions.Replace( ptr, length, ( byte )'\\', ( byte )'/' );
        ByteStringFunctions.AsciiToLowerInPlace( ptr, length );
        var utf = new Utf8String().Setup( ptr, length, null, true, true, true, true );
        return new NewGamePath( utf );
    }

    public int CompareTo( NewRelPath rhs )
        => Path.CompareTo( rhs.Path );

    public bool Equals( NewRelPath other )
        => Path.Equals( other.Path );

    public override string ToString()
        => Path.ToString();

    public void Dispose()
        => Path.Dispose();

    private class NewRelPathConverter : JsonConverter
    {
        public override bool CanConvert( Type objectType )
            => objectType == typeof( NewRelPath );

        public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
        {
            var token = JToken.Load( reader ).ToString();
            return FromString( token, out var p )
                ? p
                : throw new JsonException( $"Could not convert \"{token}\" to {nameof( NewRelPath )}." );
        }

        public override bool CanWrite
            => true;

        public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
        {
            if( value is NewRelPath p )
            {
                serializer.Serialize( writer, p.ToString() );
            }
        }
    }
}