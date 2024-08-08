using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;

namespace Penumbra.Interop.Hooks.PostProcessing;

// TODO: "SetupScaling" does not seem to only set up scaling -> find a better name?
public unsafe class HumanSetupScalingHook : FastHook<HumanSetupScalingHook.Delegate>
{
    private const int ReplacementCapacity = 2;

    public event EventDelegate? SetupReplacements;

    public HumanSetupScalingHook(HookManager hooks, CharacterBaseVTables vTables)
        => Task = hooks.CreateHook<Delegate>("Human.SetupScaling", vTables.HumanVTable[58], Detour,
            !HookOverrides.Instance.PostProcessing.HumanSetupScaling);

    private void Detour(CharacterBase* drawObject, uint slotIndex)
    {
        Span<Replacement> replacements    = stackalloc Replacement[ReplacementCapacity];
        var               numReplacements = 0;
        IDisposable?      pbdDisposable   = null;
        object?           shpkLock        = null;
        var               releaseLock     = false;

        try
        {
            SetupReplacements?.Invoke(drawObject, slotIndex, replacements, ref numReplacements, ref pbdDisposable, ref shpkLock);
            if (shpkLock != null)
            {
                Monitor.Enter(shpkLock);
                releaseLock = true;
            }

            for (var i = 0; i < numReplacements; ++i)
                *(nint*)replacements[i].AddressToReplace = replacements[i].ValueToSet;
            Task.Result.Original(drawObject, slotIndex);
        }
        finally
        {
            for (var i = numReplacements; i-- > 0;)
                *(nint*)replacements[i].AddressToReplace = replacements[i].ValueToRestore;
            if (releaseLock)
                Monitor.Exit(shpkLock!);
            pbdDisposable?.Dispose();
        }
    }

    public delegate void Delegate(CharacterBase* drawObject, uint slotIndex);

    public delegate void EventDelegate(CharacterBase* drawObject, uint slotIndex, Span<Replacement> replacements, ref int numReplacements,
        ref IDisposable? pbdDisposable, ref object? shpkLock);

    public readonly record struct Replacement(nint AddressToReplace, nint ValueToSet, nint ValueToRestore);
}
