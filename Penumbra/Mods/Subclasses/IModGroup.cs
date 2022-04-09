using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Logging;
using Newtonsoft.Json;
using Penumbra.Util;

namespace Penumbra.Mods;

public interface IModGroup : IEnumerable< ISubMod >
{
    public string Name { get; }
    public string Description { get; }
    public SelectType Type { get; }
    public int Priority { get; }

    public int OptionPriority( Index optionIdx );

    public ISubMod this[ Index idx ] { get; }

    public int Count { get; }

    public bool IsOption
        => Type switch
        {
            SelectType.Single => Count > 1,
            SelectType.Multi  => Count > 0,
            _                 => false,
        };

    public string FileName( DirectoryInfo basePath )
        => Path.Combine( basePath.FullName, $"group_{Name.RemoveInvalidPathSymbols().ToLowerInvariant()}.json" );

    public void DeleteFile( DirectoryInfo basePath )
    {
        var file = FileName( basePath );
        if( !File.Exists( file ) )
        {
            return;
        }

        try
        {
            File.Delete( file );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not delete file {file}:\n{e}" );
            throw;
        }
    }

    public static void SaveModGroup( IModGroup group, DirectoryInfo basePath )
    {
        var       file       = group.FileName( basePath );
        using var s          = File.Exists( file ) ? File.Open( file, FileMode.Truncate ) : File.Open( file, FileMode.CreateNew );
        using var writer     = new StreamWriter( s );
        using var j          = new JsonTextWriter( writer ) { Formatting = Formatting.Indented };
        var       serializer = new JsonSerializer { Formatting           = Formatting.Indented };
        j.WriteStartObject();
        j.WritePropertyName( nameof( group.Name ) );
        j.WriteValue( group.Name );
        j.WritePropertyName( nameof( group.Description ) );
        j.WriteValue( group.Description );
        j.WritePropertyName( nameof( group.Priority ) );
        j.WriteValue( group.Priority );
        j.WritePropertyName( nameof( Type ) );
        j.WriteValue( group.Type.ToString() );
        j.WritePropertyName( "Options" );
        j.WriteStartArray();
        for( var idx = 0; idx < group.Count; ++idx )
        {
            ISubMod.WriteSubMod( j, serializer, group[ idx ], basePath, group.Type == SelectType.Multi ? group.OptionPriority( idx ) : null );
        }

        j.WriteEndArray();
        j.WriteEndObject();
    }
}