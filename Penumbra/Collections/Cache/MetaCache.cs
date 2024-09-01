using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.Collections.Cache;

public class MetaCache(MetaFileManager manager, ModCollection collection)
{
    public readonly EqpCache       Eqp       = new(manager, collection);
    public readonly EqdpCache      Eqdp      = new(manager, collection);
    public readonly EstCache       Est       = new(manager, collection);
    public readonly GmpCache       Gmp       = new(manager, collection);
    public readonly RspCache       Rsp       = new(manager, collection);
    public readonly ImcCache       Imc       = new(manager, collection);
    public readonly GlobalEqpCache GlobalEqp = new();
    public          bool           IsDisposed { get; private set; }

    public int Count
        => Eqp.Count + Eqdp.Count + Est.Count + Gmp.Count + Rsp.Count + Imc.Count + GlobalEqp.Count;

    public IEnumerable<(IMetaIdentifier, IMod)> IdentifierSources
        => Eqp.Select(kvp => ((IMetaIdentifier)kvp.Key, kvp.Value.Source))
            .Concat(Eqdp.Select(kvp => ((IMetaIdentifier)kvp.Key, kvp.Value.Source)))
            .Concat(Est.Select(kvp => ((IMetaIdentifier)kvp.Key, kvp.Value.Source)))
            .Concat(Gmp.Select(kvp => ((IMetaIdentifier)kvp.Key, kvp.Value.Source)))
            .Concat(Rsp.Select(kvp => ((IMetaIdentifier)kvp.Key, kvp.Value.Source)))
            .Concat(Imc.Select(kvp => ((IMetaIdentifier)kvp.Key, kvp.Value.Source)))
            .Concat(GlobalEqp.Select(kvp => ((IMetaIdentifier)kvp.Key, kvp.Value)));

    public void Reset()
    {
        Eqp.Reset();
        Eqdp.Reset();
        Est.Reset();
        Gmp.Reset();
        Rsp.Reset();
        Imc.Reset();
        GlobalEqp.Clear();
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        Eqp.Dispose();
        Eqdp.Dispose();
        Est.Dispose();
        Gmp.Dispose();
        Rsp.Dispose();
        Imc.Dispose();
    }

    public bool TryGetMod(IMetaIdentifier identifier, [NotNullWhen(true)] out IMod? mod)
    {
        mod = null;
        return identifier switch
        {
            EqdpIdentifier i        => Eqdp.TryGetValue(i, out var p) && Convert(p, out mod),
            EqpIdentifier i         => Eqp.TryGetValue(i, out var p) && Convert(p,  out mod),
            EstIdentifier i         => Est.TryGetValue(i, out var p) && Convert(p,  out mod),
            GmpIdentifier i         => Gmp.TryGetValue(i, out var p) && Convert(p,  out mod),
            ImcIdentifier i         => Imc.TryGetValue(i, out var p) && Convert(p,  out mod),
            RspIdentifier i         => Rsp.TryGetValue(i, out var p) && Convert(p,  out mod),
            GlobalEqpManipulation i => GlobalEqp.TryGetValue(i, out mod),
            _                       => false,
        };

        static bool Convert<T>((IMod, T) pair, out IMod mod)
        {
            mod = pair.Item1;
            return true;
        }
    }

    public bool RevertMod(IMetaIdentifier identifier, [NotNullWhen(true)] out IMod? mod)
        => identifier switch
        {
            EqdpIdentifier i        => Eqdp.RevertMod(i, out mod),
            EqpIdentifier i         => Eqp.RevertMod(i, out mod),
            EstIdentifier i         => Est.RevertMod(i, out mod),
            GmpIdentifier i         => Gmp.RevertMod(i, out mod),
            ImcIdentifier i         => Imc.RevertMod(i, out mod),
            RspIdentifier i         => Rsp.RevertMod(i, out mod),
            GlobalEqpManipulation i => GlobalEqp.RevertMod(i, out mod),
            _                       => (mod = null) != null,
        };

    public bool ApplyMod(IMod mod, IMetaIdentifier identifier, object entry)
        => identifier switch
        {
            EqdpIdentifier i when entry is EqdpEntry e         => Eqdp.ApplyMod(mod, i, e),
            EqdpIdentifier i when entry is EqdpEntryInternal e => Eqdp.ApplyMod(mod, i, e.ToEntry(i.Slot)),
            EqpIdentifier i when entry is EqpEntry e           => Eqp.ApplyMod(mod, i, e),
            EqpIdentifier i when entry is EqpEntryInternal e   => Eqp.ApplyMod(mod, i, e.ToEntry(i.Slot)),
            EstIdentifier i when entry is EstEntry e           => Est.ApplyMod(mod, i, e),
            GmpIdentifier i when entry is GmpEntry e           => Gmp.ApplyMod(mod, i, e),
            ImcIdentifier i when entry is ImcEntry e           => Imc.ApplyMod(mod, i, e),
            RspIdentifier i when entry is RspEntry e           => Rsp.ApplyMod(mod, i, e),
            GlobalEqpManipulation i                            => GlobalEqp.ApplyMod(mod, i),
            _                                                  => false,
        };

    ~MetaCache()
        => Dispose();

    internal EqdpEntry GetEqdpEntry(GenderRace race, bool accessory, PrimaryId primaryId)
        => Eqdp.ApplyFullEntry(primaryId, race, accessory, Meta.Files.ExpandedEqdpFile.GetDefault(manager, race, accessory, primaryId));

    internal EstEntry GetEstEntry(EstType type, GenderRace genderRace, PrimaryId primaryId)
        => Est.GetEstEntry(new EstIdentifier(primaryId, type, genderRace));
}
