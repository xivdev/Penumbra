using System;
using System.IO;
using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Penumbra.GameData.ByteString;

// NewGamePath wrap some additional validity checking around Utf8String,
// provide some filesystem helpers, and conversion to Json.
[JsonConverter( typeof( NewGamePathConverter ) )]
public readonly struct NewGamePath : IEquatable< NewGamePath >, IComparable< NewGamePath >, IDisposable
{
    public const int MaxGamePathLength = 256;

    public readonly        Utf8String  Path;
    public static readonly NewGamePath Empty = new(Utf8String.Empty);

    internal NewGamePath( Utf8String s )
        => Path = s;

    public int Length
        => Path.Length;

    public bool IsEmpty
        => Path.IsEmpty;

    public NewGamePath ToLower()
        => new(Path.AsciiToLower());

    public static unsafe bool FromPointer( byte* ptr, out NewGamePath path, bool lower = false )
    {
        var utf = new Utf8String( ptr );
        return ReturnChecked( utf, out path, lower );
    }

    public static bool FromSpan( ReadOnlySpan< byte > data, out NewGamePath path, bool lower = false )
    {
        var utf = Utf8String.FromSpanUnsafe( data, false, null, null );
        return ReturnChecked( utf, out path, lower );
    }

    // Does not check for Forward/Backslashes due to assuming that SE-strings use the correct one.
    // Does not check for initial slashes either, since they are assumed to be by choice.
    // Checks for maxlength, ASCII and lowercase.
    private static bool ReturnChecked( Utf8String utf, out NewGamePath path, bool lower = false )
    {
        path = Empty;
        if( !utf.IsAscii || utf.Length > MaxGamePathLength )
        {
            return false;
        }

        path = new NewGamePath( lower ? utf.AsciiToLower() : utf );
        return true;
    }

    public NewGamePath Clone()
        => new(Path.Clone());

    public static bool FromString( string? s, out NewGamePath path, bool toLower = false )
    {
        path = Empty;
        if( s.IsNullOrEmpty() )
        {
            return true;
        }

        var substring = s!.Replace( '\\', '/' );
        substring.TrimStart( '/' );
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

        path = new NewGamePath( ascii );
        return true;
    }

    public static bool FromFile( FileInfo file, DirectoryInfo baseDir, out NewGamePath path, bool toLower = false )
    {
        path = Empty;
        if( !file.FullName.StartsWith( baseDir.FullName ) )
        {
            return false;
        }

        var substring = file.FullName[ baseDir.FullName.Length.. ];
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

    public bool Equals( NewGamePath other )
        => Path.Equals( other.Path );

    public override int GetHashCode()
        => Path.GetHashCode();

    public int CompareTo( NewGamePath other )
        => Path.CompareTo( other.Path );

    public override string ToString()
        => Path.ToString();

    public void Dispose()
        => Path.Dispose();

    public bool IsRooted()
        => Path.Length >= 1 && ( Path[ 0 ] == '/' || Path[ 0 ] == '\\' )
         || Path.Length >= 2
         && ( Path[ 0 ] >= 'A' && Path[ 0 ] <= 'Z' || Path[ 0 ] >= 'a' && Path[ 0 ] <= 'z' )
         && Path[ 1 ] == ':';

    private class NewGamePathConverter : JsonConverter
    {
        public override bool CanConvert( Type objectType )
            => objectType == typeof( NewGamePath );

        public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
        {
            var token = JToken.Load( reader ).ToString();
            return FromString( token, out var p, true )
                ? p
                : throw new JsonException( $"Could not convert \"{token}\" to {nameof( NewGamePath )}." );
        }

        public override bool CanWrite
            => true;

        public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
        {
            if( value is NewGamePath p )
            {
                serializer.Serialize( writer, p.ToString() );
            }
        }
    }
}