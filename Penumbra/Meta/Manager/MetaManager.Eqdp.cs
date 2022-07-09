using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui.Filesystem;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    private readonly ExpandedEqdpFile?[] _eqdpFiles = new ExpandedEqdpFile?[CharacterUtility.NumEqdpFiles - 2]; // TODO: female Hrothgar

    private readonly List< EqdpManipulation > _eqdpManipulations = new();

    public void SetEqdpFiles()
    {
        for( var i = 0; i < CharacterUtility.EqdpIndices.Length; ++i )
        {
            SetFile( _eqdpFiles[ i ], CharacterUtility.EqdpIndices[ i ] );
        }
    }

    public static void ResetEqdpFiles()
    {
        foreach( var idx in CharacterUtility.EqdpIndices )
        {
            SetFile( null, idx );
        }
    }

    public void ResetEqdp()
    {
        foreach( var file in _eqdpFiles )
        {
            file?.Reset( _eqdpManipulations.Where( m => m.FileIndex() == file.Index ).Select( m => ( int )m.SetId ) );
        }

        _eqdpManipulations.Clear();
    }

    public bool ApplyMod( EqdpManipulation manip )
    {
        _eqdpManipulations.AddOrReplace( manip );
        var file = _eqdpFiles[ Array.IndexOf( CharacterUtility.EqdpIndices, manip.FileIndex() ) ] ??=
            new ExpandedEqdpFile( Names.CombinedRace( manip.Gender, manip.Race ), manip.Slot.IsAccessory() ); // TODO: female Hrothgar
        return manip.Apply( file );
    }

    public bool RevertMod( EqdpManipulation manip )
    {
        if( _eqdpManipulations.Remove( manip ) )
        {
            var def  = ExpandedEqdpFile.GetDefault( Names.CombinedRace( manip.Gender, manip.Race ), manip.Slot.IsAccessory(), manip.SetId );
            var file = _eqdpFiles[ Array.IndexOf( CharacterUtility.EqdpIndices, manip.FileIndex() ) ]!;
            manip = new EqdpManipulation( def, manip.Slot, manip.Gender, manip.Race, manip.SetId );
            return manip.Apply( file );
        }

        return false;
    }

    public ExpandedEqdpFile? EqdpFile( GenderRace race, bool accessory )
        => _eqdpFiles
            [ Array.IndexOf( CharacterUtility.EqdpIndices, CharacterUtility.EqdpIdx( race, accessory ) ) ]; // TODO: female Hrothgar

    public void DisposeEqdp()
    {
        for( var i = 0; i < _eqdpFiles.Length; ++i )
        {
            _eqdpFiles[ i ]?.Dispose();
            _eqdpFiles[ i ] = null;
        }

        _eqdpManipulations.Clear();
    }
}