using System;
using System.Collections.Generic;
using System.Threading;
using Dalamud.Logging;
using OtterGui.Classes;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manager;
using Penumbra.Mods;

namespace Penumbra.Collections;

public partial class ModCollection
{
    // Only active collections need to have a cache.
    private Cache? _cache;

    public bool HasCache
        => _cache != null;

    // Count the number of changes of the effective file list.
    // This is used for material and imc changes.
    public int ChangeCounter { get; private set; }

    // Only create, do not update. 
    private void CreateCache()
    {
        if( _cache == null )
        {
            CalculateEffectiveFileList();
            PluginLog.Verbose( "Created new cache for collection {Name:l}.", Name );
        }
    }

    // Force an update with metadata for this cache.
    private void ForceCacheUpdate()
        => CalculateEffectiveFileList();

    // Handle temporary mods for this collection.
    public void Apply( Mod.TemporaryMod tempMod, bool created )
    {
        if( created )
        {
            _cache?.AddMod( tempMod, tempMod.TotalManipulations > 0 );
        }
        else
        {
            _cache?.ReloadMod( tempMod, tempMod.TotalManipulations > 0 );
        }
    }

    public void Remove( Mod.TemporaryMod tempMod )
    {
        _cache?.RemoveMod( tempMod, tempMod.TotalManipulations > 0 );
    }


    // Clear the current cache.
    internal void ClearCache()
    {
        _cache?.Dispose();
        _cache = null;
        PluginLog.Verbose( "Cleared cache of collection {Name:l}.", Name );
    }

    public IEnumerable< Utf8GamePath > ReverseResolvePath( FullPath path )
        => _cache?.ReverseResolvePath( path ) ?? Array.Empty< Utf8GamePath >();

    public FullPath? ResolvePath( Utf8GamePath path )
        => _cache?.ResolvePath( path );

    // Force a file to be resolved to a specific path regardless of conflicts.
    internal void ForceFile( Utf8GamePath path, FullPath fullPath )
        => _cache!.ResolvedFiles[ path ] = new ModPath( Mod.ForcedFiles, fullPath );

    // Force a file resolve to be removed.
    internal void RemoveFile( Utf8GamePath path )
        => _cache!.ResolvedFiles.Remove( path );

    // Obtain data from the cache.
    internal MetaManager? MetaCache
        => _cache?.MetaManipulations;

    internal IReadOnlyDictionary< Utf8GamePath, ModPath > ResolvedFiles
        => _cache?.ResolvedFiles ?? new Dictionary< Utf8GamePath, ModPath >();

    internal IReadOnlyDictionary< string, (SingleArray< IMod >, object?) > ChangedItems
        => _cache?.ChangedItems ?? new Dictionary< string, (SingleArray< IMod >, object?) >();

    internal IEnumerable< SingleArray< ModConflicts > > AllConflicts
        => _cache?.AllConflicts ?? Array.Empty< SingleArray< ModConflicts > >();

    internal SingleArray< ModConflicts > Conflicts( Mod mod )
        => _cache?.Conflicts( mod ) ?? new SingleArray< ModConflicts >();

    // Update the effective file list for the given cache.
    // Creates a cache if necessary.
    public void CalculateEffectiveFileList()
        => Penumbra.Framework.RegisterImportant( nameof( CalculateEffectiveFileList ) + Name,
            CalculateEffectiveFileListInternal );

    private void CalculateEffectiveFileListInternal()
    {
        // Skip the empty collection.
        if( Index == 0 )
        {
            return;
        }

        PluginLog.Debug( "[{Thread}] Recalculating effective file list for {CollectionName:l}",
            Thread.CurrentThread.ManagedThreadId, AnonymizedName );
        _cache ??= new Cache( this );
        _cache.FullRecalculation();

        PluginLog.Debug( "[{Thread}] Recalculation of effective file list for {CollectionName:l} finished.",
            Thread.CurrentThread.ManagedThreadId, AnonymizedName );
    }

    // Set Metadata files.
    public void SetEqpFiles()
    {
        if( _cache == null )
        {
            MetaManager.ResetEqpFiles();
        }
        else
        {
            _cache.MetaManipulations.SetEqpFiles();
        }
    }

    public void SetEqdpFiles()
    {
        if( _cache == null )
        {
            MetaManager.ResetEqdpFiles();
        }
        else
        {
            _cache.MetaManipulations.SetEqdpFiles();
        }
    }

    public void SetGmpFiles()
    {
        if( _cache == null )
        {
            MetaManager.ResetGmpFiles();
        }
        else
        {
            _cache.MetaManipulations.SetGmpFiles();
        }
    }

    public void SetEstFiles()
    {
        if( _cache == null )
        {
            MetaManager.ResetEstFiles();
        }
        else
        {
            _cache.MetaManipulations.SetEstFiles();
        }
    }

    public void SetCmpFiles()
    {
        if( _cache == null )
        {
            MetaManager.ResetCmpFiles();
        }
        else
        {
            _cache.MetaManipulations.SetCmpFiles();
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
            PluginLog.Debug( "Set CharacterUtility resources for collection {Name:l}.", Name );
        }
    }
}