using System.Collections.Frozen;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Editor;

public class ModMetaEditor(ModManager modManager)
{
    private readonly HashSet<ImcManipulation>       _imc       = [];
    private readonly HashSet<EqpManipulation>       _eqp       = [];
    private readonly HashSet<EqdpManipulation>      _eqdp      = [];
    private readonly HashSet<GmpManipulation>       _gmp       = [];
    private readonly HashSet<EstManipulation>       _est       = [];
    private readonly HashSet<RspManipulation>       _rsp       = [];
    private readonly HashSet<GlobalEqpManipulation> _globalEqp = [];

    public sealed class OtherOptionData : HashSet<string>
    {
        public int TotalCount;

        public new void Clear()
        {
            TotalCount = 0;
            base.Clear();
        }
    }

    public readonly FrozenDictionary<MetaManipulation.Type, OtherOptionData> OtherData =
        Enum.GetValues<MetaManipulation.Type>().ToFrozenDictionary(t => t, _ => new OtherOptionData());

    public bool Changes { get; private set; }

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

    public IReadOnlySet<GlobalEqpManipulation> GlobalEqp
        => _globalEqp;

    public bool CanAdd(MetaManipulation m)
    {
        return m.ManipulationType switch
        {
            MetaManipulation.Type.Imc       => !_imc.Contains(m.Imc),
            MetaManipulation.Type.Eqdp      => !_eqdp.Contains(m.Eqdp),
            MetaManipulation.Type.Eqp       => !_eqp.Contains(m.Eqp),
            MetaManipulation.Type.Est       => !_est.Contains(m.Est),
            MetaManipulation.Type.Gmp       => !_gmp.Contains(m.Gmp),
            MetaManipulation.Type.Rsp       => !_rsp.Contains(m.Rsp),
            MetaManipulation.Type.GlobalEqp => !_globalEqp.Contains(m.GlobalEqp),
            _                               => false,
        };
    }

    public bool Add(MetaManipulation m)
    {
        var added = m.ManipulationType switch
        {
            MetaManipulation.Type.Imc       => _imc.Add(m.Imc),
            MetaManipulation.Type.Eqdp      => _eqdp.Add(m.Eqdp),
            MetaManipulation.Type.Eqp       => _eqp.Add(m.Eqp),
            MetaManipulation.Type.Est       => _est.Add(m.Est),
            MetaManipulation.Type.Gmp       => _gmp.Add(m.Gmp),
            MetaManipulation.Type.Rsp       => _rsp.Add(m.Rsp),
            MetaManipulation.Type.GlobalEqp => _globalEqp.Add(m.GlobalEqp),
            _                               => false,
        };
        Changes |= added;
        return added;
    }

    public bool Delete(MetaManipulation m)
    {
        var deleted = m.ManipulationType switch
        {
            MetaManipulation.Type.Imc       => _imc.Remove(m.Imc),
            MetaManipulation.Type.Eqdp      => _eqdp.Remove(m.Eqdp),
            MetaManipulation.Type.Eqp       => _eqp.Remove(m.Eqp),
            MetaManipulation.Type.Est       => _est.Remove(m.Est),
            MetaManipulation.Type.Gmp       => _gmp.Remove(m.Gmp),
            MetaManipulation.Type.Rsp       => _rsp.Remove(m.Rsp),
            MetaManipulation.Type.GlobalEqp => _globalEqp.Remove(m.GlobalEqp),
            _                               => false,
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
        _globalEqp.Clear();
        Changes = true;
    }

    public void Load(Mod mod, IModDataContainer currentOption)
    {
        foreach (var type in Enum.GetValues<MetaManipulation.Type>())
            OtherData[type].Clear();

        foreach (var option in mod.AllDataContainers)
        {
            if (option == currentOption)
                continue;

            foreach (var manip in option.Manipulations)
            {
                var data = OtherData[manip.ManipulationType];
                ++data.TotalCount;
                data.Add(option.GetFullName());
            }
        }

        Split(currentOption.Manipulations);
    }

    public void Apply(IModDataContainer container)
    {
        if (!Changes)
            return;

        modManager.OptionEditor.SetManipulations(container, Recombine().ToHashSet());
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
                case MetaManipulation.Type.GlobalEqp:
                    _globalEqp.Add(manip.GlobalEqp);
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
            .Concat(_rsp.Select(m => (MetaManipulation)m))
            .Concat(_globalEqp.Select(m => (MetaManipulation)m));
}
