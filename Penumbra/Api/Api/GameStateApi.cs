using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Communication;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Api.Api;

public class GameStateApi : IPenumbraApiGameState, Luna.IApiService, IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly CollectionResolver  _collectionResolver;
    private readonly DrawObjectState     _drawObjectState;
    private readonly CutsceneService     _cutsceneService;
    private readonly ResourceLoader      _resourceLoader;

    public unsafe GameStateApi(CommunicatorService communicator, CollectionResolver collectionResolver, CutsceneService cutsceneService,
        ResourceLoader resourceLoader, DrawObjectState drawObjectState)
    {
        _communicator                  =  communicator;
        _collectionResolver            =  collectionResolver;
        _cutsceneService               =  cutsceneService;
        _resourceLoader                =  resourceLoader;
        _drawObjectState               =  drawObjectState;
        _resourceLoader.ResourceLoaded += OnResourceLoaded;
        _resourceLoader.PapRequested   += OnPapRequested;
        _communicator.CreatedCharacterBase.Subscribe(OnCreatedCharacterBase, Communication.CreatedCharacterBase.Priority.Api);
        _communicator.CreatingCharacterBase.Subscribe(OnCreatingCharacterBase, Communication.CreatingCharacterBase.Priority.Api);
    }

    private void OnCreatingCharacterBase(in CreatingCharacterBase.Arguments arguments)
        => CreatingCharacterBase?.Invoke(arguments.GameObject.Address, arguments.Collection.Identity.Id, arguments.ModelCharaId, arguments.Customize,
            arguments.EquipData);

    public unsafe void Dispose()
    {
        _resourceLoader.ResourceLoaded -= OnResourceLoaded;
        _resourceLoader.PapRequested   -= OnPapRequested;
        _communicator.CreatedCharacterBase.Unsubscribe(OnCreatedCharacterBase);
        _communicator.CreatingCharacterBase.Unsubscribe(OnCreatingCharacterBase);
    }

    public event CreatedCharacterBaseDelegate?       CreatedCharacterBase;
    public event GameObjectResourceResolvedDelegate? GameObjectResourceResolved;
    public event CreatingCharacterBaseDelegate?      CreatingCharacterBase;

    public unsafe (nint GameObject, (Guid Id, string Name) Collection) GetDrawObjectInfo(nint drawObject)
    {
        var data = _collectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        return (data.AssociatedGameObject, (Id: data.ModCollection.Identity.Id, Name: data.ModCollection.Identity.Name));
    }

    public int GetCutsceneParentIndex(int actorIdx)
        => _cutsceneService.GetParentIndex(actorIdx);

    public Func<int, int> GetCutsceneParentIndexFunc()
    {
        var weakRef = new WeakReference<CutsceneService>(_cutsceneService);
        return idx =>
        {
            if (!weakRef.TryGetTarget(out var c))
                throw new ObjectDisposedException("The underlying cutscene state storage of this IPC container was disposed.");

            return c.GetParentIndex(idx);
        };
    }

    public Func<nint, nint> GetGameObjectFromDrawObjectFunc()
    {
        var weakRef = new WeakReference<DrawObjectState>(_drawObjectState);
        return model =>
        {
            if (!weakRef.TryGetTarget(out var c))
                throw new ObjectDisposedException("The underlying draw object state storage of this IPC container was disposed.");

            return c.TryGetValue(model, out var data) ? data.Item1.Address : nint.Zero;
        };
    }

    public PenumbraApiEc SetCutsceneParentIndex(int copyIdx, int newParentIdx)
        => _cutsceneService.SetParentIndex(copyIdx, newParentIdx)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.InvalidArgument;

    private unsafe void OnResourceLoaded(ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath, ResolveData resolveData)
    {
        if (resolveData.AssociatedGameObject != nint.Zero && GameObjectResourceResolved != null)
        {
            var original = originalPath.ToString();
            GameObjectResourceResolved.Invoke(resolveData.AssociatedGameObject, original,
                manipulatedPath?.ToString() ?? original);
        }
    }

    private void OnPapRequested(Utf8GamePath originalPath, FullPath? manipulatedPath, ResolveData resolveData)
    {
        if (resolveData.AssociatedGameObject != nint.Zero && GameObjectResourceResolved != null)
        {
            var original = originalPath.ToString();
            GameObjectResourceResolved.Invoke(resolveData.AssociatedGameObject, original,
                manipulatedPath?.ToString() ?? original);
        }
    }

    private void OnCreatedCharacterBase(in CreatedCharacterBase.Arguments arguments)
        => CreatedCharacterBase?.Invoke(arguments.GameObject, arguments.Collection.Identity.Id, arguments.DrawObject);
}
