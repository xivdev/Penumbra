using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manager;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Collections;

public partial class ModCollection
{
    // Only active collections need to have a cache.
    private Cache? _cache;

    public bool HasCache
        => _cache != null;

    // Only create, do not update. 
    public void CreateCache( bool isDefault )
    {
        if( _cache == null )
        {
            CalculateEffectiveFileList( true, isDefault );
        }
    }

    // Force an update with metadata for this cache.
    public void ForceCacheUpdate( bool isDefault )
        => CalculateEffectiveFileList( true, isDefault );


    // Clear the current cache.
    public void ClearCache()
    {
        _cache?.Dispose();
        _cache = null;
    }


    public FullPath? ResolvePath( Utf8GamePath path )
        => _cache?.ResolvePath( path );

    // Force a file to be resolved to a specific path regardless of conflicts.
    internal void ForceFile( Utf8GamePath path, FullPath fullPath )
        => _cache!.ResolvedFiles[ path ] = fullPath;

    // Force a file resolve to be removed.
    internal void RemoveFile( Utf8GamePath path )
        => _cache!.ResolvedFiles.Remove( path );

    // Obtain data from the cache.
    internal MetaManager? MetaCache
        => _cache?.MetaManipulations;

    internal IReadOnlyDictionary< Utf8GamePath, FullPath > ResolvedFiles
        => _cache?.ResolvedFiles ?? new Dictionary< Utf8GamePath, FullPath >();

    internal IReadOnlySet< FullPath > MissingFiles
        => _cache?.MissingFiles ?? new HashSet< FullPath >();

    internal IReadOnlyDictionary< string, object? > ChangedItems
        => _cache?.ChangedItems ?? new Dictionary< string, object? >();

    internal IReadOnlyList< ConflictCache.Conflict > Conflicts
        => _cache?.Conflicts.Conflicts ?? Array.Empty< ConflictCache.Conflict >();

    internal IEnumerable< ConflictCache.Conflict > ModConflicts( int modIdx )
        => _cache?.Conflicts.ModConflicts( modIdx ) ?? Array.Empty< ConflictCache.Conflict >();

    // Update the effective file list for the given cache.
    // Creates a cache if necessary.
    public void CalculateEffectiveFileList( bool withMetaManipulations, bool reloadDefault )
    {
        // Skip the empty collection.
        if( Index == 0 )
        {
            return;
        }

        PluginLog.Debug( "Recalculating effective file list for {CollectionName} [{WithMetaManipulations}] [{ReloadDefault}]", Name,
            withMetaManipulations, reloadDefault );
        _cache ??= new Cache( this );
        _cache.CalculateEffectiveFileList();
        if( withMetaManipulations )
        {
            _cache.UpdateMetaManipulations();
            if( reloadDefault )
            {
                SetFiles();
            }
        }

        if( reloadDefault )
        {
            Penumbra.ResidentResources.Reload();
        }
    }

    // Set Metadata files.
    [Conditional( "USE_EQP" )]
    public void SetEqpFiles()
    {
        if( _cache == null )
        {
            MetaManager.MetaManagerEqp.ResetFiles();
        }
        else
        {
            _cache.MetaManipulations.Eqp.SetFiles();
        }
    }

    [Conditional( "USE_EQDP" )]
    public void SetEqdpFiles()
    {
        if( _cache == null )
        {
            MetaManager.MetaManagerEqdp.ResetFiles();
        }
        else
        {
            _cache.MetaManipulations.Eqdp.SetFiles();
        }
    }

    [Conditional( "USE_GMP" )]
    public void SetGmpFiles()
    {
        if( _cache == null )
        {
            MetaManager.MetaManagerGmp.ResetFiles();
        }
        else
        {
            _cache.MetaManipulations.Gmp.SetFiles();
        }
    }

    [Conditional( "USE_EST" )]
    public void SetEstFiles()
    {
        if( _cache == null )
        {
            MetaManager.MetaManagerEst.ResetFiles();
        }
        else
        {
            _cache.MetaManipulations.Est.SetFiles();
        }
    }

