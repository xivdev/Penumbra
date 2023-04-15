using System;
using System.Collections.Generic;
using OtterGui.Filesystem;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public struct CmpCache : IDisposable
{
    private          CmpFile?                _cmpFile          = null;
    private readonly List< RspManipulation > _cmpManipulations = new();

    public CmpCache()
    {}

    public void SetFiles(CollectionCacheManager manager)
        => manager.SetFile( _cmpFile, MetaIndex.HumanCmp );

    public CharacterUtility.MetaList.MetaReverter TemporarilySetFiles(CollectionCacheManager manager)
        => manager.TemporarilySetFile( _cmpFile, MetaIndex.HumanCmp );

    public bool ApplyMod( CollectionCacheManager manager, RspManipulation manip )
    {
        _cmpManipulations.AddOrReplace( manip );
        _cmpFile ??= new CmpFile();
        return manip.Apply( _cmpFile );
    }

    public bool RevertMod( CollectionCacheManager manager, RspManipulation manip )
    {
        if (!_cmpManipulations.Remove(manip))
            return false;

        var def = CmpFile.GetDefault( manip.SubRace, manip.Attribute );
        manip = new RspManipulation( manip.SubRace, manip.Attribute, def );
        return manip.Apply( _cmpFile! );

    }

    public void Dispose()
    {
        _cmpFile?.Dispose();
        _cmpFile = null;
        _cmpManipulations.Clear();
    }
}