using Dalamud.Plugin.Services;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Collections;

public sealed class CollectionAutoSelector : Luna.IRequiredService, IDisposable
{
    private readonly BehaviorConfig     _config;
    private readonly ActiveCollections  _collections;
    private readonly IClientState       _clientState;
    private readonly CollectionResolver _resolver;
    private readonly ObjectManager      _objects;

    public CollectionAutoSelector(BehaviorConfig config, ActiveCollections collections, IClientState clientState, CollectionResolver resolver,
        ObjectManager objects)
    {
        _config      = config;
        _collections = collections;
        _clientState = clientState;
        _resolver    = resolver;
        _objects     = objects;

        _config.AutoSelectCollectionChanged += OnAutoSelectCollectionChanged;
        if (_config.AutoSelectCollection)
            Attach();
    }

    private void OnAutoSelectCollectionChanged(bool newValue, bool oldValue)
    {
        if (newValue)
            Attach();
        else
            Detach();
    }

    public bool Disposed { get; private set; }

    private void Attach()
    {
        if (Disposed)
            return;

        _clientState.Login += OnLogin;
        Select();
    }

    private void OnLogin()
        => Select();

    private void Detach()
        => _clientState.Login -= OnLogin;

    private void Select()
    {
        if (!_objects[0].IsCharacter)
            return;

        var collection = _resolver.PlayerCollection();
        if (collection.Identity.Id == Guid.Empty)
        {
            Penumbra.Log.Debug("Not setting current collection because character has no mods assigned.");
        }
        else
        {
            Penumbra.Log.Debug($"Setting current collection to {collection.Identity.Identifier} through automatic collection selection.");
            _collections.SetCollection(collection, CollectionType.Current);
        }
    }


    public void Dispose()
    {
        if (Disposed)
            return;

        _config.AutoSelectCollectionChanged -= OnAutoSelectCollectionChanged;
        Disposed                            =  true;
        Detach();
    }
}
