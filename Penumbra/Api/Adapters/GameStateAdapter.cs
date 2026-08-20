using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
using Penumbra.Api.Wrappers;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Services;

namespace Penumbra.Api;

public sealed class GameStateAdapterFactory(
    IpcObjectManager ipcManager,
    RedrawService redrawService,
    CutsceneService cutsceneService,
    DrawObjectState drawObjectState)
    : IAdapterFactory, IApiService
{
    public          IpcObjectManager IpcManager { get; } = ipcManager;
    public readonly RedrawService    RedrawService   = redrawService;
    public readonly CutsceneService  CutsceneService = cutsceneService;
    public readonly DrawObjectState  DrawObjectState = drawObjectState;

    public IpcObjectManager.BasicAdapter? CreateAdapter(string owner, object? data)
        => new GameStateAdapter(this, owner);
}

public sealed partial class GameStateAdapter(GameStateAdapterFactory parent, string owner)
    : IpcObjectManager.BasicAdapter(parent, owner, nameof(GameStateAdapter))
{
    public new GameStateAdapterFactory Parent
        => (GameStateAdapterFactory)base.Parent!;

    [AdapterMethod(GameStateWrapper.Method.GetLastGameObject)]
    private nint LastGameObject
        => Parent.DrawObjectState.LastGameObject;

    [AdapterMethod(GameStateWrapper.Method.GameObjectFromDrawObject)]
    private nint GameObjectFromDrawObject(nint drawObject)
        => Parent.DrawObjectState.TryGetValue(drawObject, out var data) ? data.Item1.Address : nint.Zero;

    [AdapterMethod(GameStateWrapper.Method.RedrawByIndex)]
    private void Redraw(int objectIndex, int redrawType)
        => Parent.RedrawService.RedrawObject(objectIndex, (RedrawType)redrawType);

    [AdapterMethod(GameStateWrapper.Method.ResolveCutsceneActor)]
    private short ResolveCutsceneActor(ushort objectIndex)
        => Parent.CutsceneService.GetParentIndex(objectIndex);

    [AdapterMethod(GameStateWrapper.Method.SetCutsceneActor)]
    private void SetCutsceneParentIndex(ushort changedObject, ushort newParent)
        => Parent.CutsceneService.SetParentIndex(changedObject, newParent);
}
