using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    private readonly ExpandedEqdpFile?[] _eqdpFiles = new ExpandedEqdpFile[CharacterUtilityData.EqdpIndices.Length]; // TODO: female Hrothgar

    private readonly List< EqdpManipulation > _eqdpManipulations = new();

    public void SetEqdpFiles()
    {
        for( var i = 0; i < CharacterUtilityData.EqdpIndices.Length; ++i )
        {
            SetFile( _eqdpFiles[ i ], CharacterUtilityData.EqdpIndices[ i ] );
        }
    }

    public CharacterUtility.MetaList.MetaReverter? TemporarilySetEqdpFile( GenderRace genderRace, bool accessory )
    {
        var idx = CharacterUtilityData.EqdpIdx( genderRace, accessory );
        if( ( int )idx != -1 )
        {
            var i = CharacterUtilityData.EqdpIndices.IndexOf( idx );
            if( i != -1 )
            {
                return TemporarilySetFile( _eqdpFiles[ i ], idx );
            }
        }

        return null;
    }

    public static void ResetEqdpFiles()
    {
        foreach( var idx in CharacterUtilityData.EqdpIndices )
        {
            SetFile( null, idx );
        }
    }

    public void ResetEqdp()
    {
        foreach( var file in _eqdpFiles.OfType< ExpandedEqdpFile >() )
        {
            var relevant = CharacterUtility.RelevantIndices[ file.Index.Value ];
            file.Reset( _eqdpManipulations.Where( m => m.FileIndex() == relevant ).Select( m => ( int )m.SetId ) );
        }

        _eqdpManipulations.Clear();
    }

    public bool ApplyMod( EqdpManipulation manip )
    {
        _eqdpManipulations.AddOrReplace( manip );
        var file = _eqdpFiles[ Array.IndexOf( CharacterUtilityData.EqdpIndices, manip.FileIndex() ) ] ??=
            new ExpandedEqdpFile( Names.CombinedRace( manip.Gender, manip.Race ), manip.Slot.IsAccessory() ); // TODO: female Hrothgar
        return manip.Apply( file );
    }

    public bool RevertMod( EqdpManipulation manip )
    {
        if( _eqdpManipulations.Remove( manip ) )
        {
            var def  = ExpandedEqdpFile.GetDefault( Names.CombinedRace( manip.Gender, manip.Race ), manip.Slot.IsAccessory(), manip.SetId );
            var file = _eqdpFiles[ Array.IndexOf( CharacterUtilityData.EqdpIndices, manip.FileIndex() ) ]!;
            manip = new EqdpManipulation( def, manip.Slot, manip.Gender, manip.Race, manip.SetId );
            return manip.Apply( file );
        }

        return false;
    }

    public ExpandedEqdpFile? EqdpFile( GenderRace race, bool accessory )
        => _eqdpFiles
            [ Array.IndexOf( CharacterUtilityData.EqdpIndices, CharacterUtilityData.EqdpIdx( race, accessory ) ) ]; // TODO: female Hrothgar

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