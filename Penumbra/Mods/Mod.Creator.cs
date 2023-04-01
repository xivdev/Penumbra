using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Import.Structs;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

internal static partial class ModCreator
{
    /// <summary>
    /// Create and return a new directory based on the given directory and name, that is <br/>
    ///    - Not Empty.<br/>
    ///    - Unique, by appending (digit) for duplicates.<br/>
    ///    - Containing no symbols invalid for FFXIV or windows paths.<br/>
    /// </summary>
    /// <param name="outDirectory"></param>
    /// <param name="modListName"></param>
    /// <param name="create"></param>
    /// <returns></returns>
    /// <exception cref="IOException"></exception>
    public static DirectoryInfo CreateModFolder( DirectoryInfo outDirectory, string modListName, bool create = true )
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

        if( create )
        {
            Directory.CreateDirectory( newModFolder );
        }

        return new DirectoryInfo( newModFolder );
    }

    /// <summary>
    /// Create the name for a group or option subfolder based on its parent folder and given name.
    /// subFolderName should never be empty, and the result is unique and contains no invalid symbols.
    /// </summary>
    public static DirectoryInfo? NewSubFolderName( DirectoryInfo parentFolder, string subFolderName )
    {
        var newModFolderBase = NewOptionDirectory( parentFolder, subFolderName );
        var newModFolder     = newModFolderBase.FullName.ObtainUniqueFile();
        return newModFolder.Length == 0 ? null : new DirectoryInfo( newModFolder );
    }

    /// <summary> Create a file for an option group from given data. </summary>
    public static void CreateOptionGroup( DirectoryInfo baseFolder, GroupType type, string name,
        int priority, int index, uint defaultSettings, string desc, IEnumerable< ISubMod > subMods )
    {
        switch( type )
        {
            case GroupType.Multi:
            {
                var group = new MultiModGroup()
                {
                    Name            = name,
                    Description     = desc,
                    Priority        = priority,
                    DefaultSettings = defaultSettings,
                };
                group.PrioritizedOptions.AddRange( subMods.OfType< SubMod >().Select( ( s, idx ) => ( s, idx ) ) );
                Penumbra.SaveService.ImmediateSave(new ModSaveGroup(baseFolder, group, index));
                break;
            }
            case GroupType.Single:
            {
                var group = new SingleModGroup()
                {
                    Name            = name,
                    Description     = desc,
                    Priority        = priority,
                    DefaultSettings = defaultSettings,
                };
                group.OptionData.AddRange( subMods.OfType< SubMod >() );
                Penumbra.SaveService.ImmediateSave(new ModSaveGroup(baseFolder, group, index));
                break;
            }
        }
    }

    /// <summary> Create the data for a given sub mod from its data and the folder it is based on. </summary>
    public static ISubMod CreateSubMod( DirectoryInfo baseFolder, DirectoryInfo optionFolder, OptionList option )
    {
        var list = optionFolder.EnumerateFiles( "*.*", SearchOption.AllDirectories )
            .Select( f => ( Utf8GamePath.FromFile( f, optionFolder, out var gamePath, true ), gamePath, new FullPath( f ) ) )
            .Where( t => t.Item1 );

        var mod = new SubMod( null! ) // Mod is irrelevant here, only used for saving.
        {
            Name        = option.Name,
            Description = option.Description,
        };
        foreach( var (_, gamePath, file) in list )
        {
            mod.FileData.TryAdd( gamePath, file );
        }

        mod.IncorporateMetaChanges( baseFolder, true );
        return mod;
    }

    /// <summary> Create an empty sub mod for single groups with None options. </summary>
    internal static ISubMod CreateEmptySubMod( string name )
        => new SubMod( null! ) // Mod is irrelevant here, only used for saving.
        {
            Name = name,
        };

    /// <summary>
    /// Create the default data file from all unused files that were not handled before
    /// and are used in sub mods.
    /// </summary>
    internal static void CreateDefaultFiles( DirectoryInfo directory )
    {
        var mod = new Mod( directory );
        mod.Reload( Penumbra.ModManager, false, out _ );
        foreach( var file in mod.FindUnusedFiles() )
        {
            if( Utf8GamePath.FromFile( new FileInfo( file.FullName ), directory, out var gamePath, true ) )
                mod._default.FileData.TryAdd( gamePath, file );
        }

        mod._default.IncorporateMetaChanges( directory, true );
        Penumbra.SaveService.ImmediateSave(new ModSaveGroup(mod, -1));
    }

    /// <summary> Return the name of a new valid directory based on the base directory and the given name. </summary>
    public static DirectoryInfo NewOptionDirectory( DirectoryInfo baseDir, string optionName )
        => new(Path.Combine( baseDir.FullName, ReplaceBadXivSymbols( optionName ) ));

    /// <summary> Normalize for nicer names, and remove invalid symbols or invalid paths. </summary>
    public static string ReplaceBadXivSymbols( string s, string replacement = "_" )
    {
        switch( s )
        {
            case ".":  return replacement;
            case "..": return replacement + replacement;
        }

        StringBuilder sb = new(s.Length);
        foreach( var c in s.Normalize( NormalizationForm.FormKC ) )
        {
            if( c.IsInvalidInPath() )
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

    public static void SplitMultiGroups( DirectoryInfo baseDir )
    {
        var mod = new Mod( baseDir );

        var files   = mod.GroupFiles.ToList();
        var idx     = 0;
        var reorder = false;
        foreach( var groupFile in files )
        {
            ++idx;
            try
            {
                if( reorder )
                {
                    var newName = $"{baseDir.FullName}\\group_{idx:D3}{groupFile.Name[ 9.. ]}";
                    Penumbra.Log.Debug( $"Moving {groupFile.Name} to {Path.GetFileName( newName )} due to reordering after multi group split." );
                    groupFile.MoveTo( newName, false );
                }
            }
            catch( Exception ex )
            {
                throw new Exception( "Could not reorder group file after splitting multi group on .pmp import.", ex );
            }

            try
            {
                var json = JObject.Parse( File.ReadAllText( groupFile.FullName ) );
                if( json[ nameof( IModGroup.Type ) ]?.ToObject< GroupType >() is not GroupType.Multi )
                {
                    continue;
                }

                var name = json[ nameof( IModGroup.Name ) ]?.ToObject< string >() ?? string.Empty;
                if( name.Length == 0 )
                {
                    continue;
                }


                var options = json[ "Options" ]?.Children().ToList();
                if( options == null )
                {
                    continue;
                }

                if( options.Count <= IModGroup.MaxMultiOptions )
                {
                    continue;
                }

                Penumbra.Log.Information( $"Splitting multi group {name} in {mod.Name} due to {options.Count} being too many options." );
                var clone = json.DeepClone();
                reorder = true;
                foreach( var o in options.Skip( IModGroup.MaxMultiOptions ) )
                {
                    o.Remove();
                }

                var newOptions = clone[ "Options" ]!.Children().ToList();
                foreach( var o in newOptions.Take( IModGroup.MaxMultiOptions ) )
                {
                    o.Remove();
                }

                var match       = DuplicateNumber().Match( name );
                var startNumber = match.Success ? int.Parse( match.Groups[ 0 ].Value ) : 1;
                name = match.Success ? name[ ..4 ] : name;
                var oldName = $"{name}, Part {startNumber}";
                var oldPath = $"{baseDir.FullName}\\group_{idx:D3}_{oldName.RemoveInvalidPathSymbols().ToLowerInvariant()}.json";
                var newName = $"{name}, Part {startNumber + 1}";
                var newPath = $"{baseDir.FullName}\\group_{++idx:D3}_{newName.RemoveInvalidPathSymbols().ToLowerInvariant()}.json";
                json[ nameof( IModGroup.Name ) ]  = oldName;
                clone[ nameof( IModGroup.Name ) ] = newName;

                clone[ nameof( IModGroup.DefaultSettings ) ] = 0u;

                Penumbra.Log.Debug( $"Writing the first {IModGroup.MaxMultiOptions} options to {Path.GetFileName( oldPath )} after split." );
                using( var oldFile = File.CreateText( oldPath ) )
                {
                    using var j = new JsonTextWriter( oldFile )
                    {
                        Formatting = Formatting.Indented,
                    };
                    json.WriteTo( j );
                }

                Penumbra.Log.Debug( $"Writing the remaining {options.Count - IModGroup.MaxMultiOptions} options to {Path.GetFileName( newPath )} after split." );
                using( var newFile = File.CreateText( newPath ) )
                {
                    using var j = new JsonTextWriter( newFile )
                    {
                        Formatting = Formatting.Indented,
                    };
                    clone.WriteTo( j );
                }

                Penumbra.Log.Debug(
                    $"Deleting the old group file at {groupFile.Name} after splitting it into {Path.GetFileName( oldPath )} and {Path.GetFileName( newPath )}." );
                groupFile.Delete();
            }
            catch( Exception ex )
            {
                throw new Exception( $"Could not split multi group file {groupFile.Name} on .pmp import.", ex );
            }
        }
    }

    [GeneratedRegex(@", Part (\d+)$", RegexOptions.NonBacktracking )]
    private static partial Regex DuplicateNumber();
}