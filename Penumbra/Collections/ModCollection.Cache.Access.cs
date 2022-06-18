using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public int RecomputeCounter
        => _cache?.ChangeCounter ?? 0;

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


    // Clear the current cache.
    private void ClearCache()
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
            Thread.CurrentThread.ManagedThreadId, Name );
        _cache ??= new Cache( this );
        _cache.FullRecalculation();

        PluginLog.Debug( "[{Thread}] Recalculation of effective file list for {CollectionName:l} finished.",
            Thread.CurrentThread.ManagedThreadId, Name );
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
            PluginLog.Debug( "Set CharacterUtility resources for collection {Name:l}.", Name );
        }
    }
}