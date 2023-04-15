using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui.Filesystem;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public struct GmpCache : IDisposable
{
    private          ExpandedGmpFile?        _gmpFile          = null;
    private readonly List< GmpManipulation > _gmpManipulations = new();

    public GmpCache()
    {}

    public void SetFiles(CollectionCacheManager manager)
        => manager.SetFile( _gmpFile, MetaIndex.Gmp );

    public CharacterUtility.MetaList.MetaReverter TemporarilySetFiles(CollectionCacheManager manager)
        => manager.TemporarilySetFile( _gmpFile, MetaIndex.Gmp );

    public void ResetGmp(CollectionCacheManager manager)
    {
        if( _gmpFile == null )
            return;

        _gmpFile.Reset( _gmpManipulations.Select( m => ( int )m.SetId ) );
        _gmpManipulations.Clear();
    }

    public bool ApplyMod( CollectionCacheManager manager, GmpManipulation manip )
    {
        _gmpManipulations.AddOrReplace( manip );
        _gmpFile ??= new ExpandedGmpFile();
        return manip.Apply( _gmpFile );
    }

    public bool RevertMod( CollectionCacheManager manager, GmpManipulation manip )
    {
        if (!_gmpManipulations.Remove(manip))
            return false;

        var def = ExpandedGmpFile.GetDefault( manip.SetId );
        manip = new GmpManipulation( def, manip.SetId );
        return manip.Apply( _gmpFile! );
    }

    public void Dispose()
    {
        _gmpFile?.Dispose();
        _gmpFile = null;
        _gmpManipulations.Clear();
    }
}