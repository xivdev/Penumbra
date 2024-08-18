using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Resources;

public sealed unsafe class LoadMtrl : FastHook<LoadMtrl.Delegate>
{
    private readonly GameState           _gameState;
    private readonly CommunicatorService _communicator;

    public LoadMtrl(HookManager hooks, GameState gameState, CommunicatorService communicator)
    {
        _gameState = gameState;
        _communicator = communicator;
        Task = hooks.CreateHook<Delegate>("Load Material", Sigs.LoadMtrl, Detour, !HookOverrides.Instance.Resources.LoadMtrl);
    }

    public delegate byte Delegate(MaterialResourceHandle* mtrlResourceHandle, void* unk1, byte unk2);

    private byte Detour(MaterialResourceHandle* handle, void* unk1, byte unk2)
    {
        var last     = _gameState.MtrlData.Value;
        var mtrlData = _gameState.LoadSubFileHelper((nint)handle);
        _gameState.MtrlData.Value = mtrlData;
        var ret = Task.Result.Original(handle, unk1, unk2);
        _gameState.MtrlData.Value = last;
        _communicator.MtrlLoaded.Invoke((nint)handle, mtrlData.AssociatedGameObject);
        return ret;
    }
}
