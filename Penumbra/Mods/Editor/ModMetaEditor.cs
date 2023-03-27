using System.Collections.Generic;
using System.Linq;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Mods;

public class ModMetaEditor
{
    private readonly ModManager _modManager;

    private readonly HashSet<ImcManipulation>  _imc  = new();
    private readonly HashSet<EqpManipulation>  _eqp  = new();
    private readonly HashSet<EqdpManipulation> _eqdp = new();
    private readonly HashSet<GmpManipulation>  _gmp  = new();
    private readonly HashSet<EstManipulation>  _est  = new();
    private readonly HashSet<RspManipulation>  _rsp  = new();

    public ModMetaEditor(ModManager modManager)
        => _modManager = modManager;

    public bool Changes { get; private set; } = false;

    public IReadOnlySet<ImcManipulation> Imc
        => _imc;

    public IReadOnlySet<EqpManipulation> Eqp
        => _eqp;

    public IReadOnlySet<EqdpManipulation> Eqdp
        => _eqdp;

    public IReadOnlySet<GmpManipulation> Gmp
        => _gmp;

    public IReadOnlySet<EstManipulation> Est
        => _est;

    public IReadOnlySet<RspManipulation> Rsp
        => _rsp;

    public bool CanAdd(MetaManipulation m)
    {
        return m.ManipulationType switch
        {
            MetaManipulation.Type.Imc  => !_imc.Contains(m.Imc),
            MetaManipulation.Type.Eqdp => !_eqdp.Contains(m.Eqdp),
            MetaManipulation.Type.Eqp  => !_eqp.Contains(m.Eqp),
            MetaManipulation.Type.Est  => !_est.Contains(m.Est),
            MetaManipulation.Type.Gmp  => !_gmp.Contains(m.Gmp),
            MetaManipulation.Type.Rsp  => !_rsp.Contains(m.Rsp),
            _                          => false,
        };
    }

    public bool Add(MetaManipulation m)
    {
        var added = m.ManipulationType switch
        {
            MetaManipulation.Type.Imc  => _imc.Add(m.Imc),
            MetaManipulation.Type.Eqdp => _eqdp.Add(m.Eqdp),
            MetaManipulation.Type.Eqp  => _eqp.Add(m.Eqp),
            MetaManipulation.Type.Est  => _est.Add(m.Est),
            MetaManipulation.Type.Gmp  => _gmp.Add(m.Gmp),
            MetaManipulation.Type.Rsp  => _rsp.Add(m.Rsp),
            _                          => false,
        };
        Changes |= added;
        return added;
    }

    public bool Delete(MetaManipulation m)
    {
        var deleted = m.ManipulationType switch
        {
            MetaManipulation.Type.Imc  => _imc.Remove(m.Imc),
            MetaManipulation.Type.Eqdp => _eqdp.Remove(m.Eqdp),
            MetaManipulation.Type.Eqp  => _eqp.Remove(m.Eqp),
            MetaManipulation.Type.Est  => _est.Remove(m.Est),
            MetaManipulation.Type.Gmp  => _gmp.Remove(m.Gmp),
            MetaManipulation.Type.Rsp  => _rsp.Remove(m.Rsp),
            _                          => false,
        };
        Changes |= deleted;
        return deleted;
    }

    public bool Change(MetaManipulation m)
        => Delete(m) && Add(m);

    public bool Set(MetaManipulation m)
        => Delete(m) | Add(m);

    public void Clear()
    {
        _imc.Clear();
        _eqp.Clear();
        _eqdp.Clear();
        _gmp.Clear();
        _est.Clear();
        _rsp.Clear();
        Changes = true;
    }

    public void Load(ISubMod mod)
        => Split(mod.Manipulations);

    public void Apply(Mod mod, int groupIdx, int optionIdx)
    {
        if (!Changes)
            return;

        _modManager.OptionEditor.OptionSetManipulations(mod, groupIdx, optionIdx, Recombine().ToHashSet());
        Changes = false;
    }

    private void Split(IEnumerable<MetaManipulation> manips)
    {
        Clear();
        foreach (var manip in manips)
        {
            switch (manip.ManipulationType)
            {
                case MetaManipulation.Type.Imc:
                    _imc.Add(manip.Imc);
                    break;
                case MetaManipulation.Type.Eqdp:
                    _eqdp.Add(manip.Eqdp);
                    break;
                case MetaManipulation.Type.Eqp:
                    _eqp.Add(manip.Eqp);
                    break;
                case MetaManipulation.Type.Est:
                    _est.Add(manip.Est);
                    break;
                case MetaManipulation.Type.Gmp:
                    _gmp.Add(manip.Gmp);
                    break;
                case MetaManipulation.Type.Rsp:
                    _rsp.Add(manip.Rsp);
                    break;
            }
        }

        Changes = false;
    }

    public IEnumerable<MetaManipulation> Recombine()
        => _imc.Select(m => (MetaManipulation)m)
            .Concat(_eqdp.Select(m => (MetaManipulation)m))
            .Concat(_eqp.Select(m => (MetaManipulation)m))
            .Concat(_est.Select(m => (MetaManipulation)m))
            .Concat(_gmp.Select(m => (MetaManipulation)m))
            .Concat(_rsp.Select(m => (MetaManipulation)m));
}