    [Conditional( "USE_CMP" )]
    public void SetCmpFiles()
    {
        if( _cache == null )
        {
            MetaManager.MetaManagerCmp.ResetFiles();
        }
        else
        {
            _cache.MetaManipulations.Cmp.SetFiles();
        }
    }

    public void SetFiles()
    {
        if( _cache == null )
        {
            Penumbra.CharacterUtility.ResetAll();
        }
        else
        {
            _cache.MetaManipulations.SetFiles();
        }
    }


    // The ModCollectionCache contains all required temporary data to use a collection.
    // It will only be setup if a collection gets activated in any way.
    private class Cache : IDisposable
    {
        // Shared caches to avoid allocations.
        private static readonly BitArray                        FileSeen         = new(256);
        private static readonly Dictionary< Utf8GamePath, int > RegisteredFiles  = new(256);
        private static readonly List< ModSettings? >            ResolvedSettings = new(128);

        private readonly ModCollection                        _collection;
        private readonly SortedList< string, object? >        _changedItems = new();
        public readonly  Dictionary< Utf8GamePath, FullPath > ResolvedFiles = new();
        public readonly  HashSet< FullPath >                  MissingFiles  = new();
        public readonly  MetaManager                          MetaManipulations;
        public           ConflictCache                        Conflicts = new();

        // Obtain currently changed items. Computes them if they haven't been computed before.
        public IReadOnlyDictionary< string, object? > ChangedItems
        {
            get
            {
                SetChangedItems();
                return _changedItems;
            }
        }

        // The cache reacts through events on its collection changing.
        public Cache( ModCollection collection )
        {
            _collection                    =  collection;
            MetaManipulations              =  new MetaManager( collection );
            _collection.ModSettingChanged  += OnModSettingChange;
            _collection.InheritanceChanged += OnInheritanceChange;
        }

        public void Dispose()
        {
            _collection.ModSettingChanged  -= OnModSettingChange;
            _collection.InheritanceChanged -= OnInheritanceChange;
        }

        private void OnModSettingChange( ModSettingChange type, int modIdx, int oldValue, string? optionName, bool _ )
        {
            // Recompute the file list if it was not just a non-conflicting priority change
            // or a setting change for a disabled mod.
            if( type == ModSettingChange.Priority && !Conflicts.ModConflicts( modIdx ).Any()
            || type  == ModSettingChange.Setting  && !_collection[ modIdx ].Settings!.Enabled )
            {
                return;
            }

            var hasMeta = type is ModSettingChange.MultiEnableState or ModSettingChange.MultiInheritance
             || Penumbra.ModManager[ modIdx ].Resources.MetaManipulations.Count > 0;
            _collection.CalculateEffectiveFileList( hasMeta, Penumbra.CollectionManager.Default == _collection );
        }

        // Inheritance changes are too big to check for relevance,
        // just recompute everything.
        private void OnInheritanceChange( bool _ )
            => _collection.CalculateEffectiveFileList( true, true );

        // Reset the shared file-seen cache.
        private static void ResetFileSeen( int size )
        {
            if( size < FileSeen.Length )
            {
                FileSeen.Length = size;
                FileSeen.SetAll( false );
            }
            else
            {
                FileSeen.SetAll( false );
                FileSeen.Length = size;
            }
        }


        // Clear all local and global caches to prepare for recomputation.
        private void ClearStorageAndPrepare()
        {
            ResolvedFiles.Clear();
            MissingFiles.Clear();
            RegisteredFiles.Clear();
            _changedItems.Clear();
            ResolvedSettings.Clear();
            Conflicts.ClearFileConflicts();
            // Obtains actual settings for this collection with all inheritances.
            ResolvedSettings.AddRange( _collection.ActualSettings );
        }

        public void CalculateEffectiveFileList()
        {
            ClearStorageAndPrepare();
            for( var i = 0; i < Penumbra.ModManager.Count; ++i )
            {
                if( ResolvedSettings[ i ]?.Enabled == true )
                {
                    AddFiles( i );
                    AddSwaps( i );
                }
            }

            AddMetaFiles();
        }

