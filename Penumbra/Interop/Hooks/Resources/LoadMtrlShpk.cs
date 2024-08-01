using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Resources;

public sealed unsafe class LoadMtrlShpk : FastHook<LoadMtrlShpk.Delegate>
{
    private readonly GameState           _gameState;
    private readonly CommunicatorService _communicator;

    public LoadMtrlShpk(HookManager hooks, GameState gameState, CommunicatorService communicator)
    {
        _gameState = gameState;
        _communicator = communicator;
        Task = hooks.CreateHook<Delegate>("Load Material Shaders", Sigs.LoadMtrlShpk, Detour, !HookOverrides.Instance.Resources.LoadMtrlShpk);
    }

    public delegate byte Delegate(MaterialResourceHandle* mtrlResourceHandle);

    private byte Detour(MaterialResourceHandle* handle)
    {
        var last     = _gameState.MtrlData.Value;
        var mtrlData = _gameState.LoadSubFileHelper((nint)handle);
        _gameState.MtrlData.Value = mtrlData;
        var ret = Task.Result.Original(handle);
        _gameState.MtrlData.Value = last;
        _communicator.MtrlShpkLoaded.Invoke((nint)handle, mtrlData.AssociatedGameObject);
        return ret;
    }
}
