using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public unsafe class EqdpAccessoryHook : FastHook<EqdpAccessoryHook.Delegate>, IDisposable
{
    public delegate void Delegate(CharacterUtility* utility, EqdpEntry* entry, uint id, uint raceCode);

    private readonly MetaState _metaState;

    public EqdpAccessoryHook(HookManager hooks, MetaState metaState)
    {
        _metaState = metaState;
        Task = hooks.CreateHook<Delegate>("GetEqdpAccessoryEntry", Sigs.GetEqdpAccessoryEntry, Detour,
            metaState.Config.EnableMods && !HookOverrides.Instance.Meta.EqdpAccessoryHook);
        if (!HookOverrides.Instance.Meta.EqdpAccessoryHook)
            _metaState.Config.ModsEnabled += Toggle;
    }

    private void Detour(CharacterUtility* utility, EqdpEntry* entry, uint setId, uint raceCode)
    {
        Task.Result.Original(utility, entry, setId, raceCode);
        if (_metaState.EqdpCollection.TryPeek(out var collection)
         && collection is { Valid: true, ModCollection.MetaCache: { } cache })
            *entry = cache.Eqdp.ApplyFullEntry(new PrimaryId((ushort)setId), (GenderRace)raceCode, true, *entry);
        Penumbra.Log.Excessive(
            $"[GetEqdpAccessoryEntry] Invoked on 0x{(ulong)utility:X} with {setId}, {(GenderRace)raceCode}, returned {(ushort)*entry:B10}.");
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}
