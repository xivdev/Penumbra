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
    private          CmpFile?                _cmpFile          = null;
    private readonly List< RspManipulation > _cmpManipulations = new();

    public void SetCmpFiles()
        => SetFile( _cmpFile, MetaIndex.HumanCmp );

    public static void ResetCmpFiles()
        => SetFile( null, MetaIndex.HumanCmp );

    public CharacterUtility.MetaList.MetaReverter TemporarilySetCmpFile()
        => TemporarilySetFile( _cmpFile, MetaIndex.HumanCmp );

    public void ResetCmp()
    {
        if( _cmpFile == null )
        {
            return;
        }

        _cmpFile.Reset( _cmpManipulations.Select( m => ( m.SubRace, m.Attribute ) ) );
        _cmpManipulations.Clear();
    }

    public bool ApplyMod( RspManipulation manip )
    {
        _cmpManipulations.AddOrReplace( manip );
        _cmpFile ??= new CmpFile();
        return manip.Apply( _cmpFile );
    }

    public bool RevertMod( RspManipulation manip )
    {
        if( _cmpManipulations.Remove( manip ) )
        {
            var def = CmpFile.GetDefault( manip.SubRace, manip.Attribute );
            manip = new RspManipulation( manip.SubRace, manip.Attribute, def );
            return manip.Apply( _cmpFile! );
        }

        return false;
    }

    public void DisposeCmp()
    {
        _cmpFile?.Dispose();
        _cmpFile = null;
        _cmpManipulations.Clear();
    }
}