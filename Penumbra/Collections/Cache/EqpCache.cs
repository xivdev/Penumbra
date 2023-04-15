using System;
using System.Collections.Generic;
using OtterGui.Filesystem;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public struct EqpCache : IDisposable
{
    private          ExpandedEqpFile?        _eqpFile          = null;
    private readonly List< EqpManipulation > _eqpManipulations = new();

    public EqpCache()
    {}

    public void SetFiles(CollectionCacheManager manager) 
        => manager.SetFile( _eqpFile, MetaIndex.Eqp );

    public static void ResetFiles(CollectionCacheManager manager)
        => manager.SetFile( null, MetaIndex.Eqp );

    public CharacterUtility.MetaList.MetaReverter TemporarilySetFiles(CollectionCacheManager manager)
        => manager.TemporarilySetFile( _eqpFile, MetaIndex.Eqp );

    public bool ApplyMod( CollectionCacheManager manager, EqpManipulation manip )
    {
        _eqpManipulations.AddOrReplace( manip );
        _eqpFile ??= new ExpandedEqpFile();
        return manip.Apply( _eqpFile );
    }

    public bool RevertMod( CollectionCacheManager manager, EqpManipulation manip )
    {
        var idx = _eqpManipulations.FindIndex( manip.Equals );
        if (idx < 0)
            return false;

        var def = ExpandedEqpFile.GetDefault( manip.SetId );
        manip = new EqpManipulation( def, manip.Slot, manip.SetId );
        return manip.Apply( _eqpFile! );

    }

    public void Dispose()
    {
        _eqpFile?.Dispose();
        _eqpFile = null;
        _eqpManipulations.Clear();
    }
}