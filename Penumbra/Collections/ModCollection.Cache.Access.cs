using OtterGui.Classes;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manager;
using Penumbra.Mods;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Penumbra.Interop;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

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
            Penumbra.Log.Verbose( $"Created new cache for collection {Name}." );
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
        Penumbra.Log.Verbose( $"Cleared cache of collection {Name}." );
    }

    public IEnumerable< Utf8GamePath > ReverseResolvePath( FullPath path )
        => _cache?.ReverseResolvePath( path ) ?? Array.Empty< Utf8GamePath >();

    public FullPath? ResolvePath( Utf8GamePath path )
        => _cache?.ResolvePath( path );

    // Force a file to be resolved to a specific path regardless of conflicts.
    internal void ForceFile( Utf8GamePath path, FullPath fullPath )
    {
        if( CheckFullPath( path, fullPath ) )
        {
            _cache!.ResolvedFiles[ path ] = new ModPath( Mod.ForcedFiles, fullPath );
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static bool CheckFullPath( Utf8GamePath path, FullPath fullPath )
    {
        if( fullPath.InternalName.Length < Utf8GamePath.MaxGamePathLength )
        {
            return true;
        }

        Penumbra.Log.Error( $"The redirected path is too long to add the redirection\n\t{path}\n\t--> {fullPath}" );
        return false;
    }

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

        Penumbra.Log.Debug( $"[{Thread.CurrentThread.ManagedThreadId}] Recalculating effective file list for {AnonymizedName}" );
        _cache ??= new Cache( this );
        _cache.FullRecalculation();

        Penumbra.Log.Debug( $"[{Thread.CurrentThread.ManagedThreadId}] Recalculation of effective file list for {AnonymizedName} finished." );
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
            Penumbra.Log.Debug( $"Set CharacterUtility resources for collection {Name}." );
        }
    }

    public void SetMetaFile( Interop.Structs.CharacterUtility.Index idx )
    {
        if( _cache == null )
        {
            Penumbra.CharacterUtility.ResetResource( idx );
        }
        else
        {
            _cache.MetaManipulations.SetFile( idx );
        }
    }

    // Used for short periods of changed files.
    public CharacterUtility.List.MetaReverter TemporarilySetEqdpFile( GenderRace genderRace, bool accessory )
        => _cache?.MetaManipulations.TemporarilySetEqdpFile( genderRace, accessory )
         ?? Penumbra.CharacterUtility.TemporarilyResetResource( Interop.Structs.CharacterUtility.EqdpIdx( genderRace, accessory ) );

    public CharacterUtility.List.MetaReverter TemporarilySetEqpFile()
        => _cache?.MetaManipulations.TemporarilySetEqpFile()
         ?? Penumbra.CharacterUtility.TemporarilyResetResource( Interop.Structs.CharacterUtility.Index.Eqp );

    public CharacterUtility.List.MetaReverter TemporarilySetGmpFile()
        => _cache?.MetaManipulations.TemporarilySetGmpFile()
         ?? Penumbra.CharacterUtility.TemporarilyResetResource( Interop.Structs.CharacterUtility.Index.Gmp );

    public CharacterUtility.List.MetaReverter TemporarilySetCmpFile()
        => _cache?.MetaManipulations.TemporarilySetCmpFile()
         ?? Penumbra.CharacterUtility.TemporarilyResetResource( Interop.Structs.CharacterUtility.Index.HumanCmp );

    public CharacterUtility.List.MetaReverter TemporarilySetEstFile( EstManipulation.EstType type )
        => _cache?.MetaManipulations.TemporarilySetEstFile( type )
         ?? Penumbra.CharacterUtility.TemporarilyResetResource( ( Interop.Structs.CharacterUtility.Index )type );
}