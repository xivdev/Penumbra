using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Importer.Models;

namespace Penumbra.Mods;

public partial class Mod2
{
    internal static void CreateMeta( DirectoryInfo directory, string? name, string? author, string? description, string? version,
        string? website )
    {
        var mod = new Mod2( directory );
        if( name is { Length: 0 } )
        {
            mod.Name = name;
        }

        if( author != null )
        {
            mod.Author = author;
        }

        if( description != null )
        {
            mod.Description = description;
        }

        if( version != null )
        {
            mod.Version = version;
        }

        if( website != null )
        {
            mod.Website = website;
        }

        mod.SaveMeta();
    }

    internal static void CreateOptionGroup( DirectoryInfo baseFolder, ModGroup groupData,
        int priority, string desc, List< ISubMod > subMods )
    {
        switch( groupData.SelectionType )
        {
            case SelectType.Multi:
            {
                var group = new MultiModGroup()
                {
                    Name        = groupData.GroupName!,
                    Description = desc,
                    Priority    = priority,
                };
                group.PrioritizedOptions.AddRange( subMods.OfType< SubMod >().Select( ( s, idx ) => ( s, idx ) ) );
                IModGroup.SaveModGroup( group, baseFolder );
                break;
            }
            case SelectType.Single:
            {
                var group = new SingleModGroup()
                {
                    Name        = groupData.GroupName!,
                    Description = desc,
                    Priority    = priority,
                };
                group.OptionData.AddRange( subMods.OfType< SubMod >() );
                IModGroup.SaveModGroup( group, baseFolder );
                break;
            }
        }
    }

    internal static ISubMod CreateSubMod( DirectoryInfo baseFolder, DirectoryInfo optionFolder, OptionList option )
    {
        var list = optionFolder.EnumerateFiles( "*.*", SearchOption.AllDirectories )
           .Select( f => ( Utf8GamePath.FromFile( f, optionFolder, out var gamePath, true ), gamePath, new FullPath( f ) ) )
           .Where( t => t.Item1 );

        var mod = new SubMod()
        {
            Name = option.Name!,
        };
        foreach( var (_, gamePath, file) in list )
        {
            mod.FileData.TryAdd( gamePath, file );
        }

        mod.IncorporateMetaChanges( baseFolder, true );
        return mod;
    }

    internal static void CreateDefaultFiles( DirectoryInfo directory )
    {
        var mod = new Mod2( directory );
        foreach( var file in mod.FindUnusedFiles() )
        {
            if( Utf8GamePath.FromFile( new FileInfo( file.FullName ), directory, out var gamePath, true ) )
            {
                mod._default.FileData.TryAdd( gamePath, file );
            }
        }

        mod._default.IncorporateMetaChanges( directory, true );
        mod.SaveDefaultMod();
    }
}