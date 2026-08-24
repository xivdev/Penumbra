using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
using Penumbra.Api.Wrappers;
using Penumbra.Collections;
using Penumbra.Communication;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Api;

public sealed class GameStateAdapterFactory(
    IpcObjectManager ipcManager,
    RedrawService redrawService,
    CutsceneService cutsceneService,
    DrawObjectState drawObjectState,
    LunaLogger log,
    CommunicatorService communicator,
    ResourceLoader resourceLoader)
    : IAdapterFactory, IApiService
{
    public          IpcObjectManager    IpcManager { get; } = ipcManager;
    public readonly LunaLogger          Log             = log;
    public readonly RedrawService       RedrawService   = redrawService;
    public readonly CutsceneService     CutsceneService = cutsceneService;
    public readonly DrawObjectState     DrawObjectState = drawObjectState;
    public readonly CommunicatorService Communicator    = communicator;
    public readonly ResourceLoader      ResourceLoader  = resourceLoader;

    public IpcObjectManager.IBasicAdapter CreateAdapter(string owner, object? data)
        => new GameStateAdapter(this, owner);
}

public sealed partial class GameStateAdapter(GameStateAdapterFactory parent, string owner)
    : IpcObjectManager.BasicAdapter(parent, owner, nameof(GameStateAdapter)), IpcObjectManager.IBasicAdapter
{
    public new GameStateAdapterFactory Parent
        => (GameStateAdapterFactory)base.Parent!;

    [AdapterMethod(GameStateWrapper.Method.Version)]
    public override (int Major, int Minor) Version
        => (1, 0);

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
    {
        Parent.Log.Debug($"[{Owner}] Setting cutscene parent of actor {changedObject} to {newParent}...");
        Parent.CutsceneService.SetParentIndex(changedObject, newParent);
    }

    [AdapterMethod(GameStateWrapper.Method.CreatingCharacterBase,
        SubscribeEvent = nameof(SubscribeCreatingCharacterBase),
        UnsubscribeEvent = nameof(UnsubscribeCreatingCharacterBase))]
    private event Action<nint, Guid, nint, nint, nint>? CreatingCharacterBase;

    [AdapterMethod(GameStateWrapper.Method.CreatedCharacterBase,
        SubscribeEvent = nameof(SubscribeCreatedCharacterBase),
        UnsubscribeEvent = nameof(UnsubscribeCreatedCharacterBase))]
    private event Action<nint, Guid, nint>? CreatedCharacterBase;

    [AdapterMethod(GameStateWrapper.Method.GameObjectResourceResolved,
        SubscribeEvent = nameof(SubscribeGameObjectResourceResolved),
        UnsubscribeEvent = nameof(UnsubscribeGameObjectResourceResolved))]
    private event Action<nint, string, string>? GameObjectResourceResolved;

    protected override void DisposeInternal()
    {
        UnsubscribeCreatingCharacterBase();
        UnsubscribeCreatedCharacterBase();
        UnsubscribeGameObjectResourceResolved();
    }

    private void SubscribeCreatingCharacterBase()
    {
        Parent.Communicator.CreatingCharacterBase.Subscribe(OnCreatingCharacterBase, Communication.CreatingCharacterBase.Priority.Api);
        SubscribedEvents.TryAdd(nameof(CreatingCharacterBase));
    }

    private void UnsubscribeCreatingCharacterBase()
    {
        Parent.Communicator.CreatingCharacterBase.Unsubscribe(OnCreatingCharacterBase);
        SubscribedEvents.TryRemove(nameof(CreatingCharacterBase));
    }

    private void SubscribeCreatedCharacterBase()
    {
        Parent.Communicator.CreatedCharacterBase.Subscribe(OnCreatedCharacterBase, Communication.CreatedCharacterBase.Priority.Api);
        SubscribedEvents.TryAdd(nameof(CreatedCharacterBase));
    }

    private void UnsubscribeCreatedCharacterBase()
    {
        Parent.Communicator.CreatedCharacterBase.Unsubscribe(OnCreatedCharacterBase);
        SubscribedEvents.TryRemove(nameof(CreatedCharacterBase));
    }

    private unsafe void SubscribeGameObjectResourceResolved()
    {
        Parent.ResourceLoader.ResourceLoaded += OnResourceLoaded;
        Parent.ResourceLoader.PapRequested   += OnPapRequested;
        SubscribedEvents.TryAdd(nameof(GameObjectResourceResolved));
    }

    private unsafe void UnsubscribeGameObjectResourceResolved()
    {
        Parent.ResourceLoader.ResourceLoaded -= OnResourceLoaded;
        Parent.ResourceLoader.PapRequested   -= OnPapRequested;
        SubscribedEvents.TryRemove(nameof(GameObjectResourceResolved));
    }

    private void OnPapRequested(Utf8GamePath originalPath, FullPath? manipulatedPath, ResolveData resolveData)
    {
        if (resolveData.AssociatedGameObject == nint.Zero)
            return;

        var original = originalPath.ToString();
        try
        {
            GameObjectResourceResolved!.Invoke(resolveData.AssociatedGameObject, original, manipulatedPath?.ToString() ?? original);
        }
        catch (Exception ex)
        {
            Parent.Log.Error($"Error invoking {Owner}s {nameof(GameStateWrapper.GameObjectResourceResolved)} subscribers:\n{ex}");
        }
    }

    private unsafe void OnResourceLoaded(ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath, ResolveData resolveData)
        => OnPapRequested(originalPath, manipulatedPath, resolveData);

    private void OnCreatedCharacterBase(in CreatedCharacterBase.Arguments arguments)
    {
        try
        {
            CreatedCharacterBase!.Invoke(arguments.GameObject, arguments.Collection.Identity.Id, arguments.DrawObject);
        }
        catch (Exception ex)
        {
            Parent.Log.Error($"Error invoking {Owner}s {nameof(GameStateWrapper.CreatedCharacterBase)} subscribers:\n{ex}");
        }
    }

    private void OnCreatingCharacterBase(in CreatingCharacterBase.Arguments arguments)
    {
        try
        {
            CreatingCharacterBase!.Invoke(arguments.GameObject.Address, arguments.Collection.Identity.Id, arguments.ModelCharaId,
                arguments.Customize, arguments.EquipData);
        }
        catch (Exception ex)
        {
            Parent.Log.Error($"Error invoking {Owner}s {nameof(GameStateWrapper.CreatingCharacterBase)} subscribers:\n{ex}");
        }
    }
}
