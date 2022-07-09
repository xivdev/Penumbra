using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Logging;
using Penumbra.Collections;
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
        // Imc manipulations do not require character utility.
        if( manip.ManipulationType == MetaManipulation.Type.Imc )
        {
            return ApplyMod( manip.Imc );
        }

        if( !Penumbra.CharacterUtility.Ready )
        {
            return true;
        }

        return manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp     => ApplyMod( manip.Eqp ),
            MetaManipulation.Type.Gmp     => ApplyMod( manip.Gmp ),
            MetaManipulation.Type.Eqdp    => ApplyMod( manip.Eqdp ),
            MetaManipulation.Type.Est     => ApplyMod( manip.Est ),
            MetaManipulation.Type.Rsp     => ApplyMod( manip.Rsp ),
            MetaManipulation.Type.Unknown => false,
            _                             => false,
        };
    }

    public bool RevertMod( MetaManipulation manip )
    {
        var ret = _manipulations.Remove( manip );
        // Imc manipulations do not require character utility.
        if( manip.ManipulationType == MetaManipulation.Type.Imc )
        {
            return RevertMod( manip.Imc );
        }

        if( !Penumbra.CharacterUtility.Ready )
        {
            return ret;
        }

        return manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp     => RevertMod( manip.Eqp ),
            MetaManipulation.Type.Gmp     => RevertMod( manip.Gmp ),
            MetaManipulation.Type.Eqdp    => RevertMod( manip.Eqdp ),
            MetaManipulation.Type.Est     => RevertMod( manip.Est ),
            MetaManipulation.Type.Rsp     => RevertMod( manip.Rsp ),
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
        foreach( var manip in Manipulations.Where( m => m.ManipulationType != MetaManipulation.Type.Imc ) )
        {
            loaded += manip.ManipulationType switch
            {
                MetaManipulation.Type.Eqp     => ApplyMod( manip.Eqp ),
                MetaManipulation.Type.Gmp     => ApplyMod( manip.Gmp ),
                MetaManipulation.Type.Eqdp    => ApplyMod( manip.Eqdp ),
                MetaManipulation.Type.Est     => ApplyMod( manip.Est ),
                MetaManipulation.Type.Rsp     => ApplyMod( manip.Rsp ),
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
        PluginLog.Debug( "{Collection}: Loaded {Num} delayed meta manipulations.", _collection.Name, loaded );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private static unsafe void SetFile( MetaBaseFile? file, int index )
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
}