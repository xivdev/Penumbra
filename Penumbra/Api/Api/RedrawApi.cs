using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Services;

namespace Penumbra.Api.Api;

public class RedrawApi(RedrawService redrawService, IFramework framework, CollectionManager collections, ObjectManager objects, ApiHelpers helpers) : IPenumbraApiRedraw, IApiService
{
    public void RedrawObject(int gameObjectIndex, RedrawType setting)
    {
        framework.RunOnFrameworkThread(() => redrawService.RedrawObject(gameObjectIndex, setting));
    }

    public void RedrawObject(string name, RedrawType setting)
    {
        framework.RunOnFrameworkThread(() => redrawService.RedrawObject(name, setting));
    }

    public void RedrawObject(IGameObject? gameObject, RedrawType setting)
    {
        framework.RunOnFrameworkThread(() => redrawService.RedrawObject(gameObject, setting));
    }

    public void RedrawAll(RedrawType setting)
    {
        framework.RunOnFrameworkThread(() => redrawService.RedrawAll(setting));
    }

    public void RedrawCollectionMembers(Guid collectionId, RedrawType setting)
    {
        if (!collections.Storage.ById(collectionId, out var collection))
            collection = ModCollection.Empty;
        framework.RunOnFrameworkThread(() =>
        {
            foreach (var actor in objects.Objects)
            {
                helpers.AssociatedCollection(actor.ObjectIndex, out var modCollection);
                if (collection == modCollection)
                {
                     redrawService.RedrawObject(actor.ObjectIndex, setting);
                }
            }
        });
    }

    public event GameObjectRedrawnDelegate? GameObjectRedrawn
    {
        add => redrawService.GameObjectRedrawn += value;
        remove => redrawService.GameObjectRedrawn -= value;
    }
}
