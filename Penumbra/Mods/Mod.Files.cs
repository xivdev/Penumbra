using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public partial class Mod
{
    public ISubMod Default
        => _default;

    public IReadOnlyList< IModGroup > Groups
        => _groups;

    private readonly SubMod            _default;
    private readonly List< IModGroup > _groups  = new();

    public int TotalFileCount { get; private set; }
    public int TotalSwapCount { get; private set; }
    public int TotalManipulations { get; private set; }
    public bool HasOptions { get; private set; }

    private bool SetCounts()
    {
        TotalFileCount     = 0;
        TotalSwapCount     = 0;
        TotalManipulations = 0;
        foreach( var s in AllSubMods )
        {
            TotalFileCount     += s.Files.Count;
            TotalSwapCount     += s.FileSwaps.Count;
            TotalManipulations += s.Manipulations.Count;
        }

        HasOptions = _groups.Any( o
            => o is MultiModGroup m && m.PrioritizedOptions.Count > 0
         || o is SingleModGroup s   && s.OptionData.Count         > 1 );
        return true;
    }

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
        => ModPath.EnumerateFiles( "group_*.json" );

    public List< FullPath > FindUnusedFiles()
    {
        var modFiles = AllFiles.ToHashSet();
        return ModPath.EnumerateDirectories()
           .SelectMany( f => f.EnumerateFiles( "*", SearchOption.AllDirectories ) )
           .Select( f => new FullPath( f ) )
           .Where( f => !modFiles.Contains( f ) )
           .ToList();
    }

    private static IModGroup? LoadModGroup( Mod mod, FileInfo file, int groupIdx )
    {
        if( !File.Exists( file.FullName ) )
        {
            return null;
        }

        try
        {
            var json = JObject.Parse( File.ReadAllText( file.FullName ) );
            switch( json[ nameof( Type ) ]?.ToObject< GroupType >() ?? GroupType.Single )
            {
                case GroupType.Multi:  return MultiModGroup.Load( mod, json, groupIdx );
                case GroupType.Single: return SingleModGroup.Load( mod, json, groupIdx );
            }
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not read mod group from {file.FullName}:\n{e}" );
        }

        return null;
    }

    private void LoadAllGroups()
    {
        _groups.Clear();
        var changes = false;
        foreach( var file in GroupFiles )
        {
            var group = LoadModGroup( this, file, _groups.Count );
            if( group != null && _groups.All( g => g.Name != group.Name ) )
            {
                changes = changes || group.FileName( ModPath, _groups.Count ) != file.FullName;
                _groups.Add( group );
            }
            else
            {
                changes = true;
            }
        }

        if( changes )
        {
            SaveAllGroups();
        }
    }

    // Delete all existing group files and save them anew.
    // Used when indices change in complex ways.
    private void SaveAllGroups()
    {
        foreach( var file in GroupFiles )
        {
            try
            {
                if( file.Exists )
                {
                    file.Delete();
                }
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not delete outdated group file {file}:\n{e}" );
            }
        }

        foreach( var (group, index) in _groups.WithIndex() )
        {
            IModGroup.Save( group, ModPath, index );
        }
    }
}