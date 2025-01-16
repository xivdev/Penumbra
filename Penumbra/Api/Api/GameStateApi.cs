using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Api.Api;

public class GameStateApi : IPenumbraApiGameState, IApiService, IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly CollectionResolver  _collectionResolver;
    private readonly CutsceneService     _cutsceneService;
    private readonly ResourceLoader      _resourceLoader;

    public unsafe GameStateApi(CommunicatorService communicator, CollectionResolver collectionResolver, CutsceneService cutsceneService,
        ResourceLoader resourceLoader)
    {
        _communicator                  =  communicator;
        _collectionResolver            =  collectionResolver;
        _cutsceneService               =  cutsceneService;
        _resourceLoader                =  resourceLoader;
        _resourceLoader.ResourceLoaded += OnResourceLoaded;
        _resourceLoader.PapRequested   += OnPapRequested;
        _communicator.CreatedCharacterBase.Subscribe(OnCreatedCharacterBase, Communication.CreatedCharacterBase.Priority.Api);
    }

    public unsafe void Dispose()
    {
        _resourceLoader.ResourceLoaded -= OnResourceLoaded;
        _resourceLoader.PapRequested   -= OnPapRequested;
        _communicator.CreatedCharacterBase.Unsubscribe(OnCreatedCharacterBase);
    }

    public event CreatedCharacterBaseDelegate?       CreatedCharacterBase;
    public event GameObjectResourceResolvedDelegate? GameObjectResourceResolved;

    public event CreatingCharacterBaseDelegate? CreatingCharacterBase
    {
        add
        {
            if (value == null)
                return;

            _communicator.CreatingCharacterBase.Subscribe(new Action<nint, Guid, nint, nint, nint>(value),
                Communication.CreatingCharacterBase.Priority.Api);
        }
        remove
        {
            if (value == null)
                return;

            _communicator.CreatingCharacterBase.Unsubscribe(new Action<nint, Guid, nint, nint, nint>(value));
        }
    }

    public unsafe (nint GameObject, (Guid Id, string Name) Collection) GetDrawObjectInfo(nint drawObject)
    {
        var data = _collectionResolver.IdentifyCollection((DrawObject*)drawObject, true);
        return (data.AssociatedGameObject, (Id: data.ModCollection.Identity.Id, Name: data.ModCollection.Identity.Name));
    }

    public int GetCutsceneParentIndex(int actorIdx)
        => _cutsceneService.GetParentIndex(actorIdx);

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

    private void OnCreatedCharacterBase(nint gameObject, ModCollection collection, nint drawObject)
        => CreatedCharacterBase?.Invoke(gameObject, collection.Identity.Id, drawObject);
}