        private void SetChangedItems()
        {
            if( _changedItems.Count > 0 || ResolvedFiles.Count + MetaManipulations.Count == 0 )
            {
                return;
            }

            try
            {
                // Skip IMCs because they would result in far too many false-positive items,
                // since they are per set instead of per item-slot/item/variant.
                var identifier = GameData.GameData.GetIdentifier();
                foreach( var resolved in ResolvedFiles.Keys.Where( file => !file.Path.EndsWith( 'i', 'm', 'c' ) ) )
                {
                    identifier.Identify( _changedItems, resolved.ToGamePath() );
                }
                // TODO: Meta Manipulations
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Unknown Error:\n{e}" );
            }
        }

        private void AddFiles( int idx )
        {
            var mod = Penumbra.ModManager.Mods[ idx ];
            ResetFileSeen( mod.Resources.ModFiles.Count );
            // Iterate in reverse so that later groups take precedence before earlier ones.
            // TODO: add group priorities.
            foreach( var group in mod.Meta.Groups.Values.Reverse() )
            {
                switch( group.SelectionType )
                {
                    case SelectType.Single:
                        AddFilesForSingle( group, mod, idx );
                        break;
                    case SelectType.Multi:
                        AddFilesForMulti( group, mod, idx );
                        break;
                    default: throw new InvalidEnumArgumentException();
                }
            }

            AddRemainingFiles( mod, idx );
        }

        // If audio streaming is not disabled, replacing .scd files crashes the game,
        // so only add those files if it is disabled.
        private static bool FilterFile( Utf8GamePath gamePath )
            => !Penumbra.Config.DisableSoundStreaming
             && gamePath.Path.EndsWith( '.', 's', 'c', 'd' );

        private void AddFile( int modIdx, Utf8GamePath gamePath, FullPath file )
        {
            if( FilterFile( gamePath ) )
            {
                return;
            }

            if( !RegisteredFiles.TryGetValue( gamePath, out var oldModIdx ) )
            {
                // No current conflict, just add.
                RegisteredFiles.Add( gamePath, modIdx );
                ResolvedFiles[ gamePath ] = file;
            }
            else
            {
                // Conflict, check which mod has higher priority, replace if necessary, add conflict.
                var priority    = ResolvedSettings[ modIdx ]!.Priority;
                var oldPriority = ResolvedSettings[ oldModIdx ]!.Priority;
                Conflicts.AddConflict( oldModIdx, modIdx, oldPriority, priority, gamePath );
                if( priority > oldPriority )
                {
                    ResolvedFiles[ gamePath ]   = file;
                    RegisteredFiles[ gamePath ] = modIdx;
                }
            }
        }

        private void AddMissingFile( FullPath file )
        {
            switch( file.Extension.ToLowerInvariant() )
            {
                // We do not care for those file types
                case ".scp" when !Penumbra.Config.DisableSoundStreaming:
                case ".meta":
                case ".rgsp":
                    return;
                default:
                    MissingFiles.Add( file );
                    return;
            }
        }

        private void AddPathsForOption( Option option, Mod mod, int modIdx, bool enabled )
        {
            foreach( var (file, paths) in option.OptionFiles )
            {
                // TODO: complete rework of options.
                var fullPath = new FullPath( mod.BasePath, file );
                var idx      = mod.Resources.ModFiles.IndexOf( f => f.Equals( fullPath ) );
                if( idx < 0 )
                {
                    AddMissingFile( fullPath );
                    continue;
                }

                var registeredFile = mod.Resources.ModFiles[ idx ];
                if( !registeredFile.Exists )
                {
                    AddMissingFile( registeredFile );
                    continue;
                }

                FileSeen.Set( idx, true );
                if( enabled )
                {
                    foreach( var path in paths )
                    {
                        AddFile( modIdx, path, registeredFile );
                    }
                }
            }
        }

