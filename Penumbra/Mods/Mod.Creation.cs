using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Utility;
using OtterGui.Classes;
using OtterGui.Filesystem;
using Penumbra.GameData.ByteString;
using Penumbra.Import;

namespace Penumbra.Mods;

public partial class Mod
{
    // Create and return a new directory based on the given directory and name, that is
    //     - Not Empty
    //     - Unique, by appending (digit) for duplicates.
    //     - Containing no symbols invalid for FFXIV or windows paths.
    internal static DirectoryInfo CreateModFolder( DirectoryInfo outDirectory, string modListName )
    {
        var name = modListName;
        if( name.Length == 0 )
        {
            name = "_";
        }

        var newModFolderBase = NewOptionDirectory( outDirectory, name );
        var newModFolder     = newModFolderBase.FullName.ObtainUniqueFile();
        if( newModFolder.Length == 0 )
        {
            throw new IOException( "Could not create mod folder: too many folders of the same name exist." );
        }

        Directory.CreateDirectory( newModFolder );
        return new DirectoryInfo( newModFolder );
    }

    // Create the name for a group or option subfolder based on its parent folder and given name.
    // subFolderName should never be empty, and the result is unique and contains no invalid symbols.
    internal static DirectoryInfo? NewSubFolderName( DirectoryInfo parentFolder, string subFolderName )
    {
        var newModFolderBase = NewOptionDirectory( parentFolder, subFolderName );
        var newModFolder     = newModFolderBase.FullName.ObtainUniqueFile();
        return newModFolder.Length == 0 ? null : new DirectoryInfo( newModFolder );
    }

    // Create the file containing the meta information about a mod from scratch.
    internal static void CreateMeta( DirectoryInfo directory, string? name, string? author, string? description, string? version,
        string? website )
    {
        var mod = new Mod( directory );
        mod.Name        = name.IsNullOrEmpty() ? mod.Name : new LowerString( name! );
        mod.Author      = author != null ? new LowerString( author ) : mod.Author;
        mod.Description = description ?? mod.Description;
        mod.Version     = version     ?? mod.Version;
        mod.Website     = website     ?? mod.Website;
        mod.SaveMetaFile(); // Not delayed.
    }

    // Create a file for an option group from given data.
    internal static void CreateOptionGroup( DirectoryInfo baseFolder, SelectType type, string name,
        int priority, int index, string desc, IEnumerable< ISubMod > subMods )
    {
        switch( type )
        {
            case SelectType.Multi:
            {
                var group = new MultiModGroup()
                {
                    Name        = name,
                    Description = desc,
                    Priority    = priority,
                };
                group.PrioritizedOptions.AddRange( subMods.OfType< SubMod >().Select( ( s, idx ) => ( s, idx ) ) );
                IModGroup.Save( group, baseFolder, index );
                break;
            }
            case SelectType.Single:
            {
                var group = new SingleModGroup()
                {
                    Name        = name,
                    Description = desc,
                    Priority    = priority,
                };
                group.OptionData.AddRange( subMods.OfType< SubMod >() );
                IModGroup.Save( group, baseFolder, index );
                break;
            }
        }
    }

    // Create the data for a given sub mod from its data and the folder it is based on.
    internal static ISubMod CreateSubMod( DirectoryInfo baseFolder, DirectoryInfo optionFolder, OptionList option )
    {
        var list = optionFolder.EnumerateFiles( "*.*", SearchOption.AllDirectories )
           .Select( f => ( Utf8GamePath.FromFile( f, optionFolder, out var gamePath, true ), gamePath, new FullPath( f ) ) )
           .Where( t => t.Item1 );

        var mod = new SubMod(null!) // Mod is irrelevant here, only used for saving.
        {
            Name = option.Name,
        };
        foreach( var (_, gamePath, file) in list )
        {
            mod.FileData.TryAdd( gamePath, file );
        }

        mod.IncorporateMetaChanges( baseFolder, true );
        return mod;
    }

    // Create an empty sub mod for single groups with None options.
    internal static ISubMod CreateEmptySubMod( string name )
        => new SubMod(null! ) // Mod is irrelevant here, only used for saving.
        {
            Name = name,
        };

    // Create the default data file from all unused files that were not handled before
    // and are used in sub mods.
    internal static void CreateDefaultFiles( DirectoryInfo directory )
    {
        var mod = new Mod( directory );
        mod.Reload( out _ );
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

    // Return the name of a new valid directory based on the base directory and the given name.
    private static DirectoryInfo NewOptionDirectory( DirectoryInfo baseDir, string optionName )
        => new(Path.Combine( baseDir.FullName, ReplaceBadXivSymbols( optionName ) ));


    // XIV can not deal with non-ascii symbols in a path,
    // and the path must obviously be valid itself.
    public static string ReplaceBadXivSymbols( string s, string replacement = "_" )
    {
        StringBuilder sb = new(s.Length);
        foreach( var c in s )
        {
            if( c.IsInvalidAscii() || c.IsInvalidInPath() )
            {
                sb.Append( replacement );
            }
            else
            {
                sb.Append( c );
            }
        }

        return sb.ToString();
    }
}