using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
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

    internal readonly SubMod            _default;
    internal readonly List< IModGroup > _groups  = new();

    public int TotalFileCount { get; internal set; }
    public int TotalSwapCount { get; internal set; }
    public int TotalManipulations { get; internal set; }
    public bool HasOptions { get; internal set; }

    internal bool SetCounts()
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
                changes = changes || Penumbra.Filenames.OptionGroupFile(ModPath.FullName, Groups.Count, group.Name) != file.FullName;
                _groups.Add( group );
            }
            else
            {
                changes = true;
            }
        }

        if( changes )
            Penumbra.SaveService.SaveAllOptionGroups(this);
    }

    private void LoadDefaultOption()
    {
        var defaultFile = Penumbra.Filenames.OptionGroupFile(this, -1);
        _default.SetPosition(-1, 0);
        try
        {
            if (!File.Exists(defaultFile))
            {
                _default.Load(ModPath, new JObject(), out _);
            }
            else
            {
                _default.Load(ModPath, JObject.Parse(File.ReadAllText(defaultFile)), out _);
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not parse default file for {Name}:\n{e}");
        }
    }

    public void WriteAllTexToolsMeta()
    {
        try
        {
            _default.WriteTexToolsMeta(ModPath);
            foreach (var group in Groups)
            {
                var dir = Creator.NewOptionDirectory(ModPath, group.Name);
                if (!dir.Exists)
                {
                    dir.Create();
                }

                foreach (var option in group.OfType<SubMod>())
                {
                    var optionDir = Creator.NewOptionDirectory(dir, option.Name);
                    if (!optionDir.Exists)
                    {
                        optionDir.Create();
                    }

                    option.WriteTexToolsMeta(optionDir);
                }
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error writing TexToolsMeta:\n{e}");
        }
    }

}