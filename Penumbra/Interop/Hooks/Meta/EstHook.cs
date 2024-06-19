using OtterGui.Services;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.Hooks.Meta;

public class EstHook : FastHook<EstHook.Delegate>, IDisposable
{
    public delegate EstEntry Delegate(uint id, int estType, uint genderRace);

    private readonly MetaState _metaState;

    public EstHook(HookManager hooks, MetaState metaState)
    {
        _metaState                    =  metaState;
        Task                          =  hooks.CreateHook<Delegate>("GetEstEntry", "44 8B C9 83 EA ?? 74", Detour, metaState.Config.EnableMods);
        _metaState.Config.ModsEnabled += Toggle;
    }

    private EstEntry Detour(uint genderRace, int estType, uint id)
    {
        EstEntry ret;
        if (_metaState.EstCollection.TryPeek(out var collection)
         && collection is { Valid: true, ModCollection.MetaCache: { } cache }
         && cache.Est.TryGetValue(Convert(genderRace, estType, id), out var entry))
            ret = entry.Entry;
        else
            ret = Task.Result.Original(genderRace, estType, id);

        Penumbra.Log.Excessive($"[GetEstEntry] Invoked with {genderRace}, {estType}, {id}, returned {ret.Value}.");
        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static EstIdentifier Convert(uint genderRace, int estType, uint id)
    {
        var i  = new PrimaryId((ushort)id);
        var gr = (GenderRace)genderRace;
        var type = estType switch
        {
            1 => EstType.Face,
            2 => EstType.Hair,
            3 => EstType.Head,
            4 => EstType.Body,
            _ => (EstType)0,
        };
        return new EstIdentifier(i, type, gr);
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}
