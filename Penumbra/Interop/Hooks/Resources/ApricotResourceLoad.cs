using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Services;
using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.Resources;

public sealed unsafe class ApricotResourceLoad : FastHook<ApricotResourceLoad.Delegate>
{
    private readonly GameState _gameState;

    public ApricotResourceLoad(HookManager hooks, GameState gameState)
    {
        _gameState = gameState;
        Task = hooks.CreateHook<Delegate>("Load Apricot Resource", Sigs.ApricotResourceLoad, Detour,
            !HookOverrides.Instance.Resources.ApricotResourceLoad);
    }

    public delegate byte Delegate(ResourceHandle* handle, nint unk1, byte unk2);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private byte Detour(ResourceHandle* handle, nint unk1, byte unk2)
    {
        var last = _gameState.AvfxData.Value;
        _gameState.AvfxData.Value = _gameState.LoadSubFileHelper((nint)handle);
        var ret = Task.Result.Original(handle, unk1, unk2);
        _gameState.AvfxData.Value = last;
        return ret;
    }
}
