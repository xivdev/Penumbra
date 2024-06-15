using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Interop.Hooks.Meta;

public unsafe class EqpHook : FastHook<EqpHook.Delegate>
{
    public delegate void Delegate(CharacterUtility* utility, EqpEntry* flags, CharacterArmor* armor);

    private readonly MetaState _metaState;

    public EqpHook(HookManager hooks, MetaState metaState)
    {
        _metaState = metaState;
        Task       = hooks.CreateHook<Delegate>("GetEqpFlags", "E8 ?? ?? ?? ?? 0F B6 44 24 ?? C0 E8", Detour, true);
    }

    private void Detour(CharacterUtility* utility, EqpEntry* flags, CharacterArmor* armor)
    {
        if (_metaState.EqpCollection.TryPeek(out var collection) && collection is { Valid: true, ModCollection.MetaCache: { } cache })
        {
            *flags = cache.Eqp.GetValues(armor);
            *flags = cache.GlobalEqp.Apply(*flags, armor);
        }
        else
        {
            Task.Result.Original(utility, flags, armor);
        }

        Penumbra.Log.Excessive($"[GetEqpFlags] Invoked on 0x{(nint)utility:X} with 0x{(ulong)armor:X}, returned 0x{(ulong)*flags:X16}.");
    }
}
