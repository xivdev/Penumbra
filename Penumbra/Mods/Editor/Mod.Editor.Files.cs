using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Editor
    {
        // All files in subdirectories of the mod directory.
        public IReadOnlyList< (FullPath, long) > AvailableFiles
            => _availableFiles;

        private readonly List< (FullPath, long) > _availableFiles;

        // All files that are available but not currently used in any option.
        private readonly SortedSet< FullPath > _unusedFiles;

        public IReadOnlySet< FullPath > UnusedFiles
            => _unusedFiles;

        // All paths that are used in any option in the mod.
        private readonly SortedSet< FullPath > _usedPaths;

        public IReadOnlySet< FullPath > UsedPaths
            => _usedPaths;

        // All paths that are used in 
        private readonly SortedSet< FullPath > _missingPaths;

        public IReadOnlySet< FullPath > MissingPaths
            => _missingPaths;

        // Adds all currently unused paths, relative to the mod directory, to the replacements.
        public void AddUnusedPathsToDefault()
        {
            var dict = new Dictionary< Utf8GamePath, FullPath >( UnusedFiles.Count );
            foreach( var file in UnusedFiles )
            {
                var gamePath = file.ToGamePath( _mod.BasePath, out var g ) ? g : Utf8GamePath.Empty;
                if( !gamePath.IsEmpty && !dict.ContainsKey( gamePath ) )
                {
                    dict.Add( gamePath, file );
                    PluginLog.Debug( "[AddUnusedPaths] Adding {GamePath} -> {File} to default option of {Mod}.", gamePath, file, _mod.Name );
                }
            }

            Penumbra.ModManager.OptionAddFiles( _mod, -1, 0, dict );
            _usedPaths.UnionWith( _mod.Default.Files.Values );
            _unusedFiles.RemoveWhere( f => _mod.Default.Files.Values.Contains( f ) );
        }

        // Delete all currently unused paths from your filesystem.
        public void DeleteUnusedPaths()
        {
            foreach( var file in UnusedFiles )
            {
                try
                {
                    File.Delete( file.FullName );
                    PluginLog.Debug( "[DeleteUnusedPaths] Deleted {File} from {Mod}.", file, _mod.Name );
                }
                catch( Exception e )
                {
                    PluginLog.Error($"[DeleteUnusedPaths] Could not delete {file} from {_mod.Name}:\n{e}"  );
                }
            }

            _unusedFiles.RemoveWhere( f => !f.Exists );
            _availableFiles.RemoveAll( p => !p.Item1.Exists );
        }

        // Remove all path redirections where the pointed-to file does not exist.
        public void RemoveMissingPaths()
        {
            void HandleSubMod( ISubMod mod, int groupIdx, int optionIdx )
            {
                var newDict = mod.Files.Where( kvp => CheckAgainstMissing( kvp.Value, kvp.Key ) )
                   .ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
                if( newDict.Count != mod.Files.Count )
                {
                    Penumbra.ModManager.OptionSetFiles( _mod, groupIdx, optionIdx, newDict );
                }
            }

            ApplyToAllOptions( _mod, HandleSubMod );
            _usedPaths.RemoveWhere( _missingPaths.Contains );
            _missingPaths.Clear();
        }

        private bool CheckAgainstMissing( FullPath file, Utf8GamePath key )
        {
            if( !_missingPaths.Contains( file ) )
            {
                return true;
            }

            PluginLog.Debug( "[RemoveMissingPaths] Removing {GamePath} -> {File} from {Mod}.", key, file, _mod.Name );
            return false;
        }


        private static List<(FullPath, long)> GetAvailablePaths( Mod mod )
            => mod.BasePath.EnumerateDirectories()
               .SelectMany( d => d.EnumerateFiles( "*.*", SearchOption.AllDirectories ).Select( f => (new FullPath( f ), f.Length) ) )
               .OrderBy( p => -p.Length ).ToList();
    }
}