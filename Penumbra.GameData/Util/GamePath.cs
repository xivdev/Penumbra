using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Penumbra.GameData.Util;

public readonly struct GamePath : IComparable
{
    public const int MaxGamePathLength = 256;

    private readonly string _path;

    private GamePath( string path, bool _ )
        => _path = path;

    public GamePath( string? path )
    {
        if( path is { Length: < MaxGamePathLength } )
        {
            _path = Lower( Trim( ReplaceSlash( path ) ) );
        }
        else
        {
            _path = string.Empty;
        }
    }

    public GamePath( FileInfo file, DirectoryInfo baseDir )
        => _path = CheckPre( file, baseDir ) ? Lower( Trim( ReplaceSlash( Substring( file, baseDir ) ) ) ) : "";

    private static bool CheckPre( FileInfo file, DirectoryInfo baseDir )
        => file.FullName.StartsWith( baseDir.FullName ) && file.FullName.Length < MaxGamePathLength;

    private static string Substring( FileInfo file, DirectoryInfo baseDir )
        => file.FullName.Substring( baseDir.FullName.Length );

    private static string ReplaceSlash( string path )
        => path.Replace( '\\', '/' );

    private static string Trim( string path )
        => path.TrimStart( '/' );

    private static string Lower( string path )
        => path.ToLowerInvariant();

    public static GamePath GenerateUnchecked( string path )
        => new(path, true);

    public static GamePath GenerateUncheckedLower( string path )
        => new(Lower( path ), true);

    public static implicit operator string( GamePath gamePath )
        => gamePath._path;

    public static explicit operator GamePath( string gamePath )
        => new(gamePath);

    public bool Empty
        => _path.Length == 0;

    public string Filename()
    {
        var idx = _path.LastIndexOf( "/", StringComparison.Ordinal );
        return idx == -1 ? _path : idx == _path.Length - 1 ? "" : _path[ ( idx + 1 ).. ];
    }

    public int CompareTo( object? rhs )
    {
        return rhs switch
        {
            string path   => string.Compare( _path, path, StringComparison.Ordinal ),
            GamePath path => string.Compare( _path, path._path, StringComparison.Ordinal ),
            _             => -1,
        };
    }

    public override string ToString()
        => _path;
}

public class GamePathConverter : JsonConverter
{
    public override bool CanConvert( Type objectType )
        => objectType == typeof( GamePath );

    public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
    {
        var token = JToken.Load( reader );
        return token.ToObject< GamePath >();
    }

    public override bool CanWrite
        => true;

    public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
    {
        if( value != null )
        {
            var v = ( GamePath )value;
            serializer.Serialize( writer, v.ToString() );
        }
    }
}

