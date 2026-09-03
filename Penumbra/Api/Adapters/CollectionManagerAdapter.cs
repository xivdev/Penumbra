using Dalamud.Plugin.Ipc;
using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
using Penumbra.Api.Wrappers;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Services;

namespace Penumbra.Api;

public sealed class CollectionManagerAdapterFactory(
    IpcObjectManager ipcManager,
    CollectionManager collections,
    CollectionResolver resolver,
    ObjectManager objects,
    ActorManager actors,
    ModStorage mods,
    LunaLogger log,
    CommunicatorService communicator) : IAdapterFactory, IApiService
{
    public          IpcObjectManager    IpcManager { get; } = ipcManager;
    public readonly LunaLogger          Log          = log;
    public readonly ModStorage          Mods         = mods;
    public readonly CollectionManager   Collections  = collections;
    public readonly ObjectManager       Objects      = objects;
    public readonly ActorManager        Actors       = actors;
    public readonly CollectionResolver  Resolver     = resolver;
    public readonly CommunicatorService Communicator = communicator;

    public IpcObjectManager.IBasicAdapter CreateAdapter(string owner, object? _ = null)
        => new CollectionManagerAdapter(this, owner);
}

public sealed partial class CollectionManagerAdapter(CollectionManagerAdapterFactory parent, string owner)
    : IpcObjectManager.BasicAdapter(parent, owner, nameof(CollectionManagerAdapter)), IAdapterFactory, IpcObjectManager.IBasicAdapter
{
    public IpcObjectManager IpcManager
        => Parent.IpcManager;

    public new CollectionManagerAdapterFactory Parent
        => (CollectionManagerAdapterFactory)base.Parent!;

    [AdapterMethod(CollectionManagerWrapper.Method.Version, AlwaysAlive = true)]
    public override (int Major, int Minor) Version
        => (1, 0);

    [AdapterMethod(CollectionManagerWrapper.Method.Alive, AlwaysAlive = true)]
    public override bool Alive
        => base.Parent is not null;

    [AdapterMethod(CollectionManagerWrapper.Method.DisposedEvent, AlwaysAlive = true)]
    public event Action? Disposed;

    protected override void InvokeDisposed()
        => Disposed?.Invoke();

    [AdapterMethod(CollectionManagerWrapper.Method.ModSettingsChanged,
        SubscribeEvent = nameof(SubscribeModSettingChanged),
        UnsubscribeEvent = nameof(UnsubscribeModSettingChanged))]
    private event Action<int, Guid, string, bool>? ModSettingsChanged;

    [AdapterMethod(CollectionManagerWrapper.Method.CollectionChanged,
        SubscribeEvent = nameof(SubscribeCollectionChanged),
        UnsubscribeEvent = nameof(UnsubscribeCollectionChanged))]
    private event Action<int, Guid?, Guid?, string>? CollectionChanged;

    [AdapterMethod(CollectionManagerWrapper.Method.GetCurrent, DisposeOnFailure = true)]
    private IIdDataShareAdapter Current
        => CreateCollection(Parent.Collections.Active.Current);

    [AdapterMethod(CollectionManagerWrapper.Method.GetDefault, DisposeOnFailure = true)]
    private IIdDataShareAdapter Default
        => CreateCollection(Parent.Collections.Active.Default);

    [AdapterMethod(CollectionManagerWrapper.Method.GetInterface, DisposeOnFailure = true)]
    private IIdDataShareAdapter Interface
        => CreateCollection(Parent.Collections.Active.Interface);

    [AdapterMethod(CollectionManagerWrapper.Method.Count)]
    private int Count
        => Parent.Collections.Storage.Count;

    [AdapterMethod(CollectionManagerWrapper.Method.GetEnumerable)]
    private IEnumerable<IIdDataShareAdapter> GetEnumerable()
        => Parent.Collections.Storage.Select(c => CreateCollection(c));

    [AdapterMethod(CollectionManagerWrapper.Method.GetByIndex, DisposeOnFailure = true)]
    private IIdDataShareAdapter? ByIndex(int index)
        => CreateCollection(index < 0 || index >= Parent.Collections.Storage.Count ? null : Parent.Collections.Storage[index]);

    [AdapterMethod(CollectionManagerWrapper.Method.GetById, DisposeOnFailure = true)]
    private IIdDataShareAdapter? GetById(Guid identifier)
        => CreateCollection(Parent.Collections.Storage.ById(identifier, out var collection)
         || Parent.Collections.Temp.CollectionById(identifier, out collection)
                ? collection
                : null);

    [AdapterMethod(CollectionManagerWrapper.Method.GetByName, DisposeOnFailure = true)]
    private IIdDataShareAdapter? GetByName(string name)
        => CreateCollection(Parent.Collections.Storage.ByName(name, out var collection)
         || Parent.Collections.Temp.CollectionByName(name, out collection)
                ? collection
                : null);

    [AdapterMethod(CollectionManagerWrapper.Method.GetByIdentifier, DisposeOnFailure = true)]
    private IIdDataShareAdapter? GetByIdentifier(string identifier)
        => CreateCollection(Parent.Collections.Storage.ByIdentifier(identifier, out var collection) ? collection : null);

    [AdapterMethod(CollectionManagerWrapper.Method.GetForType, DisposeOnFailure = true)]
    private IIdDataShareAdapter? GetForType(int type)
        => CreateCollection(Parent.Collections.Active.ByType((CollectionType)type));

    [AdapterMethod(CollectionManagerWrapper.Method.TryGetForObject, DisposeOnFailure = true)]
    private unsafe IIdDataShareAdapter? TryGetForObject(int objectIndex, bool onlyIndividual)
    {
        var actor = Parent.Objects[objectIndex];
        if (!actor.Valid)
            return null;

        if (onlyIndividual)
        {
            var identifier = actor.GetIdentifier(Parent.Actors);
            if (!identifier.IsValid)
                return null;

            var collection = Parent.Collections.Active.Individual(identifier);
            return CreateCollection(collection);
        }

        var data = Parent.Resolver.IdentifyCollection(actor.AsObject, false);
        return CreateCollection(data.ModCollection);
    }

    [AdapterMethod(CollectionManagerWrapper.Method.GetTypeCollectionIdentity)]
    public (Guid Identifier, string Name, int Index)? TypeCollectionId(int type)
    {
        var collection = Parent.Collections.Active.ByType((CollectionType)type);
        if (collection is null)
            return null;

        return (collection.Identity.Id, collection.Identity.Name, collection.Identity.Index);
    }

    [AdapterMethod(CollectionManagerWrapper.Method.GetObjectCollectionIdentity)]
    public unsafe (Guid Identifier, string Name, int Index) ObjectCollectionId(int objectIndex)
    {
        var collection = Parent.Resolver.IdentifyCollection(Parent.Objects[objectIndex].AsObject, true).ModCollection;
        return (collection.Identity.Id, collection.Identity.Name, collection.Identity.Index);
    }

    [AdapterMethod(CollectionManagerWrapper.Method.GetByIndexIdentity)]
    public (Guid Identifier, string Name, int Index)? IdentityByIndex(int index)
    {
        if (index < 0 || index >= Parent.Collections.Storage.Count)
            return null;

        var collection = Parent.Collections.Storage[index];
        return (collection.Identity.Id, collection.Identity.Name, collection.Identity.Index);
    }

    [AdapterMethod(CollectionManagerWrapper.Method.GetByIdIdentity)]
    public (Guid Identifier, string Name, int Index)? IdentityById(Guid identifier)
    {
        if (!Parent.Collections.Storage.ById(identifier, out var collection)
         && !Parent.Collections.Temp.CollectionById(identifier, out collection))
            return null;

        return (collection.Identity.Id, collection.Identity.Name, collection.Identity.Index);
    }

    [AdapterMethod(CollectionManagerWrapper.Method.GetByNameIdentity)]
    public (Guid Identifier, string Name, int Index)? IdentityByName(string name)
    {
        if (!Parent.Collections.Storage.ByName(name, out var collection) && !Parent.Collections.Temp.CollectionByName(name, out collection))
            return null;

        return (collection.Identity.Id, collection.Identity.Name, collection.Identity.Index);
    }

    [AdapterMethod(CollectionManagerWrapper.Method.GetByIdentifierIdentity)]
    public (Guid Identifier, string Name, int Index)? IdentityByIdentifier(string identifier)
    {
        if (!Parent.Collections.Storage.ByIdentifier(identifier, out var collection))
            return null;

        return (collection.Identity.Id, collection.Identity.Name, collection.Identity.Index);
    }

    [AdapterMethod(CollectionManagerWrapper.Method.GetPlayerCollectionIdentity)]
    public (Guid Identifier, string Name, int Index) PlayerCollectionId
        => ObjectCollectionId(0);

    [AdapterMethod(CollectionManagerWrapper.Method.GetPlayerCollection, DisposeOnFailure = true)]
    public IIdDataShareAdapter? PlayerCollection
        => TryGetForObject(0, false);

    [AdapterMethod(CollectionManagerWrapper.Method.GetNames)]
    private IEnumerable<(Guid Identifier, string Name, int Index)> GetNames()
        => Parent.Collections.Storage.Select(collection => (collection.Identity.Id, collection.Identity.Name, collection.Identity.Index));

    [AdapterMethod(CollectionManagerWrapper.Method.CheckCurrentChangedItems)]
    private IEnumerable<ModIdentifier> CheckCurrentChangedItems(string itemName)
    {
        if (!Parent.Collections.Active.Current.ChangedItems.TryGetValue(itemName, out var data))
            return [];

        return data.Item1.Select(m => new ModIdentifier(m is Mod mod ? mod.Identifier : string.Empty, m.Name));
    }

    [AdapterMethod(CollectionManagerWrapper.Method.RemoveAllTemporarySettings)]
    private int RemoveAllTemporarySettingsObject(Guid collectionId, int key)
    {
        if (!Parent.Collections.Storage.ById(collectionId, out var collection))
            return 0;

        var numRemoved = Parent.Collections.Editor.ClearTemporarySettings(collection, key);
        return numRemoved;
    }

    [AdapterMethod(CollectionManagerWrapper.Method.RemoveAllTemporarySettingsObject)]
    private unsafe int RemoveAllTemporarySettingsObject(int objectIndex, int key)
    {
        var collection = Parent.Resolver.IdentifyCollection(Parent.Objects[objectIndex].AsObject, true).ModCollection;
        var numRemoved = Parent.Collections.Editor.ClearTemporarySettings(collection, key);
        return numRemoved;
    }

    [return: NotNullIfNotNull(nameof(collection))]
    private IIdDataShareAdapter? CreateCollection(ModCollection? collection, [CallerMemberName] string? callerName = null)
        => this.Create(Owner, collection, callerName);

    public IpcObjectManager.IBasicAdapter? CreateAdapter(string owner, object? collection)
        => collection is not ModCollection c ? null : new CollectionAdapter(this, c);

    private void SubscribeModSettingChanged()
    {
        Parent.Communicator.ModSettingChanged.Subscribe(OnModSettingChanged, ModSettingChanged.Priority.Api);
        Parent.Communicator.ModPathChanged.Subscribe(OnModPathChanged, ModPathChanged.Priority.ApiModSettings);
        Parent.Communicator.ModOptionChanged.Subscribe(OnModOptionChanged, ModOptionChanged.Priority.Api);
        SubscribedEvents.TryAdd(nameof(ModSettingsChanged));
    }

    private void UnsubscribeModSettingChanged()
    {
        Parent.Communicator.ModSettingChanged.Unsubscribe(OnModSettingChanged);
        Parent.Communicator.ModPathChanged.Unsubscribe(OnModPathChanged);
        Parent.Communicator.ModOptionChanged.Unsubscribe(OnModOptionChanged);
        SubscribedEvents.TryRemove(nameof(ModSettingsChanged));
    }

    private void OnModPathChanged(in ModPathChanged.Arguments arguments)
    {
        if (arguments.Type is ModPathChangeType.Reloaded)
            TriggerSettingEditedForPlayer(arguments.Mod);
    }

    private void SubscribeCollectionChanged()
    {
        Parent.Communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.Api);
        SubscribedEvents.TryAdd(nameof(CollectionChanged));
    }

    private void UnsubscribeCollectionChanged()
    {
        Parent.Communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        SubscribedEvents.TryRemove(nameof(CollectionChanged));
    }

    private void OnCollectionChange(in CollectionChange.Arguments arguments)
        => CollectionChanged?.Invoke((int)arguments.Type, arguments.OldCollection?.Identity.Id, arguments.NewCollection?.Identity.Id,
            arguments.DisplayName);

    private void OnModSettingChanged(in ModSettingChanged.Arguments arguments)
    {
        try
        {
            ModSettingsChanged?.Invoke((int)arguments.Type, arguments.Collection.Identity.Id, arguments.Mod?.Identifier ?? string.Empty,
                arguments.Inherited);
        }
        catch (Exception ex)
        {
            Parent.Log.Error($"[{Owner}] Error invoking {nameof(CollectionManagerWrapper.ModSettingsChanged)} subscribers:\n{ex}");
        }
    }

    private void OnModOptionChanged(in ModOptionChanged.Arguments arguments)
    {
        switch (arguments.Type)
        {
            case ModOptionChangeType.GroupDeleted:
            case ModOptionChangeType.GroupMoved:
            case ModOptionChangeType.GroupTypeChanged:
            case ModOptionChangeType.PriorityChanged:
            case ModOptionChangeType.OptionDeleted:
            case ModOptionChangeType.OptionMoved:
            case ModOptionChangeType.OptionFilesChanged:
            case ModOptionChangeType.OptionFilesAdded:
            case ModOptionChangeType.OptionSwapsChanged:
            case ModOptionChangeType.OptionMetaChanged:
            case ModOptionChangeType.ConditionChanged:
                TriggerSettingEditedForPlayer(arguments.Mod);
                break;
        }
    }

    protected override void DisposeInternal()
        => UnsubscribeModSettingChanged();

    private void TriggerSettingEditedForPlayer(Mod mod)
    {
        var playerCollection = Parent.Resolver.PlayerCollection();
        var (_, parent) = playerCollection.GetActualSettings(mod.Index);
        OnModSettingChanged(new ModSettingChanged.Arguments(ModSettingChange.Edited, Parent.Resolver.PlayerCollection(), mod, Setting.Indefinite, -1, parent != playerCollection));
    }
}
