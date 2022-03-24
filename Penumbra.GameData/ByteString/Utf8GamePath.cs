using System;
using System.IO;
using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Util;

namespace Penumbra.GameData.ByteString;

// NewGamePath wrap some additional validity checking around Utf8String,
// provide some filesystem helpers, and conversion to Json.
[JsonConverter( typeof( Utf8GamePathConverter ) )]
public readonly struct Utf8GamePath : IEquatable< Utf8GamePath >, IComparable< Utf8GamePath >, IDisposable
{
    public const int MaxGamePathLength = 256;

    public readonly        Utf8String   Path;
    public static readonly Utf8GamePath Empty = new(Utf8String.Empty);

    internal Utf8GamePath( Utf8String s )
        => Path = s;

    public int Length
        => Path.Length;

    public bool IsEmpty
        => Path.IsEmpty;

    public Utf8GamePath ToLower()
        => new(Path.AsciiToLower());

    public static unsafe bool FromPointer( byte* ptr, out Utf8GamePath path, bool lower = false )
    {
        var utf = new Utf8String( ptr );
        return ReturnChecked( utf, out path, lower );
    }

    public static bool FromSpan( ReadOnlySpan< byte > data, out Utf8GamePath path, bool lower = false )
    {
        var utf = Utf8String.FromSpanUnsafe( data, false, null, null );
        return ReturnChecked( utf, out path, lower );
    }

    // Does not check for Forward/Backslashes due to assuming that SE-strings use the correct one.
    // Does not check for initial slashes either, since they are assumed to be by choice.
    // Checks for maxlength, ASCII and lowercase.
    private static bool ReturnChecked( Utf8String utf, out Utf8GamePath path, bool lower = false )
    {
        path = Empty;
        if( !utf.IsAscii || utf.Length > MaxGamePathLength )
        {
            return false;
        }

        path = new Utf8GamePath( lower ? utf.AsciiToLower() : utf );
        return true;
    }

    public Utf8GamePath Clone()
        => new(Path.Clone());

    public static explicit operator Utf8GamePath( string s )
        => FromString( s, out var p, true ) ? p : Empty;

    public static bool FromString( string? s, out Utf8GamePath path, bool toLower = false )
    {
        path = Empty;
        if( s.IsNullOrEmpty() )
        {
            return true;
        }

        var substring = s!.Replace( '\\', '/' ).TrimStart( '/' );
        if( substring.Length > MaxGamePathLength )
        {
            return false;
        }

        if( substring.Length == 0 )
        {
            return true;
        }

        if( !Utf8String.FromString( substring, out var ascii, toLower ) || !ascii.IsAscii )
        {
            return false;
        }

        path = new Utf8GamePath( ascii );
        return true;
    }

    public static bool FromFile( FileInfo file, DirectoryInfo baseDir, out Utf8GamePath path, bool toLower = false )
    {
        path = Empty;
        if( !file.FullName.StartsWith( baseDir.FullName ) )
        {
            return false;
        }

        var substring = file.FullName[ ( baseDir.FullName.Length + 1 ).. ];
        return FromString( substring, out path, toLower );
    }

    public Utf8String Filename()
    {
        var idx = Path.LastIndexOf( ( byte )'/' );
        return idx == -1 ? Path : Path.Substring( idx + 1 );
    }

    public Utf8String Extension()
    {
        var idx = Path.LastIndexOf( ( byte )'.' );
        return idx == -1 ? Utf8String.Empty : Path.Substring( idx );
    }

    public bool Equals( Utf8GamePath other )
        => Path.Equals( other.Path );

    public override int GetHashCode()
        => Path.GetHashCode();

    public int CompareTo( Utf8GamePath other )
        => Path.CompareTo( other.Path );

    public override string ToString()
        => Path.ToString();

    public void Dispose()
        => Path.Dispose();

    public bool IsRooted()
        => IsRooted( Path );

    public static bool IsRooted( Utf8String path )
        => path.Length >= 1 && ( path[ 0 ] == '/' || path[ 0 ] == '\\' )
         || path.Length >= 2
         && ( path[ 0 ] >= 'A' && path[ 0 ] <= 'Z' || path[ 0 ] >= 'a' && path[ 0 ] <= 'z' )
         && path[ 1 ] == ':';

    public class Utf8GamePathConverter : JsonConverter
    {
        public override bool CanConvert( Type objectType )
            => objectType == typeof( Utf8GamePath );

        public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
        {
            var token = JToken.Load( reader ).ToString();
            return FromString( token, out var p, true )
                ? p
                : throw new JsonException( $"Could not convert \"{token}\" to {nameof( Utf8GamePath )}." );
        }

        public override bool CanWrite
            => true;

        public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
        {
            if( value is Utf8GamePath p )
            {
                serializer.Serialize( writer, p.ToString() );
            }
        }
    }

    public GamePath ToGamePath()
        => GamePath.GenerateUnchecked( ToString() );
}