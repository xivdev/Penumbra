using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dalamud.Plugin;
using Penumbra.Structs;
using Penumbra.Util;

namespace Penumbra.Mod
{
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
                    _filesBySize[ fileLength ] = new List< FileInfo >() { file };
                }
            }
        }

        private static Option FindOrCreateDuplicates( ModMeta meta )
        {
            static Option RequiredOption()
                => new()
                {
                    OptionName  = Required,
                    OptionDesc  = "",
                    OptionFiles = new Dictionary< RelPath, HashSet< GamePath > >(),
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
            foreach( var pair in dedup._filesBySize.Where( pair => pair.Value.Count >= 2 ) )
            {
                if( pair.Value.Count == 2 )
                {
                    if( CompareFilesDirectly( pair.Value[ 0 ], pair.Value[ 1 ] ) )
                    {
                        dedup.ReplaceFile( pair.Value[ 0 ], pair.Value[ 1 ] );
                    }
                }
                else
                {
                    var deleted = Enumerable.Repeat( false, pair.Value.Count ).ToArray();
                    var hashes  = pair.Value.Select( dedup.ComputeHash ).ToArray();

                    for( var i = 0; i < pair.Value.Count; ++i )
                    {
                        if( deleted[ i ] )
                        {
                            continue;
                        }

                        for( var j = i + 1; j < pair.Value.Count; ++j )
                        {
                            if( deleted[ j ] || !CompareHashes( hashes[ i ], hashes[ j ] ) )
                            {
                                continue;
                            }

                            dedup.ReplaceFile( pair.Value[ i ], pair.Value[ j ] );
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
            RelPath relName1 = new( f1, _baseDir );
            RelPath relName2 = new( f2, _baseDir );

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
                    duplicates.AddFile( relName1, new GamePath( relName2, 0 ) );
                }

                if( !inOption2 )
                {
                    duplicates.AddFile( relName1, new GamePath( relName1, 0 ) );
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

        private static bool FileIsInAnyGroup( ModMeta meta, RelPath relPath, bool exceptDuplicates = false )
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
                foreach( var kvp in required.OptionFiles.ToArray() )
                {
                    if( kvp.Value.Count > 1 || FileIsInAnyGroup( meta, kvp.Key, true ) )
                    {
                        continue;
                    }

                    if( kvp.Value.Count == 0 || kvp.Value.First().CompareTo( new GamePath( kvp.Key, 0 ) ) == 0 )
                    {
                        required.OptionFiles.Remove( kvp.Key );
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

        private static void RemoveFromGroups( ModMeta meta, RelPath relPath, GamePath gamePath, GroupType type = GroupType.Both,
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

        public static bool MoveFile( ModMeta meta, string basePath, RelPath oldRelPath, RelPath newRelPath )
        {
            if( oldRelPath == newRelPath )
            {
                return true;
            }

            try
            {
                var newFullPath = Path.Combine( basePath, newRelPath );
                new FileInfo( newFullPath ).Directory!.Create();
                File.Move( Path.Combine( basePath, oldRelPath ), newFullPath );
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
                var                            firstOption = true;
                HashSet< (RelPath, GamePath) > groupList   = new();
                foreach( var option in group.Options )
                {
                    HashSet< (RelPath, GamePath) > optionList = new();
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

                var newPath = new Dictionary< RelPath, GamePath >();
                foreach( var (path, gamePath) in groupList )
                {
                    var relPath = new RelPath( gamePath );
                    if( newPath.TryGetValue( path, out var usedGamePath ) )
                    {
                        var required    = FindOrCreateDuplicates( meta );
                        var usedRelPath = new RelPath( usedGamePath );
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
    }
}