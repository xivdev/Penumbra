using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Logging;
using OtterGui.Classes;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manager;

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

    internal SubList< ConflictCache.Conflict > ModConflicts( int modIdx )
        => _cache?.Conflicts.ModConflicts( modIdx ) ?? SubList< ConflictCache.Conflict >.Empty;

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
        _cache.CalculateEffectiveFileList( withMetaManipulations );
        if( reloadDefault )
        {
            SetFiles();
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
}