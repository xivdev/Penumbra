using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;

namespace Penumbra.Mods;

public interface IModGroup : IEnumerable< ISubMod >
{
    public const int MaxMultiOptions = 32;

    public string Name { get; }
    public string Description { get; }
    public GroupType Type { get; }
    public int Priority { get; }
    public uint DefaultSettings { get; set; }

    public int OptionPriority( Index optionIdx );

    public ISubMod this[ Index idx ] { get; }

    public int Count { get; }

    public bool IsOption
        => Type switch
        {
            GroupType.Single => Count > 1,
            GroupType.Multi  => Count > 0,
            _                 => false,
        };

    public string FileName( DirectoryInfo basePath, int groupIdx )
        => Path.Combine( basePath.FullName, $"group_{groupIdx + 1:D3}_{Name.RemoveInvalidPathSymbols().ToLowerInvariant()}.json" );

    public void DeleteFile( DirectoryInfo basePath, int groupIdx )
    {
        var file = FileName( basePath, groupIdx );
        if( !File.Exists( file ) )
        {
            return;
        }

        try
        {
            File.Delete( file );
            Penumbra.Log.Debug( $"Deleted group file {file} for group {groupIdx + 1}: {Name}." );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not delete file {file}:\n{e}" );
            throw;
        }
    }

    public static void SaveDelayed( IModGroup group, DirectoryInfo basePath, int groupIdx )
    {
        Penumbra.Framework.RegisterDelayed( $"{nameof( SaveModGroup )}_{basePath.Name}_{group.Name}",
            () => SaveModGroup( group, basePath, groupIdx ) );
    }

    public static void Save( IModGroup group, DirectoryInfo basePath, int groupIdx )
        => SaveModGroup( group, basePath, groupIdx );

    private static void SaveModGroup( IModGroup group, DirectoryInfo basePath, int groupIdx )
    {
        var       file       = group.FileName( basePath, groupIdx );
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
        j.WritePropertyName( nameof( group.DefaultSettings ) );
        j.WriteValue( group.DefaultSettings );
        j.WritePropertyName( "Options" );
        j.WriteStartArray();
        for( var idx = 0; idx < group.Count; ++idx )
        {
            ISubMod.WriteSubMod( j, serializer, group[ idx ], basePath, group.Type == GroupType.Multi ? group.OptionPriority( idx ) : null );
        }

        j.WriteEndArray();
        j.WriteEndObject();
        Penumbra.Log.Debug( $"Saved group file {file} for group {groupIdx + 1}: {group.Name}." );
    }

    public IModGroup Convert( GroupType type );
    public bool      MoveOption( int optionIdxFrom, int optionIdxTo );
    public void      UpdatePositions( int from = 0 );
}