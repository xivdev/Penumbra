using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Mods;

public partial class Mod2
{
    public ISubMod Default
        => _default;

    public IReadOnlyList< IModGroup > Groups
        => _groups;

    public bool HasOptions { get; private set; }

    private void SetHasOptions()
    {
        HasOptions = _groups.Any( o
            => o is MultiModGroup m && m.PrioritizedOptions.Count > 0
         || o is SingleModGroup s   && s.OptionData.Count         > 1 );
    }


    private readonly SubMod            _default = new();
    private readonly List< IModGroup > _groups  = new();

    public IEnumerable< ISubMod > AllSubMods
        => _groups.SelectMany( o => o ).Prepend( _default );

    public IEnumerable< MetaManipulation > AllManipulations
        => AllSubMods.SelectMany( s => s.Manipulations );

    public IEnumerable< Utf8GamePath > AllRedirects
        => AllSubMods.SelectMany( s => s.Files.Keys.Concat( s.FileSwaps.Keys ) );

    public IEnumerable< FullPath > AllFiles
        => AllSubMods.SelectMany( o => o.Files )
           .Select( p => p.Value );

    public IEnumerable< FileInfo > GroupFiles
        => BasePath.EnumerateFiles( "group_*.json" );

    public List< FullPath > FindUnusedFiles()
    {
        var modFiles = AllFiles.ToHashSet();
        return BasePath.EnumerateDirectories()
           .SelectMany( f => f.EnumerateFiles( "*", SearchOption.AllDirectories ) )
           .Select( f => new FullPath( f ) )
           .Where( f => !modFiles.Contains( f ) )
           .ToList();
    }

    public List< FullPath > FindMissingFiles()
        => AllFiles.Where( f => !f.Exists ).ToList();

    public static IModGroup? LoadModGroup( FileInfo file, DirectoryInfo basePath )
    {
        if( !File.Exists( file.FullName ) )
        {
            return null;
        }

        try
        {
            var json = JObject.Parse( File.ReadAllText( file.FullName ) );
            switch( json[ nameof( Type ) ]?.ToObject< SelectType >() ?? SelectType.Single )
            {
                case SelectType.Multi:  return MultiModGroup.Load( json, basePath );
                case SelectType.Single: return SingleModGroup.Load( json, basePath );
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not read mod group from {file.FullName}:\n{e}" );
        }

        return null;
    }

    private void LoadAllGroups()
    {
        _groups.Clear();
        foreach( var file in GroupFiles )
        {
            var group = LoadModGroup( file, BasePath );
            if( group != null )
            {
                _groups.Add( group );
            }
        }
    }
}