using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui.Filesystem;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public struct EqpCache : IDisposable
{
    private          ExpandedEqpFile?        _eqpFile          = null;
    private readonly List< EqpManipulation > _eqpManipulations = new();

    public EqpCache()
    {}

    public void SetFiles(MetaFileManager manager) 
        => manager.SetFile( _eqpFile, MetaIndex.Eqp );

    public static void ResetFiles(MetaFileManager manager)
        => manager.SetFile( null, MetaIndex.Eqp );

    public MetaList.MetaReverter TemporarilySetFiles(MetaFileManager manager)
        => manager.TemporarilySetFile( _eqpFile, MetaIndex.Eqp );

    public void Reset()
    {
        if (_eqpFile == null)
            return;

        _eqpFile.Reset(_eqpManipulations.Select(m => m.SetId));
        _eqpManipulations.Clear();
    }

    public bool ApplyMod( MetaFileManager manager, EqpManipulation manip )
    {
        _eqpManipulations.AddOrReplace( manip );
        _eqpFile ??= new ExpandedEqpFile(manager);
        return manip.Apply( _eqpFile );
    }

    public bool RevertMod( MetaFileManager manager, EqpManipulation manip )
    {
        var idx = _eqpManipulations.FindIndex( manip.Equals );
        if (idx < 0)
            return false;

        var def = ExpandedEqpFile.GetDefault( manager, manip.SetId );
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