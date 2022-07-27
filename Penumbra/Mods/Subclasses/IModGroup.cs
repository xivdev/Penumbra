using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Logging;
using Newtonsoft.Json;
using OtterGui.Filesystem;

namespace Penumbra.Mods;

public enum SelectType
{
    Single,
    Multi,
}

public interface IModGroup : IEnumerable< ISubMod >
{
    public const int MaxMultiOptions = 32;

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
            PluginLog.Debug( "Deleted group file {File:l} for group {GroupIdx}: {GroupName:l}.", file, groupIdx + 1, Name );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not delete file {file}:\n{e}" );
            throw;
        }
    }

    public static void SaveDelayed( IModGroup group, DirectoryInfo basePath, int groupIdx )
    {
        Penumbra.Framework.RegisterDelayed( $"{nameof( SaveModGroup )}_{basePath.Name}_{group.Name}", () => SaveModGroup( group, basePath, groupIdx ) );
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
        j.WritePropertyName( "Options" );
        j.WriteStartArray();
        for( var idx = 0; idx < group.Count; ++idx )
        {
            ISubMod.WriteSubMod( j, serializer, group[ idx ], basePath, group.Type == SelectType.Multi ? group.OptionPriority( idx ) : null );
        }

        j.WriteEndArray();
        j.WriteEndObject();
        PluginLog.Debug( "Saved group file {File:l} for group {GroupIdx}: {GroupName:l}.", file, groupIdx + 1, group.Name );
    }

    public IModGroup Convert( SelectType type );
    public bool      MoveOption( int optionIdxFrom, int optionIdxTo );
    public void      UpdatePositions(int from = 0);
}