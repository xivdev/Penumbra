using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Editor
    {
        public class FileRegistry : IEquatable< FileRegistry >
        {
            public readonly List< (ISubMod, Utf8GamePath) > SubModUsage = new();
            public FullPath File { get; private init; }
            public Utf8RelPath RelPath { get; private init; }
            public long FileSize { get; private init; }
            public int CurrentUsage;

            public static bool FromFile( Mod mod, FileInfo file, [NotNullWhen( true )] out FileRegistry? registry )
            {
                var fullPath = new FullPath( file.FullName );
                if( !fullPath.ToRelPath( mod.ModPath, out var relPath ) )
                {
                    registry = null;
                    return false;
                }

                registry = new FileRegistry
                {
                    File         = fullPath,
                    RelPath      = relPath,
                    FileSize     = file.Length,
                    CurrentUsage = 0,
                };
                return true;
            }

            public bool Equals( FileRegistry? other )
            {
                if( other is null )
                {
                    return false;
                }

                return ReferenceEquals( this, other ) || File.Equals( other.File );
            }

            public override bool Equals( object? obj )
            {
                if( obj is null )
                {
                    return false;
                }

                if( ReferenceEquals( this, obj ) )
                {
                    return true;
                }

                return obj.GetType() == GetType() && Equals( ( FileRegistry )obj );
            }

            public override int GetHashCode()
                => File.GetHashCode();
        }

        // All files in subdirectories of the mod directory.
        public IReadOnlyList< FileRegistry > AvailableFiles
            => _availableFiles;

        public bool FileChanges { get; private set; }
        private          List< FileRegistry >    _availableFiles = null!;
        private          List< FileRegistry >    _mtrlFiles      = null!;
        private          List< FileRegistry >    _mdlFiles       = null!;
        private          List< FileRegistry >    _texFiles       = null!;
        private readonly HashSet< Utf8GamePath > _usedPaths      = new();

        // All paths that are used in 
        private readonly SortedSet< FullPath > _missingFiles = new();

        public IReadOnlySet< FullPath > MissingFiles
            => _missingFiles;

        public IReadOnlyList< FileRegistry > MtrlFiles
            => _mtrlFiles;

        public IReadOnlyList< FileRegistry > MdlFiles
            => _mdlFiles;

        public IReadOnlyList< FileRegistry > TexFiles
            => _texFiles;

        // Remove all path redirections where the pointed-to file does not exist.
        public void RemoveMissingPaths()
        {
            void HandleSubMod( ISubMod mod, int groupIdx, int optionIdx )
            {
                var newDict = mod.Files.Where( kvp => CheckAgainstMissing( kvp.Value, kvp.Key, mod == _subMod ) )
                   .ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
                if( newDict.Count != mod.Files.Count )
                {
                    Penumbra.ModManager.OptionSetFiles( _mod, groupIdx, optionIdx, newDict );
                }
            }

            ApplyToAllOptions( _mod, HandleSubMod );
            _missingFiles.Clear();
        }

        private bool CheckAgainstMissing( FullPath file, Utf8GamePath key, bool removeUsed )
        {
            if( !_missingFiles.Contains( file ) )
            {
                return true;
            }

            if( removeUsed )
            {
                _usedPaths.Remove( key );
            }

            Penumbra.Log.Debug( $"[RemoveMissingPaths] Removing {key} -> {file} from {_mod.Name}." );
            return false;
        }


        // Fetch all files inside subdirectories of the main mod directory.
        // Then check which options use them and how often.
        private void UpdateFiles()
        {
            _availableFiles = _mod.ModPath.EnumerateDirectories()
               .SelectMany( d => d.EnumerateFiles( "*.*", SearchOption.AllDirectories )
                   .Select( f => FileRegistry.FromFile( _mod, f, out var r ) ? r : null )
                   .OfType< FileRegistry >() )
               .ToList();
            _usedPaths.Clear();
            _mtrlFiles  = _availableFiles.Where( f => f.File.FullName.EndsWith( ".mtrl", StringComparison.OrdinalIgnoreCase ) ).ToList();
            _mdlFiles   = _availableFiles.Where( f => f.File.FullName.EndsWith( ".mdl", StringComparison.OrdinalIgnoreCase ) ).ToList();
            _texFiles   = _availableFiles.Where( f => f.File.FullName.EndsWith( ".tex", StringComparison.OrdinalIgnoreCase ) ).ToList();
            FileChanges = false;
            foreach( var subMod in _mod.AllSubMods )
            {
                foreach( var (gamePath, file) in subMod.Files )
                {
                    if( !file.Exists )
                    {
                        _missingFiles.Add( file );
                        if( subMod == _subMod )
                        {
                            _usedPaths.Add( gamePath );
                        }
                    }
                    else
                    {
                        var registry = _availableFiles.Find( x => x.File.Equals( file ) );
                        if( registry != null )
                        {
                            if( subMod == _subMod )
                            {
                                ++registry.CurrentUsage;
                                _usedPaths.Add( gamePath );
                            }

                            registry.SubModUsage.Add( ( subMod, gamePath ) );
                        }
                    }
                }
            }
        }

        // Return whether the given path is already used in the current option.
        public bool CanAddGamePath( Utf8GamePath path )
            => !_usedPaths.Contains( path );

        // Try to set a given path for a given file.
        // Returns false if this is not possible.
        // If path is empty, it will be deleted instead.
        // If pathIdx is equal to the total number of paths, path will be added, otherwise replaced.
        public bool SetGamePath( int fileIdx, int pathIdx, Utf8GamePath path )
        {
            if( _usedPaths.Contains( path ) || fileIdx < 0 || fileIdx > _availableFiles.Count )
            {
                return false;
            }

            var registry = _availableFiles[ fileIdx ];
            if( pathIdx > registry.SubModUsage.Count )
            {
                return false;
            }

            if( ( pathIdx == -1 || pathIdx == registry.SubModUsage.Count ) && !path.IsEmpty )
            {
                registry.SubModUsage.Add( ( CurrentOption, path ) );
                ++registry.CurrentUsage;
                _usedPaths.Add( path );
            }
            else
            {
                _usedPaths.Remove( registry.SubModUsage[ pathIdx ].Item2 );
                if( path.IsEmpty )
                {
                    registry.SubModUsage.RemoveAt( pathIdx );
                    --registry.CurrentUsage;
                }
                else
                {
                    registry.SubModUsage[ pathIdx ] = ( registry.SubModUsage[ pathIdx ].Item1, path );
                }
            }

            FileChanges = true;

            return true;
        }

        // Transform a set of files to the appropriate game paths with the given number of folders skipped,
        // and add them to the given option.
        public int AddPathsToSelected( IEnumerable< FileRegistry > files, int skipFolders = 0 )
        {
            var failed = 0;
            foreach( var file in files )
            {
                var gamePath = file.RelPath.ToGamePath( skipFolders );
                if( gamePath.IsEmpty )
                {
                    ++failed;
                    continue;
                }

                if( CanAddGamePath( gamePath ) )
                {
                    ++file.CurrentUsage;
                    file.SubModUsage.Add( ( CurrentOption, gamePath ) );
                    _usedPaths.Add( gamePath );
                    FileChanges = true;
                }
                else
                {
                    ++failed;
                }
            }

            return failed;
        }

        // Remove all paths in the current option from the given files.
        public void RemovePathsFromSelected( IEnumerable< FileRegistry > files )
        {
            foreach( var file in files )
            {
                file.CurrentUsage =  0;
                FileChanges       |= file.SubModUsage.RemoveAll( p => p.Item1 == CurrentOption && _usedPaths.Remove( p.Item2 ) ) > 0;
            }
        }

        // Delete all given files from your filesystem
        public void DeleteFiles( IEnumerable< FileRegistry > files )
        {
            var deletions = 0;
            foreach( var file in files )
            {
                try
                {
                    File.Delete( file.File.FullName );
                    Penumbra.Log.Debug( $"[DeleteFiles] Deleted {file.File.FullName} from {_mod.Name}." );
                    ++deletions;
                }
                catch( Exception e )
                {
                    Penumbra.Log.Error( $"[DeleteFiles] Could not delete {file.File.FullName} from {_mod.Name}:\n{e}" );
                }
            }

            if( deletions > 0 )
            {
                _mod.Reload( false, out _ );
                UpdateFiles();
            }
        }
    }
}