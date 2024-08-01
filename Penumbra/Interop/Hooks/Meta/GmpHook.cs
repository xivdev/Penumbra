using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Lumina.Data.Parsing.Uld;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.Hooks.Meta;

public unsafe class GmpHook : FastHook<GmpHook.Delegate>, IDisposable
{
    public delegate ulong Delegate(CharacterUtility* characterUtility, ulong* outputEntry, ushort setId);

    private readonly MetaState _metaState;

    public GmpHook(HookManager hooks, MetaState metaState)
    {
        _metaState = metaState;
        Task = hooks.CreateHook<Delegate>("GetGmpEntry", Sigs.GetGmpEntry, Detour,
            metaState.Config.EnableMods && !HookOverrides.Instance.Meta.GmpHook);
        if (!HookOverrides.Instance.Meta.GmpHook)
            _metaState.Config.ModsEnabled += Toggle;
    }

    private ulong Detour(CharacterUtility* characterUtility, ulong* outputEntry, ushort setId)
    {
        ulong ret;
        if (_metaState.GmpCollection.TryPeek(out var collection)
         && collection.Collection is { Valid: true, ModCollection.MetaCache: { } cache }
         && cache.Gmp.TryGetValue(new GmpIdentifier(collection.Id), out var entry))
            ret = (*outputEntry) = entry.Entry.Enabled ? entry.Entry.Value : 0ul;
        else
            ret = Task.Result.Original(characterUtility, outputEntry, setId);

        Penumbra.Log.Excessive(
            $"[GetGmpFlags] Invoked on 0x{(ulong)characterUtility:X} for {setId} with 0x{(ulong)outputEntry:X} (={*outputEntry:X}), returned {ret:X10}.");
        return ret;
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}
