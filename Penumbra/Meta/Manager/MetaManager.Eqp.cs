using System.Collections.Generic;
using System.Linq;
using OtterGui.Filesystem;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    private          ExpandedEqpFile?        _eqpFile          = null;
    private readonly List< EqpManipulation > _eqpManipulations = new();

    public void SetEqpFiles()
        => SetFile( _eqpFile, MetaIndex.Eqp );

    public static void ResetEqpFiles()
        => SetFile( null, MetaIndex.Eqp );

    public CharacterUtility.MetaList.MetaReverter TemporarilySetEqpFile()
        => TemporarilySetFile( _eqpFile, MetaIndex.Eqp );

    public void ResetEqp()
    {
        if( _eqpFile == null )
        {
            return;
        }

        _eqpFile.Reset( _eqpManipulations.Select( m => ( int )m.SetId ) );
        _eqpManipulations.Clear();
    }

    public bool ApplyMod( EqpManipulation manip )
    {
        _eqpManipulations.AddOrReplace( manip );
        _eqpFile ??= new ExpandedEqpFile();
        return manip.Apply( _eqpFile );
    }

    public bool RevertMod( EqpManipulation manip )
    {
        var idx = _eqpManipulations.FindIndex( manip.Equals );
        if( idx >= 0 )
        {
            var def = ExpandedEqpFile.GetDefault( manip.SetId );
            manip = new EqpManipulation( def, manip.Slot, manip.SetId );
            return manip.Apply( _eqpFile! );
        }

        return false;
    }

    public void DisposeEqp()
    {
        _eqpFile?.Dispose();
        _eqpFile = null;
        _eqpManipulations.Clear();
    }
}