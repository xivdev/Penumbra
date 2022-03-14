using System;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Meta.Manager;

public partial class MetaManager : IDisposable
{
    public MetaManagerEqp  Eqp  = new();
    public MetaManagerEqdp Eqdp = new();
    public MetaManagerGmp  Gmp  = new();
    public MetaManagerEst  Est  = new();
    public MetaManagerCmp  Cmp  = new();
    public MetaManagerImc  Imc;

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

    public bool TryGetValue( MetaManipulation manip, out Mod.Mod? mod )
    {
        mod = manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp  => Eqp.Manipulations.TryGetValue( manip.Eqp, out var m ) ? m : null,
            MetaManipulation.Type.Gmp  => Gmp.Manipulations.TryGetValue( manip.Gmp, out var m ) ? m : null,
            MetaManipulation.Type.Eqdp => Eqdp.Manipulations.TryGetValue( manip.Eqdp, out var m ) ? m : null,
            MetaManipulation.Type.Est  => Est.Manipulations.TryGetValue( manip.Est, out var m ) ? m : null,
            MetaManipulation.Type.Rsp  => Cmp.Manipulations.TryGetValue( manip.Rsp, out var m ) ? m : null,
            MetaManipulation.Type.Imc  => Imc.Manipulations.TryGetValue( manip.Imc, out var m ) ? m : null,
            _                          => throw new ArgumentOutOfRangeException(),
        };
        return mod != null;
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

    public bool ApplyMod( MetaManipulation m, Mod.Mod mod )
    {
        return m.ManipulationType switch
        {
            MetaManipulation.Type.Eqp     => Eqp.ApplyMod( m.Eqp, mod ),
            MetaManipulation.Type.Gmp     => Gmp.ApplyMod( m.Gmp, mod ),
            MetaManipulation.Type.Eqdp    => Eqdp.ApplyMod( m.Eqdp, mod ),
            MetaManipulation.Type.Est     => Est.ApplyMod( m.Est, mod ),
            MetaManipulation.Type.Rsp     => Cmp.ApplyMod( m.Rsp, mod ),
            MetaManipulation.Type.Imc     => Imc.ApplyMod( m.Imc, mod ),
            MetaManipulation.Type.Unknown => false,
            _                             => false,
        };
    }
}