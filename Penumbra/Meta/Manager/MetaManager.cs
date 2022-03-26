using System;
using System.Runtime.CompilerServices;
using Penumbra.Collections;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Manager;

public partial class MetaManager : IDisposable
{
    public MetaManagerEqp  Eqp  = new();
    public MetaManagerEqdp Eqdp = new();
    public MetaManagerGmp  Gmp  = new();
    public MetaManagerEst  Est  = new();
    public MetaManagerCmp  Cmp  = new();
    public MetaManagerImc  Imc;

    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    public static unsafe void SetFile( MetaBaseFile? file, int index )
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

    public bool TryGetValue( MetaManipulation manip, out int modIdx )
    {
        modIdx = manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp  => Eqp.Manipulations.TryGetValue( manip.Eqp, out var m ) ? m : -1,
            MetaManipulation.Type.Gmp  => Gmp.Manipulations.TryGetValue( manip.Gmp, out var m ) ? m : -1,
            MetaManipulation.Type.Eqdp => Eqdp.Manipulations.TryGetValue( manip.Eqdp, out var m ) ? m : -1,
            MetaManipulation.Type.Est  => Est.Manipulations.TryGetValue( manip.Est, out var m ) ? m : -1,
            MetaManipulation.Type.Rsp  => Cmp.Manipulations.TryGetValue( manip.Rsp, out var m ) ? m : -1,
            MetaManipulation.Type.Imc  => Imc.Manipulations.TryGetValue( manip.Imc, out var m ) ? m : -1,
            _                          => throw new ArgumentOutOfRangeException(),
        };
        return modIdx != -1;
    }

    public int Count
        => Imc.Manipulations.Count
          + Eqdp.Manipulations.Count
          + Cmp.Manipulations.Count
          + Gmp.Manipulations.Count
          + Est.Manipulations.Count
          + Eqp.Manipulations.Count;

    public MetaManager( ModCollection collection )
        => Imc = new MetaManagerImc( collection );

    public void SetFiles()
    {
        Eqp.SetFiles();
        Eqdp.SetFiles();
        Gmp.SetFiles();
        Est.SetFiles();
        Cmp.SetFiles();
        Imc.SetFiles();
    }

    public void Reset()
    {
        Eqp.Reset();
        Eqdp.Reset();
        Gmp.Reset();
        Est.Reset();
        Cmp.Reset();
        Imc.Reset();
    }

    public void Dispose()
    {
        Eqp.Dispose();
        Eqdp.Dispose();
        Gmp.Dispose();
        Est.Dispose();
        Cmp.Dispose();
        Imc.Dispose();
    }

    public bool ApplyMod( MetaManipulation m, int modIdx )
    {
        return m.ManipulationType switch
        {
            MetaManipulation.Type.Eqp     => Eqp.ApplyMod( m.Eqp, modIdx ),
            MetaManipulation.Type.Gmp     => Gmp.ApplyMod( m.Gmp, modIdx ),
            MetaManipulation.Type.Eqdp    => Eqdp.ApplyMod( m.Eqdp, modIdx ),
            MetaManipulation.Type.Est     => Est.ApplyMod( m.Est, modIdx ),
            MetaManipulation.Type.Rsp     => Cmp.ApplyMod( m.Rsp, modIdx ),
            MetaManipulation.Type.Imc     => Imc.ApplyMod( m.Imc, modIdx ),
            MetaManipulation.Type.Unknown => false,
            _                             => false,
        };
    }
}