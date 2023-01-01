using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using OtterGui;
using Penumbra.Collections;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Meta.Manager;

public partial class MetaManager : IDisposable, IEnumerable< KeyValuePair< MetaManipulation, IMod > >
{
    private readonly Dictionary< MetaManipulation, IMod > _manipulations = new();
    private readonly ModCollection                        _collection;

    public bool TryGetValue( MetaManipulation manip, [NotNullWhen( true )] out IMod? mod )
        => _manipulations.TryGetValue( manip, out mod );

    public int Count
        => _manipulations.Count;

    public IReadOnlyCollection< MetaManipulation > Manipulations
        => _manipulations.Keys;

    public IEnumerator< KeyValuePair< MetaManipulation, IMod > > GetEnumerator()
        => _manipulations.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public MetaManager( ModCollection collection )
    {
        _collection = collection;
        SetupImcDelegate();
        if( !Penumbra.CharacterUtility.Ready )
        {
            Penumbra.CharacterUtility.LoadingFinished += ApplyStoredManipulations;
        }
    }

    public void SetFiles()
    {
        SetEqpFiles();
        SetEqdpFiles();
        SetGmpFiles();
        SetEstFiles();
        SetCmpFiles();
        SetImcFiles();
    }

    public void Reset()
    {
        ResetEqp();
        ResetEqdp();
        ResetGmp();
        ResetEst();
        ResetCmp();
        ResetImc();
        _manipulations.Clear();
    }

    public void Dispose()
    {
        _manipulations.Clear();
        Penumbra.CharacterUtility.LoadingFinished -= ApplyStoredManipulations;
        DisposeEqp();
        DisposeEqdp();
        DisposeCmp();
        DisposeGmp();
        DisposeEst();
        DisposeImc();
    }

    public bool ApplyMod( MetaManipulation manip, IMod mod )
    {
        if( _manipulations.ContainsKey( manip ) )
        {
            _manipulations.Remove( manip );
        }

        _manipulations[ manip ] = mod;

        if( !Penumbra.CharacterUtility.Ready )
        {
            return true;
        }

        // Imc manipulations do not require character utility,
        // but they do require the file space to be ready.
        return manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp     => ApplyMod( manip.Eqp ),
            MetaManipulation.Type.Gmp     => ApplyMod( manip.Gmp ),
            MetaManipulation.Type.Eqdp    => ApplyMod( manip.Eqdp ),
            MetaManipulation.Type.Est     => ApplyMod( manip.Est ),
            MetaManipulation.Type.Rsp     => ApplyMod( manip.Rsp ),
            MetaManipulation.Type.Imc     => ApplyMod( manip.Imc ),
            MetaManipulation.Type.Unknown => false,
            _                             => false,
        };
    }

    public bool RevertMod( MetaManipulation manip )
    {
        var ret = _manipulations.Remove( manip );
        if( !Penumbra.CharacterUtility.Ready )
        {
            return ret;
        }

        // Imc manipulations do not require character utility,
        // but they do require the file space to be ready.
        return manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp     => RevertMod( manip.Eqp ),
            MetaManipulation.Type.Gmp     => RevertMod( manip.Gmp ),
            MetaManipulation.Type.Eqdp    => RevertMod( manip.Eqdp ),
            MetaManipulation.Type.Est     => RevertMod( manip.Est ),
            MetaManipulation.Type.Rsp     => RevertMod( manip.Rsp ),
            MetaManipulation.Type.Imc     => RevertMod( manip.Imc ),
            MetaManipulation.Type.Unknown => false,
            _                             => false,
        };
    }

    // Use this when CharacterUtility becomes ready.
    private void ApplyStoredManipulations()
    {
        if( !Penumbra.CharacterUtility.Ready )
        {
            return;
        }

        var loaded = 0;
        foreach( var manip in Manipulations )
        {
            loaded += manip.ManipulationType switch
            {
                MetaManipulation.Type.Eqp     => ApplyMod( manip.Eqp ),
                MetaManipulation.Type.Gmp     => ApplyMod( manip.Gmp ),
                MetaManipulation.Type.Eqdp    => ApplyMod( manip.Eqdp ),
                MetaManipulation.Type.Est     => ApplyMod( manip.Est ),
                MetaManipulation.Type.Rsp     => ApplyMod( manip.Rsp ),
                MetaManipulation.Type.Imc     => ApplyMod( manip.Imc ),
                MetaManipulation.Type.Unknown => false,
                _                             => false,
            }
                ? 1
                : 0;
        }

        if( Penumbra.CollectionManager.Default == _collection )
        {
            SetFiles();
            Penumbra.ResidentResources.Reload();
        }

        Penumbra.CharacterUtility.LoadingFinished -= ApplyStoredManipulations;
        Penumbra.Log.Debug( $"{_collection.AnonymizedName}: Loaded {loaded} delayed meta manipulations." );
    }

    public void SetFile( CharacterUtility.Index index )
    {
        switch( index )
        {
            case CharacterUtility.Index.Eqp:
                SetFile( _eqpFile, index );
                break;
            case CharacterUtility.Index.Gmp:
                SetFile( _gmpFile, index );
                break;
            case CharacterUtility.Index.HumanCmp:
                SetFile( _cmpFile, index );
                break;
            case CharacterUtility.Index.FaceEst:
                SetFile( _estFaceFile, index );
                break;
            case CharacterUtility.Index.HairEst:
                SetFile( _estHairFile, index );
                break;
            case CharacterUtility.Index.HeadEst:
                SetFile( _estHeadFile, index );
                break;
            case CharacterUtility.Index.BodyEst:
                SetFile( _estBodyFile, index );
                break;
            default:
                var i = CharacterUtility.EqdpIndices.IndexOf( index );
                if( i != -1 )
                {
                    SetFile( _eqdpFiles[ i ], index );
                }

                break;
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private static unsafe void SetFile( MetaBaseFile? file, CharacterUtility.Index index )
    {
        if( file == null )
        {
            Penumbra.CharacterUtility.ResetResource( index );
        }
        else
        {
            Penumbra.CharacterUtility.SetResource( index, ( IntPtr )file.Data, file.Length );
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private static unsafe Interop.CharacterUtility.List.MetaReverter TemporarilySetFile( MetaBaseFile? file, CharacterUtility.Index index )
        => file == null
            ? Penumbra.CharacterUtility.TemporarilyResetResource( index )
            : Penumbra.CharacterUtility.TemporarilySetResource( index, ( IntPtr )file.Data, file.Length );
}