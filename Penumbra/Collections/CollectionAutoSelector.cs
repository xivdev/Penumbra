using Dalamud.Plugin.Services;
using OtterGui.Services;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Collections;

public sealed class CollectionAutoSelector : IService, IDisposable
{
    private readonly Configuration      _config;
    private readonly ActiveCollections  _collections;
    private readonly IClientState       _clientState;
    private readonly CollectionResolver _resolver;
    private readonly ObjectManager      _objects;

    public CollectionAutoSelector(Configuration config, ActiveCollections collections, IClientState clientState, CollectionResolver resolver,
        ObjectManager objects)
    {
        _config      = config;
        _collections = collections;
        _clientState = clientState;
        _resolver    = resolver;
        _objects     = objects;

        if (_config.AutoSelectCollection)
            Attach();
    }

    public bool Disposed { get; private set; }

    public void SetAutomaticSelection(bool value)
    {
        _config.AutoSelectCollection = value;
        if (value)
            Attach();
        else
            Detach();
    }

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
        Penumbra.Log.Debug($"Setting current collection to {collection.Identity.Identifier} through automatic collection selection.");
        _collections.SetCollection(collection, CollectionType.Current);
    }


    public void Dispose()
    {
        if (Disposed)
            return;

        Disposed = true;
        Detach();
    }
}
