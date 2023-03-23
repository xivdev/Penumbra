using System;
using System.Collections.Generic;
using OtterGui.Filesystem;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    private EstFile? _estFaceFile = null;
    private EstFile? _estHairFile = null;
    private EstFile? _estBodyFile = null;
    private EstFile? _estHeadFile = null;

    private readonly List< EstManipulation > _estManipulations = new();

    public void SetEstFiles()
    {
        SetFile( _estFaceFile, MetaIndex.FaceEst );
        SetFile( _estHairFile, MetaIndex.HairEst );
        SetFile( _estBodyFile, MetaIndex.BodyEst );
        SetFile( _estHeadFile, MetaIndex.HeadEst );
    }

    public static void ResetEstFiles()
    {
        SetFile( null, MetaIndex.FaceEst );
        SetFile( null, MetaIndex.HairEst );
        SetFile( null, MetaIndex.BodyEst );
        SetFile( null, MetaIndex.HeadEst );
    }

    public CharacterUtility.MetaList.MetaReverter? TemporarilySetEstFile(EstManipulation.EstType type)
    {
        var (file, idx) = type switch
        {
            EstManipulation.EstType.Face => ( _estFaceFile, MetaIndex.FaceEst ),
            EstManipulation.EstType.Hair => ( _estHairFile, MetaIndex.HairEst ),
            EstManipulation.EstType.Body => ( _estBodyFile, MetaIndex.BodyEst ),
            EstManipulation.EstType.Head => ( _estHeadFile, MetaIndex.HeadEst ),
            _                            => ( null, 0 ),
        };
        
        return idx != 0 ? TemporarilySetFile( file, idx ) : null;
    }

    public void ResetEst()
    {
        _estFaceFile?.Reset();
        _estHairFile?.Reset();
        _estBodyFile?.Reset();
        _estHeadFile?.Reset();
        _estManipulations.Clear();
    }

    public bool ApplyMod( EstManipulation m )
    {
        _estManipulations.AddOrReplace( m );
        var file = m.Slot switch
        {
            EstManipulation.EstType.Hair => _estHairFile ??= new EstFile( EstManipulation.EstType.Hair ),
            EstManipulation.EstType.Face => _estFaceFile ??= new EstFile( EstManipulation.EstType.Face ),
            EstManipulation.EstType.Body => _estBodyFile ??= new EstFile( EstManipulation.EstType.Body ),
            EstManipulation.EstType.Head => _estHeadFile ??= new EstFile( EstManipulation.EstType.Head ),
            _                            => throw new ArgumentOutOfRangeException(),
        };
        return m.Apply( file );
    }

    public bool RevertMod( EstManipulation m )
    {
        if( _estManipulations.Remove( m ) )
        {
            var def   = EstFile.GetDefault( m.Slot, Names.CombinedRace( m.Gender, m.Race ), m.SetId );
            var manip = new EstManipulation( m.Gender, m.Race, m.Slot, m.SetId, def );
            var file = m.Slot switch
            {
                EstManipulation.EstType.Hair => _estHairFile!,
                EstManipulation.EstType.Face => _estFaceFile!,
                EstManipulation.EstType.Body => _estBodyFile!,
                EstManipulation.EstType.Head => _estHeadFile!,
                _                            => throw new ArgumentOutOfRangeException(),
            };
            return manip.Apply( file );
        }

        return false;
    }

    public void DisposeEst()
    {
        _estFaceFile?.Dispose();
        _estHairFile?.Dispose();
        _estBodyFile?.Dispose();
        _estHeadFile?.Dispose();
        _estFaceFile = null;
        _estHairFile = null;
        _estBodyFile = null;
        _estHeadFile = null;
        _estManipulations.Clear();
    }
}