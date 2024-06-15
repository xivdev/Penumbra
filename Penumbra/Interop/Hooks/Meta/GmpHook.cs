using OtterGui.Services;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.Hooks.Meta;

public unsafe class GmpHook : FastHook<GmpHook.Delegate>
{
    public delegate nint Delegate(nint gmpResource, uint dividedHeadId);

    private readonly MetaState _metaState;

    private static readonly Finalizer StablePointer = new();

    public GmpHook(HookManager hooks, MetaState metaState)
    {
        _metaState = metaState;
        Task       = hooks.CreateHook<Delegate>("GetGmpEntry", "E8 ?? ?? ?? ?? 48 85 C0 74 ?? 43 8D 0C", Detour, true);
    }

    /// <remarks>
    /// This function returns a pointer to the correct block in the GMP file, if it exists - cf. <see cref="ExpandedEqpGmpBase"/>.
    /// To work around this, we just have a single stable ulong accessible and offset the pointer to this by the required distance,
    /// which is defined by the modulo of the original ID and the block size, if we return our own custom gmp entry.
    /// </remarks>
    private nint Detour(nint gmpResource, uint dividedHeadId)
    {
        nint ret;
        if (_metaState.GmpCollection is { Valid: true, ModCollection.MetaCache: { } cache }
         && cache.Gmp.TryGetValue(new GmpIdentifier(_metaState.UndividedGmpId), out var entry))
        {
            if (entry.Entry.Enabled)
            {
                *StablePointer.Pointer = entry.Entry.Value;
                // This function already gets the original ID divided by the block size, so we can compute the modulo with a single multiplication and addition.
                // We then go backwards from our pointer because this gets added by the calling functions.
                ret = (nint)(StablePointer.Pointer - (_metaState.UndividedGmpId.Id - dividedHeadId * ExpandedEqpGmpBase.BlockSize));
            }
            else
            {
                ret = nint.Zero;
            }
        }
        else
        {
            ret = Task.Result.Original(gmpResource, dividedHeadId);
        }

        Penumbra.Log.Excessive($"[GetGmpFlags] Invoked on 0x{gmpResource:X} with {dividedHeadId}, returned {ret:X10}.");
        return ret;
    }

    /// <summary> Allocate and clean up our single stable ulong pointer. </summary>
    private class Finalizer
    {
        public readonly ulong* Pointer = (ulong*)Marshal.AllocHGlobal(8);

        ~Finalizer()
        {
            Marshal.FreeHGlobal((nint)Pointer);
        }
    }
}