        private void AddFilesForSingle( OptionGroup singleGroup, Mod mod, int modIdx )
        {
            Debug.Assert( singleGroup.SelectionType == SelectType.Single );
            var settings = ResolvedSettings[ modIdx ]!;
            if( !settings.Settings.TryGetValue( singleGroup.GroupName, out var setting ) )
            {
                setting = 0;
            }

            for( var i = 0; i < singleGroup.Options.Count; ++i )
            {
                AddPathsForOption( singleGroup.Options[ i ], mod, modIdx, setting == i );
            }
        }

        private void AddFilesForMulti( OptionGroup multiGroup, Mod mod, int modIdx )
        {
            Debug.Assert( multiGroup.SelectionType == SelectType.Multi );
            var settings = ResolvedSettings[ modIdx ]!;
            if( !settings.Settings.TryGetValue( multiGroup.GroupName, out var setting ) )
            {
                return;
            }

            // Also iterate options in reverse so that later options take precedence before earlier ones.
            for( var i = multiGroup.Options.Count - 1; i >= 0; --i )
            {
                AddPathsForOption( multiGroup.Options[ i ], mod, modIdx, ( setting & ( 1 << i ) ) != 0 );
            }
        }

        private void AddRemainingFiles( Mod mod, int modIdx )
        {
            for( var i = 0; i < mod.Resources.ModFiles.Count; ++i )
            {
                if( FileSeen.Get( i ) )
                {
                    continue;
                }

                var file = mod.Resources.ModFiles[ i ];
                if( file.Exists )
                {
                    if( file.ToGamePath( mod.BasePath, out var gamePath ) )
                    {
                        AddFile( modIdx, gamePath, file );
                    }
                    else
                    {
                        PluginLog.Warning( $"Could not convert {file} in {mod.BasePath.FullName} to GamePath." );
                    }
                }
                else
                {
                    MissingFiles.Add( file );
                }
            }
        }

        private void AddMetaFiles()
            => MetaManipulations.Imc.SetFiles();

        private void AddSwaps( int modIdx )
        {
            var mod = Penumbra.ModManager.Mods[ modIdx ];
            foreach( var (gamePath, swapPath) in mod.Meta.FileSwaps.Where( kvp => !FilterFile( kvp.Key ) ) )
            {
                AddFile( modIdx, gamePath, swapPath );
            }
        }

        private void AddManipulations( int modIdx )
        {
            var mod = Penumbra.ModManager.Mods[ modIdx ];
            foreach( var manip in mod.Resources.MetaManipulations.GetManipulationsForConfig( ResolvedSettings[ modIdx ]!, mod.Meta ) )
            {
                if( !MetaManipulations.TryGetValue( manip, out var oldModIdx ) )
                {
                    MetaManipulations.ApplyMod( manip, modIdx );
                }
                else
                {
                    var priority    = ResolvedSettings[ modIdx ]!.Priority;
                    var oldPriority = ResolvedSettings[ oldModIdx ]!.Priority;
                    Conflicts.AddConflict( oldModIdx, modIdx, oldPriority, priority, manip );
                    if( priority > oldPriority )
                    {
                        MetaManipulations.ApplyMod( manip, modIdx );
                    }
                }
            }
        }

        public void UpdateMetaManipulations()
        {
            MetaManipulations.Reset();
            Conflicts.ClearMetaConflicts();

            foreach( var mod in Penumbra.ModManager.Mods.Zip( ResolvedSettings )
                       .Select( ( m, i ) => ( m.First, m.Second, i ) )
                       .Where( m => m.Second?.Enabled == true && m.First.Resources.MetaManipulations.Count > 0 ) )
            {
                AddManipulations( mod.i );
            }
        }

        public FullPath? ResolvePath( Utf8GamePath gameResourcePath )
        {
            if( !ResolvedFiles.TryGetValue( gameResourcePath, out var candidate ) )
            {
                return null;
            }

            if( candidate.InternalName.Length > Utf8GamePath.MaxGamePathLength
            || candidate.IsRooted && !candidate.Exists )
            {
                return null;
            }

            return candidate;
        }
    }
}