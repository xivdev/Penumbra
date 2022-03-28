using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Importer;
using Penumbra.Util;

namespace Penumbra.Mods;

public class ModCleanup
{
    private const string Duplicates = "Duplicates";
    private const string Required   = "Required";

    private readonly DirectoryInfo _baseDir;
    private readonly ModMeta       _mod;
    private          SHA256?       _hasher;

    private readonly Dictionary< long, List< FileInfo > > _filesBySize = new();

    private SHA256 Sha()
    {
        _hasher ??= SHA256.Create();
        return _hasher;
    }

    private ModCleanup( DirectoryInfo baseDir, ModMeta mod )
    {
        _baseDir = baseDir;
        _mod     = mod;
        BuildDict();
    }

    private void BuildDict()
    {
        foreach( var file in _baseDir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
        {
            var fileLength = file.Length;
            if( _filesBySize.TryGetValue( fileLength, out var files ) )
            {
                files.Add( file );
            }
            else
            {
                _filesBySize[ fileLength ] = new List< FileInfo > { file };
            }
        }
    }

    private static DirectoryInfo CreateNewModDir( Mod mod, string optionGroup, string option )
    {
        var newName = $"{mod.BasePath.Name}_{optionGroup}_{option}";
        return TexToolsImport.CreateModFolder( new DirectoryInfo( Penumbra.Config.ModDirectory ), newName );
    }

    private static Mod CreateNewMod( DirectoryInfo newDir, string newSortOrder )
    {
        var idx    = Penumbra.ModManager.AddMod( newDir );
        var newMod = Penumbra.ModManager.Mods[ idx ];
        newMod.Move( newSortOrder );
        newMod.ComputeChangedItems();
        ModFileSystem.InvokeChange();
        return newMod;
    }

    private static ModMeta CreateNewMeta( DirectoryInfo newDir, Mod mod, string name, string optionGroup, string option )
    {
        var newMeta = new ModMeta
        {
            Author      = mod.Meta.Author,
            Name        = name,
            Description = $"Split from {mod.Meta.Name} Group {optionGroup} Option {option}.",
        };
        var metaFile = new FileInfo( Path.Combine( newDir.FullName, "meta.json" ) );
        newMeta.SaveToFile( metaFile );
        return newMeta;
    }

    private static void CreateModSplit( HashSet< string > unseenPaths, Mod mod, OptionGroup group, Option option )
    {
        try
        {
            var newDir  = CreateNewModDir( mod, group.GroupName, option.OptionName );
            var newName = group.SelectionType == SelectType.Multi ? $"{group.GroupName} - {option.OptionName}" : option.OptionName;
            var newMeta = CreateNewMeta( newDir, mod, newName, group.GroupName, option.OptionName );
            foreach( var (fileName, paths) in option.OptionFiles )
            {
                var oldPath = Path.Combine( mod.BasePath.FullName, fileName.ToString() );
                unseenPaths.Remove( oldPath );
                if( File.Exists( oldPath ) )
                {
                    foreach( var path in paths )
                    {
                        var newPath = Path.Combine( newDir.FullName, path.ToString() );
                        Directory.CreateDirectory( Path.GetDirectoryName( newPath )! );
                        File.Copy( oldPath, newPath, true );
                    }
                }
            }

            var newSortOrder = group.SelectionType == SelectType.Single
                ? $"{mod.Order.ParentFolder.FullName}/{mod.Meta.Name}/{group.GroupName}/{option.OptionName}"
                : $"{mod.Order.ParentFolder.FullName}/{mod.Meta.Name}/{group.GroupName} - {option.OptionName}";
            CreateNewMod( newDir, newSortOrder );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not split Mod:\n{e}" );
        }
    }

    public static void SplitMod( Mod mod )
    {
        if( mod.Meta.Groups.Count == 0 )
        {
            return;
        }

        var unseenPaths = mod.Resources.ModFiles.Select( f => f.FullName ).ToHashSet();
        foreach( var group in mod.Meta.Groups.Values )
        {
            foreach( var option in group.Options )
            {
                CreateModSplit( unseenPaths, mod, group, option );
            }
        }

        if( unseenPaths.Count == 0 )
        {
            return;
        }

        var defaultGroup = new OptionGroup()
        {
            GroupName     = "Default",
            SelectionType = SelectType.Multi,
        };
        var defaultOption = new Option()
        {
            OptionName = "Files",
            OptionFiles = unseenPaths.ToDictionary(
                p => Utf8RelPath.FromFile( new FileInfo( p ), mod.BasePath, out var rel ) ? rel : Utf8RelPath.Empty,
                p => new HashSet< Utf8GamePath >()
                    { Utf8GamePath.FromFile( new FileInfo( p ), mod.BasePath, out var game, true ) ? game : Utf8GamePath.Empty } ),
        };
        CreateModSplit( unseenPaths, mod, defaultGroup, defaultOption );
    }

    private static Option FindOrCreateDuplicates( ModMeta meta )
    {
        static Option RequiredOption()
            => new()
            {
                OptionName  = Required,
                OptionDesc  = "",
                OptionFiles = new Dictionary< Utf8RelPath, HashSet< Utf8GamePath > >(),
            };

        if( meta.Groups.TryGetValue( Duplicates, out var duplicates ) )
        {
            var idx = duplicates.Options.FindIndex( o => o.OptionName == Required );
            if( idx >= 0 )
            {
                return duplicates.Options[ idx ];
            }

            duplicates.Options.Add( RequiredOption() );
            return duplicates.Options.Last();
        }

        meta.Groups.Add( Duplicates, new OptionGroup
        {
            GroupName     = Duplicates,
            SelectionType = SelectType.Single,
            Options       = new List< Option > { RequiredOption() },
        } );

        return meta.Groups[ Duplicates ].Options.First();
    }

    public static void Deduplicate( DirectoryInfo baseDir, ModMeta mod )
    {
        var dedup = new ModCleanup( baseDir, mod );
        foreach( var (key, value) in dedup._filesBySize.Where( pair => pair.Value.Count >= 2 ) )
        {
            if( value.Count == 2 )
            {
                if( CompareFilesDirectly( value[ 0 ], value[ 1 ] ) )
                {
                    dedup.ReplaceFile( value[ 0 ], value[ 1 ] );
                }
            }
            else
            {
                var deleted = Enumerable.Repeat( false, value.Count ).ToArray();
                var hashes  = value.Select( dedup.ComputeHash ).ToArray();

                for( var i = 0; i < value.Count; ++i )
                {
                    if( deleted[ i ] )
                    {
                        continue;
                    }

                    for( var j = i + 1; j < value.Count; ++j )
                    {
                        if( deleted[ j ] || !CompareHashes( hashes[ i ], hashes[ j ] ) )
                        {
                            continue;
                        }

                        dedup.ReplaceFile( value[ i ], value[ j ] );
                        deleted[ j ] = true;
                    }
                }
            }
        }

        CleanUpDuplicates( mod );
        ClearEmptySubDirectories( dedup._baseDir );
    }

    private void ReplaceFile( FileInfo f1, FileInfo f2 )
    {
        if( !Utf8RelPath.FromFile( f1, _baseDir, out var relName1 )
        || !Utf8RelPath.FromFile( f2, _baseDir, out var relName2 ) )
        {
            return;
        }

        var inOption1 = false;
        var inOption2 = false;
        foreach( var option in _mod.Groups.SelectMany( g => g.Value.Options ) )
        {
            if( option.OptionFiles.ContainsKey( relName1 ) )
            {
                inOption1 = true;
            }

            if( !option.OptionFiles.TryGetValue( relName2, out var values ) )
            {
                continue;
            }

            inOption2 = true;

            foreach( var value in values )
            {
                option.AddFile( relName1, value );
            }

            option.OptionFiles.Remove( relName2 );
        }

        if( !inOption1 || !inOption2 )
        {
            var duplicates = FindOrCreateDuplicates( _mod );
            if( !inOption1 )
            {
                duplicates.AddFile( relName1, relName2.ToGamePath() );
            }

            if( !inOption2 )
            {
                duplicates.AddFile( relName1, relName1.ToGamePath() );
            }
        }

        PluginLog.Information( $"File {relName1} and {relName2} are identical. Deleting the second." );
        f2.Delete();
    }

    public static bool CompareFilesDirectly( FileInfo f1, FileInfo f2 )
        => File.ReadAllBytes( f1.FullName ).SequenceEqual( File.ReadAllBytes( f2.FullName ) );

    public static bool CompareHashes( byte[] f1, byte[] f2 )
        => StructuralComparisons.StructuralEqualityComparer.Equals( f1, f2 );

    public byte[] ComputeHash( FileInfo f )
    {
        var stream = File.OpenRead( f.FullName );
        var ret    = Sha().ComputeHash( stream );
        stream.Dispose();
        return ret;
    }

    // Does not delete the base directory itself even if it is completely empty at the end.
    public static void ClearEmptySubDirectories( DirectoryInfo baseDir )
    {
        foreach( var subDir in baseDir.GetDirectories() )
        {
            ClearEmptySubDirectories( subDir );
            if( subDir.GetFiles().Length == 0 && subDir.GetDirectories().Length == 0 )
            {
                subDir.Delete();
            }
        }
    }

    private static bool FileIsInAnyGroup( ModMeta meta, Utf8RelPath relPath, bool exceptDuplicates = false )
    {
        var groupEnumerator = exceptDuplicates
            ? meta.Groups.Values.Where( g => g.GroupName != Duplicates )
            : meta.Groups.Values;
        return groupEnumerator.SelectMany( group => group.Options )
           .Any( option => option.OptionFiles.ContainsKey( relPath ) );
    }

    private static void CleanUpDuplicates( ModMeta meta )
    {
        if( !meta.Groups.TryGetValue( Duplicates, out var info ) )
        {
            return;
        }

        var requiredIdx = info.Options.FindIndex( o => o.OptionName == Required );
        if( requiredIdx >= 0 )
        {
            var required = info.Options[ requiredIdx ];
            foreach( var (key, value) in required.OptionFiles.ToArray() )
            {
                if( value.Count > 1 || FileIsInAnyGroup( meta, key, true ) )
                {
                    continue;
                }

                if( value.Count == 0 || value.First().CompareTo( key.ToGamePath() ) == 0 )
                {
                    required.OptionFiles.Remove( key );
                }
            }

            if( required.OptionFiles.Count == 0 )
            {
                info.Options.RemoveAt( requiredIdx );
            }
        }

        if( info.Options.Count == 0 )
        {
            meta.Groups.Remove( Duplicates );
        }
    }

    public enum GroupType
    {
        Both   = 0,
        Single = 1,
        Multi  = 2,
    };

    private static void RemoveFromGroups( ModMeta meta, Utf8RelPath relPath, Utf8GamePath gamePath, GroupType type = GroupType.Both,
        bool skipDuplicates = true )
    {
        if( meta.Groups.Count == 0 )
        {
            return;
        }

        var enumerator = type switch
        {
            GroupType.Both   => meta.Groups.Values,
            GroupType.Single => meta.Groups.Values.Where( g => g.SelectionType == SelectType.Single ),
            GroupType.Multi  => meta.Groups.Values.Where( g => g.SelectionType == SelectType.Multi ),
            _                => throw new InvalidEnumArgumentException( "Invalid Enum in RemoveFromGroups" ),
        };
        foreach( var group in enumerator )
        {
            var optionEnum = skipDuplicates
                ? group.Options.Where( o => group.GroupName != Duplicates || o.OptionName != Required )
                : group.Options;
            foreach( var option in optionEnum )
            {
                if( option.OptionFiles.TryGetValue( relPath, out var gamePaths ) && gamePaths.Remove( gamePath ) && gamePaths.Count == 0 )
                {
                    option.OptionFiles.Remove( relPath );
                }
            }
        }
    }

    public static bool MoveFile( ModMeta meta, string basePath, Utf8RelPath oldRelPath, Utf8RelPath newRelPath )
    {
        if( oldRelPath.Equals( newRelPath ) )
        {
            return true;
        }

        try
        {
            var newFullPath = Path.Combine( basePath, newRelPath.ToString() );
            new FileInfo( newFullPath ).Directory!.Create();
            File.Move( Path.Combine( basePath, oldRelPath.ToString() ), newFullPath );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not move file from {oldRelPath} to {newRelPath}:\n{e}" );
            return false;
        }

        foreach( var option in meta.Groups.Values.SelectMany( group => group.Options ) )
        {
            if( option.OptionFiles.TryGetValue( oldRelPath, out var gamePaths ) )
            {
                option.OptionFiles.Add( newRelPath, gamePaths );
                option.OptionFiles.Remove( oldRelPath );
            }
        }

        return true;
    }


    private static void RemoveUselessGroups( ModMeta meta )
    {
        meta.Groups = meta.Groups.Where( kvp => kvp.Value.Options.Any( o => o.OptionFiles.Count > 0 ) )
           .ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
    }

    // Goes through all Single-Select options and checks if file links are in each of them.
    // If they are, it moves those files to the root folder and removes them from the groups (and puts them to duplicates, if necessary).
    public static void Normalize( DirectoryInfo baseDir, ModMeta meta )
    {
        foreach( var group in meta.Groups.Values.Where( g => g.SelectionType == SelectType.Single && g.GroupName != Duplicates ) )
        {
            var                                    firstOption = true;
            HashSet< (Utf8RelPath, Utf8GamePath) > groupList   = new();
            foreach( var option in group.Options )
            {
                HashSet< (Utf8RelPath, Utf8GamePath) > optionList = new();
                foreach( var (file, gamePaths) in option.OptionFiles.Select( p => ( p.Key, p.Value ) ) )
                {
                    optionList.UnionWith( gamePaths.Select( p => ( file, p ) ) );
                }

                if( firstOption )
                {
                    groupList = optionList;
                }
                else
                {
                    groupList.IntersectWith( optionList );
                }

                firstOption = false;
            }

            var newPath = new Dictionary< Utf8RelPath, Utf8GamePath >();
            foreach( var (path, gamePath) in groupList )
            {
                var relPath = new Utf8RelPath( gamePath );
                if( newPath.TryGetValue( path, out var usedGamePath ) )
                {
                    var required    = FindOrCreateDuplicates( meta );
                    var usedRelPath = new Utf8RelPath( usedGamePath );
                    required.AddFile( usedRelPath, gamePath );
                    required.AddFile( usedRelPath, usedGamePath );
                    RemoveFromGroups( meta, relPath, gamePath, GroupType.Single );
                }
                else if( MoveFile( meta, baseDir.FullName, path, relPath ) )
                {
                    newPath[ path ] = gamePath;
                    if( FileIsInAnyGroup( meta, relPath ) )
                    {
                        FindOrCreateDuplicates( meta ).AddFile( relPath, gamePath );
                    }

                    RemoveFromGroups( meta, relPath, gamePath, GroupType.Single );
                }
            }
        }

        RemoveUselessGroups( meta );
        ClearEmptySubDirectories( baseDir );
    }

    public static void AutoGenerateGroups( DirectoryInfo baseDir, ModMeta meta )
    {
        meta.Groups.Clear();
        ClearEmptySubDirectories( baseDir );
        foreach( var groupDir in baseDir.EnumerateDirectories() )
        {
            var group = new OptionGroup
            {
                GroupName     = groupDir.Name,
                SelectionType = SelectType.Single,
                Options       = new List< Option >(),
            };

            foreach( var optionDir in groupDir.EnumerateDirectories() )
            {
                var option = new Option
                {
                    OptionDesc  = string.Empty,
                    OptionName  = optionDir.Name,
                    OptionFiles = new Dictionary< Utf8RelPath, HashSet< Utf8GamePath > >(),
                };
                foreach( var file in optionDir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
                {
                    if( Utf8RelPath.FromFile( file, baseDir, out var rel )
                    && Utf8GamePath.FromFile( file, optionDir, out var game ) )
                    {
                        option.OptionFiles[ rel ] = new HashSet< Utf8GamePath > { game };
                    }
                }

                if( option.OptionFiles.Count > 0 )
                {
                    group.Options.Add( option );
                }
            }

            if( group.Options.Count > 0 )
            {
                meta.Groups.Add( groupDir.Name, group );
            }
        }

        // TODO
        var idx = Penumbra.ModManager.Mods.IndexOf( m => m.Meta == meta );
        foreach( var collection in Penumbra.CollectionManager )
        {
            collection.Settings[ idx ]?.FixInvalidSettings( meta );
        }
    }
}