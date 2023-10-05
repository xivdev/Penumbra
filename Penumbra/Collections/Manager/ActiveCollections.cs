using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Services;
using Penumbra.UI;

namespace Penumbra.Collections.Manager;

public class ActiveCollectionData
{
    public ModCollection Current   { get; internal set; } = ModCollection.Empty;
    public ModCollection Default   { get; internal set; } = ModCollection.Empty;
    public ModCollection Interface { get; internal set; } = ModCollection.Empty;

    public readonly ModCollection?[] SpecialCollections = new ModCollection?[Enum.GetValues<Api.Enums.ApiCollectionType>().Length - 3];
}

public class ActiveCollections : ISavable, IDisposable
{
    public const int Version = 1;

    private readonly CollectionStorage    _storage;
    private readonly CommunicatorService  _communicator;
    private readonly SaveService          _saveService;
    private readonly ActiveCollectionData _data;
    private readonly ActorService         _actors;

    public ActiveCollections(Configuration config, CollectionStorage storage, ActorService actors, CommunicatorService communicator,
        SaveService saveService, ActiveCollectionData data)
    {
        _storage      = storage;
        _actors       = actors;
        _communicator = communicator;
        _saveService  = saveService;
        _data         = data;
        Current       = storage.DefaultNamed;
        Default       = storage.DefaultNamed;
        Interface     = storage.DefaultNamed;
        Individuals   = new IndividualCollections(actors, config, false);
        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ActiveCollections);
        LoadCollections();
        UpdateCurrentCollectionInUse();
        Individuals.Loaded += UpdateCurrentCollectionInUse;
    }

    public void Dispose()
        => _communicator.CollectionChange.Unsubscribe(OnCollectionChange);

    /// <summary> The collection currently selected for changing settings. </summary>
    public ModCollection Current
    {
        get => _data.Current;
        private set => _data.Current = value;
    }

    /// <summary> Whether the currently selected collection is used either directly via assignment or via inheritance. </summary>
    public bool CurrentCollectionInUse { get; private set; }

    /// <summary> The collection used for general file redirections and all characters not specifically named. </summary>
    public ModCollection Default
    {
        get => _data.Default;
        private set => _data.Default = value;
    }

    /// <summary> The collection used for all files categorized as UI files. </summary>
    public ModCollection Interface
    {
        get => _data.Interface;
        private set => _data.Interface = value;
    }

    /// <summary> The list of individual assignments. </summary>
    public readonly IndividualCollections Individuals;

    /// <summary> Get the collection assigned to an individual or Default if unassigned. </summary>
    public ModCollection Individual(ActorIdentifier identifier)
        => Individuals.TryGetCollection(identifier, out var c) ? c : Default;

    /// <summary> The list of group assignments. </summary>
    private ModCollection?[] SpecialCollections
        => _data.SpecialCollections;

    /// <summary> Return all actually assigned group assignments. </summary>
    public IEnumerable<KeyValuePair<CollectionType, ModCollection>> SpecialAssignments
    {
        get
        {
            for (var i = 0; i < SpecialCollections.Length; ++i)
            {
                var collection = SpecialCollections[i];
                if (collection != null)
                    yield return new KeyValuePair<CollectionType, ModCollection>((CollectionType)i, collection);
            }
        }
    }

    /// <inheritdoc cref="ByType(CollectionType, ActorIdentifier)"/>
    public ModCollection? ByType(CollectionType type)
        => ByType(type, ActorIdentifier.Invalid);

    /// <summary> Return the configured collection for the given type or null. </summary>
    public ModCollection? ByType(CollectionType type, ActorIdentifier identifier)
    {
        if (type.IsSpecial())
            return SpecialCollections[(int)type];

        return type switch
        {
            CollectionType.Default    => Default,
            CollectionType.Interface  => Interface,
            CollectionType.Current    => Current,
            CollectionType.Individual => identifier.IsValid && Individuals.TryGetValue(identifier, out var c) ? c : null,
            _                         => null,
        };
    }

    /// <summary> Create a special collection if it does not exist and set it to the current default. </summary>
    public bool CreateSpecialCollection(CollectionType collectionType)
    {
        if (!collectionType.IsSpecial() || SpecialCollections[(int)collectionType] != null)
            return false;

        SpecialCollections[(int)collectionType] = Default;
        _communicator.CollectionChange.Invoke(collectionType, null, Default, string.Empty);
        return true;
    }

    /// <summary> Remove a special collection if it exists </summary>
    public void RemoveSpecialCollection(CollectionType collectionType)
    {
        if (!collectionType.IsSpecial())
            return;

        var old = SpecialCollections[(int)collectionType];
        if (old == null)
            return;

        SpecialCollections[(int)collectionType] = null;
        _communicator.CollectionChange.Invoke(collectionType, old, null, string.Empty);
    }

    /// <summary>Create an individual collection if possible. </summary>
    public void CreateIndividualCollection(params ActorIdentifier[] identifiers)
    {
        if (Individuals.Add(identifiers, Default))
            _communicator.CollectionChange.Invoke(CollectionType.Individual, null, Default, Individuals.Last().DisplayName);
    }

    /// <summary> Remove an individual collection if it exists. </summary>
    public void RemoveIndividualCollection(int individualIndex)
    {
        if (individualIndex < 0 || individualIndex >= Individuals.Count)
            return;

        var (name, old) = Individuals[individualIndex];
        if (Individuals.Delete(individualIndex))
            _communicator.CollectionChange.Invoke(CollectionType.Individual, old, null, name);
    }

    /// <summary> Move an individual collection from one index to another. </summary>
    public void MoveIndividualCollection(int from, int to)
    {
        if (Individuals.Move(from, to))
            _saveService.DelaySave(this);
    }

    /// <summary> Set and create an active collection, can be used to set Default, Current, Interface, Special, or Individual collections. </summary>
    public void SetCollection(ModCollection? collection, CollectionType collectionType, ActorIdentifier[] identifiers)
    {
        if (collectionType is CollectionType.Individual && identifiers.Length > 0 && identifiers[0].IsValid)
        {
            var idx = Individuals.Index(identifiers[0]);
            if (idx >= 0)
            {
                if (collection == null)
                    RemoveIndividualCollection(idx);
                else
                    SetCollection(collection, collectionType, idx);
            }
            else if (collection != null)
            {
                CreateIndividualCollection(identifiers);
                SetCollection(collection, CollectionType.Individual, Individuals.Count - 1);
            }
        }
        else
        {
            if (collection == null)
            {
                RemoveSpecialCollection(collectionType);
            }
            else
            {
                CreateSpecialCollection(collectionType);
                SetCollection(collection, collectionType);
            }
        }
    }

    /// <summary> Set an active collection, can be used to set Default, Current, Interface, Special, or Individual collections. </summary>
    public void SetCollection(ModCollection collection, CollectionType collectionType, int individualIndex = -1)
    {
        var oldCollection = collectionType switch
        {
            CollectionType.Default   => Default,
            CollectionType.Interface => Interface,
            CollectionType.Current   => Current,
            CollectionType.Individual when individualIndex >= 0 && individualIndex < Individuals.Count => Individuals[individualIndex]
                .Collection,
            CollectionType.Individual         => null,
            _ when collectionType.IsSpecial() => SpecialCollections[(int)collectionType] ?? Default,
            _                                 => null,
        };

        if (oldCollection == null || collection == oldCollection || collection.Index >= _storage.Count)
            return;

        switch (collectionType)
        {
            case CollectionType.Default:
                Default = collection;
                break;
            case CollectionType.Interface:
                Interface = collection;
                break;
            case CollectionType.Current:
                Current = collection;
                break;
            case CollectionType.Individual:
                if (!Individuals.ChangeCollection(individualIndex, collection))
                    return;

                break;
            default:
                SpecialCollections[(int)collectionType] = collection;
                break;
        }

        UpdateCurrentCollectionInUse();
        _communicator.CollectionChange.Invoke(collectionType, oldCollection, collection,
            collectionType == CollectionType.Individual ? Individuals[individualIndex].DisplayName : string.Empty);
    }

    public string ToFilename(FilenameService fileNames)
        => fileNames.ActiveCollectionsFile;

    public string TypeName
        => "Active Collections";

    public string LogName(string _)
        => "to file";

    public void Save(StreamWriter writer)
    {
        var jObj = new JObject
        {
            { nameof(Version), Version },
            { nameof(Default), Default.Name },
            { nameof(Interface), Interface.Name },
            { nameof(Current), Current.Name },
        };
        foreach (var (type, collection) in SpecialCollections.WithIndex().Where(p => p.Value != null)
                     .Select(p => ((CollectionType)p.Index, p.Value!)))
            jObj.Add(type.ToString(), collection.Name);

        jObj.Add(nameof(Individuals), Individuals.ToJObject());
        using var j = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
        jObj.WriteTo(j);
    }

    private void UpdateCurrentCollectionInUse()
        => CurrentCollectionInUse = SpecialCollections
            .OfType<ModCollection>()
            .Prepend(Interface)
            .Prepend(Default)
            .Concat(Individuals.Assignments.Select(kvp => kvp.Collection))
            .SelectMany(c => c.GetFlattenedInheritance()).Contains(Current);

    /// <summary> Save if any of the active collections is changed and set new collections to Current. </summary>
    private void OnCollectionChange(CollectionType collectionType, ModCollection? oldCollection, ModCollection? newCollection, string _3)
    {
        if (collectionType is CollectionType.Inactive)
        {
            if (newCollection != null)
            {
                SetCollection(newCollection, CollectionType.Current);
            }
            else if (oldCollection != null)
            {
                if (oldCollection == Default)
                    SetCollection(ModCollection.Empty, CollectionType.Default);
                if (oldCollection == Interface)
                    SetCollection(ModCollection.Empty, CollectionType.Interface);
                if (oldCollection == Current)
                    SetCollection(Default.Index > ModCollection.Empty.Index ? Default : _storage.DefaultNamed, CollectionType.Current);

                for (var i = 0; i < SpecialCollections.Length; ++i)
                {
                    if (oldCollection == SpecialCollections[i])
                        SetCollection(ModCollection.Empty, (CollectionType)i);
                }

                for (var i = 0; i < Individuals.Count; ++i)
                {
                    if (oldCollection == Individuals[i].Collection)
                        SetCollection(ModCollection.Empty, CollectionType.Individual, i);
                }
            }
        }
        else if (collectionType is not CollectionType.Temporary)
        {
            _saveService.DelaySave(this);
        }
    }

    /// <summary>
    /// Load default, current, special, and character collections from config.
    /// If a collection does not exist anymore, reset it to an appropriate default.
    /// </summary>
    private void LoadCollections()
    {
        var configChanged = !Load(_saveService.FileNames, out var jObject);

        // Load the default collection. If the string does not exist take the Default name if no file existed or the Empty name if one existed.
        var defaultName = jObject[nameof(Default)]?.ToObject<string>()
         ?? (configChanged ? ModCollection.DefaultCollectionName : ModCollection.Empty.Name);
        if (!_storage.ByName(defaultName, out var defaultCollection))
        {
            Penumbra.Messager.NotificationMessage(
                $"Last choice of {TutorialService.DefaultCollection} {defaultName} is not available, reset to {ModCollection.Empty.Name}.", NotificationType.Warning);
            Default       = ModCollection.Empty;
            configChanged = true;
        }
        else
        {
            Default = defaultCollection;
        }

        // Load the interface collection. If no string is set, use the name of whatever was set as Default.
        var interfaceName = jObject[nameof(Interface)]?.ToObject<string>() ?? Default.Name;
        if (!_storage.ByName(interfaceName, out var interfaceCollection))
        {
            Penumbra.Messager.NotificationMessage(
                $"Last choice of {TutorialService.InterfaceCollection} {interfaceName} is not available, reset to {ModCollection.Empty.Name}.", NotificationType.Warning);
            Interface     = ModCollection.Empty;
            configChanged = true;
        }
        else
        {
            Interface = interfaceCollection;
        }

        // Load the current collection.
        var currentName = jObject[nameof(Current)]?.ToObject<string>() ?? Default.Name;
        if (!_storage.ByName(currentName, out var currentCollection))
        {
            Penumbra.Messager.NotificationMessage(
                $"Last choice of {TutorialService.SelectedCollection} {currentName} is not available, reset to {ModCollection.DefaultCollectionName}.", NotificationType.Warning);
            Current       = _storage.DefaultNamed;
            configChanged = true;
        }
        else
        {
            Current = currentCollection;
        }

        // Load special collections.
        foreach (var (type, name, _) in CollectionTypeExtensions.Special)
        {
            var typeName = jObject[type.ToString()]?.ToObject<string>();
            if (typeName != null)
            {
                if (!_storage.ByName(typeName, out var typeCollection))
                {
                    Penumbra.Messager.NotificationMessage($"Last choice of {name} Collection {typeName} is not available, removed.",
                        NotificationType.Warning);
                    configChanged = true;
                }
                else
                {
                    SpecialCollections[(int)type] = typeCollection;
                }
            }
        }

        configChanged |= ActiveCollectionMigration.MigrateIndividualCollections(_storage, Individuals, jObject);
        configChanged |= Individuals.ReadJObject(_saveService, this, jObject[nameof(Individuals)] as JArray, _storage);

        // Save any changes.
        if (configChanged)
            _saveService.ImmediateSave(this);
    }

    /// <summary>
    /// Read the active collection file into a jObject.
    /// Returns true if this is successful, false if the file does not exist or it is unsuccessful.
    /// </summary>
    public static bool Load(FilenameService fileNames, out JObject ret)
    {
        var file = fileNames.ActiveCollectionsFile;
        var jObj = BackupService.GetJObjectForFile(fileNames, file);
        if (jObj == null)
        {
            ret = new JObject();
            return false;
        }

        ret = jObj;
        return true;
    }

    public string RedundancyCheck(CollectionType type, ActorIdentifier id)
    {
        var checkAssignment = ByType(type, id);
        if (checkAssignment == null)
            return string.Empty;

        switch (type)
        {
            case CollectionType.Yourself:
                var yourself = ByType(CollectionType.Yourself);
                if (yourself == null)
                    return string.Empty;

                var racial = false;
                foreach (var race in Enum.GetValues<SubRace>().Skip(1))
                {
                    var m = ByType(CollectionTypeExtensions.FromParts(race, Gender.Male, false));
                    if (m != null && m != yourself)
                        return string.Empty;

                    var f = ByType(CollectionTypeExtensions.FromParts(race, Gender.Female, false));
                    if (f != null && f != yourself)
                        return string.Empty;

                    racial |= m != null || f != null;
                }

                var racialString = racial ? " and Racial Assignments" : string.Empty;
                var @base        = ByType(CollectionType.Default);
                var male         = ByType(CollectionType.MalePlayerCharacter);
                var female       = ByType(CollectionType.FemalePlayerCharacter);
                if (male == yourself && female == yourself)
                    return
                        $"Assignment is redundant due to overwriting Male Players and Female Players{racialString} with an identical collection.\nYou can remove it.";

                if (male == null)
                {
                    if (female == null && @base == yourself)
                        return
                            $"Assignment is redundant due to overwriting Base{racialString} with an identical collection.\nYou can remove it.";
                    if (female == yourself && @base == yourself)
                        return
                            $"Assignment is redundant due to overwriting Base and Female Players{racialString} with an identical collection.\nYou can remove it.";
                }
                else if (male == yourself && female == null && @base == yourself)
                {
                    return
                        $"Assignment is redundant due to overwriting Base and Male Players{racialString} with an identical collection.\nYou can remove it.";
                }

                break;
            // Check individual assignments. We can only be sure of redundancy for world-overlap or ownership overlap.
            case CollectionType.Individual:
                switch (id.Type)
                {
                    case IdentifierType.Player when id.HomeWorld != ushort.MaxValue:
                    {
                        var global = ByType(CollectionType.Individual, _actors.AwaitedService.CreatePlayer(id.PlayerName, ushort.MaxValue));
                        return global?.Index == checkAssignment.Index
                            ? "Assignment is redundant due to an identical Any-World assignment existing.\nYou can remove it."
                            : string.Empty;
                    }
                    case IdentifierType.Owned:
                        if (id.HomeWorld != ushort.MaxValue)
                        {
                            var global = ByType(CollectionType.Individual,
                                _actors.AwaitedService.CreateOwned(id.PlayerName, ushort.MaxValue, id.Kind, id.DataId));
                            if (global?.Index == checkAssignment.Index)
                                return "Assignment is redundant due to an identical Any-World assignment existing.\nYou can remove it.";
                        }

                        var unowned = ByType(CollectionType.Individual, _actors.AwaitedService.CreateNpc(id.Kind, id.DataId));
                        return unowned?.Index == checkAssignment.Index
                            ? "Assignment is redundant due to an identical unowned NPC assignment existing.\nYou can remove it."
                            : string.Empty;
                }

                break;
            // Children and Elderly are redundant if they are identical to both Male NPCs and Female NPCs, or if they are unassigned to Default.
            case CollectionType.NonPlayerChild:
            case CollectionType.NonPlayerElderly:
                var maleNpc     = ByType(CollectionType.MaleNonPlayerCharacter);
                var femaleNpc   = ByType(CollectionType.FemaleNonPlayerCharacter);
                var collection1 = CollectionType.MaleNonPlayerCharacter;
                var collection2 = CollectionType.FemaleNonPlayerCharacter;
                if (maleNpc == null)
                {
                    maleNpc = Default;
                    if (maleNpc.Index != checkAssignment.Index)
                        return string.Empty;

                    collection1 = CollectionType.Default;
                }

                if (femaleNpc == null)
                {
                    femaleNpc = Default;
                    if (femaleNpc.Index != checkAssignment.Index)
                        return string.Empty;

                    collection2 = CollectionType.Default;
                }

                return collection1 == collection2
                    ? $"Assignment is currently redundant due to overwriting {collection1.ToName()} with an identical collection.\nYou can remove them."
                    : $"Assignment is currently redundant due to overwriting {collection1.ToName()} and {collection2.ToName()} with an identical collection.\nYou can remove them.";

            // For other assignments, check the inheritance order, unassigned means fall-through,
            // assigned needs identical assignments to be redundant.
            default:
                var group = type.InheritanceOrder();
                foreach (var parentType in group)
                {
                    var assignment = ByType(parentType);
                    if (assignment == null)
                        continue;

                    if (assignment.Index == checkAssignment.Index)
                        return
                            $"Assignment is currently redundant due to overwriting {parentType.ToName()} with an identical collection.\nYou can remove it.";
                }

                break;
        }

        return string.Empty;
    }
}
