using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using OtterGui.Filesystem;
using Penumbra.GameData.Actors;
using Penumbra.Services;
using Penumbra.String;

namespace Penumbra.Collections.Manager;

public sealed partial class IndividualCollections
{
    public record struct IndividualAssignment(string DisplayName, IReadOnlyList<ActorIdentifier> Identifiers, ModCollection Collection);

    private readonly Configuration                              _config;
    private readonly ActorService                               _actorService;
    private readonly Dictionary<ActorIdentifier, ModCollection> _individuals = new();
    private readonly List<IndividualAssignment>                 _assignments = new();

    public event Action Loaded;
    public bool         IsLoaded { get; private set; }

    public IReadOnlyList<IndividualAssignment> Assignments
        => _assignments;

    public IndividualCollections(ActorService actorService, Configuration config, bool temporary)
    {
        _config       =  config;
        _actorService =  actorService;
        IsLoaded      =  temporary;
        Loaded        += () => Penumbra.Log.Information($"{_assignments.Count} Individual Assignments loaded after delay.");
    }

    public enum AddResult
    {
        Valid,
        AlreadySet,
        Invalid,
    }

    public bool TryGetValue(ActorIdentifier identifier, [NotNullWhen(true)] out ModCollection? collection)
    {
        lock (_individuals)
        {
            return _individuals.TryGetValue(identifier, out collection);
        }
    }

    public bool ContainsKey(ActorIdentifier identifier)
    {
        lock (_individuals)
        {
            return _individuals.ContainsKey(identifier);
        }
    }

    public AddResult CanAdd(params ActorIdentifier[] identifiers)
    {
        if (identifiers.Length == 0)
            return AddResult.Invalid;

        if (identifiers.Any(i => !i.IsValid))
            return AddResult.Invalid;

        bool set;
        lock (_individuals)
        {
            set = identifiers.Any(_individuals.ContainsKey);
        }

        return set ? AddResult.AlreadySet : AddResult.Valid;
    }

    public AddResult CanAdd(IdentifierType type, string name, ushort homeWorld, ObjectKind kind, IEnumerable<uint> dataIds,
        out ActorIdentifier[] identifiers)
    {
        identifiers = Array.Empty<ActorIdentifier>();

        var manager = _actorService.AwaitedService;
        switch (type)
        {
            case IdentifierType.Player:
                if (!ByteString.FromString(name, out var playerName))
                    return AddResult.Invalid;

                identifiers = new[]
                {
                    manager.CreatePlayer(playerName, homeWorld),
                };
                break;
            case IdentifierType.Retainer:
                if (!ByteString.FromString(name, out var retainerName))
                    return AddResult.Invalid;

                identifiers = new[]
                {
                    manager.CreateRetainer(retainerName, ActorIdentifier.RetainerType.Both),
                };
                break;
            case IdentifierType.Owned:
                if (!ByteString.FromString(name, out var ownerName))
                    return AddResult.Invalid;

                identifiers = dataIds.Select(id => manager.CreateOwned(ownerName, homeWorld, kind, id)).ToArray();
                break;
            case IdentifierType.Npc:
                identifiers = dataIds
                    .Select(id => manager.CreateIndividual(IdentifierType.Npc, ByteString.Empty, ushort.MaxValue, kind, id)).ToArray();
                break;
            default:
                identifiers = Array.Empty<ActorIdentifier>();
                break;
        }

        return CanAdd(identifiers);
    }

    public ActorIdentifier[] GetGroup(ActorIdentifier identifier)
    {
        if (!identifier.IsValid)
            return Array.Empty<ActorIdentifier>();

        static ActorIdentifier[] CreateNpcs(ActorManager manager, ActorIdentifier identifier)
        {
            var name = manager.Data.ToName(identifier.Kind, identifier.DataId);
            var table = identifier.Kind switch
            {
                ObjectKind.BattleNpc => manager.Data.BNpcs,
                ObjectKind.EventNpc  => manager.Data.ENpcs,
                ObjectKind.Companion => manager.Data.Companions,
                ObjectKind.MountType => manager.Data.Mounts,
                ObjectKind.Ornament  => manager.Data.Ornaments,
                _                    => throw new NotImplementedException(),
            };
            return table.Where(kvp => kvp.Value == name)
                .Select(kvp => manager.CreateIndividualUnchecked(identifier.Type, identifier.PlayerName, identifier.HomeWorld.Id, identifier.Kind,
                    kvp.Key)).ToArray();
        }

        return identifier.Type switch
        {
            IdentifierType.Player => new[]
            {
                identifier.CreatePermanent(),
            },
            IdentifierType.Special => new[]
            {
                identifier,
            },
            IdentifierType.Retainer => new[]
            {
                identifier.CreatePermanent(),
            },
            IdentifierType.Owned => CreateNpcs(_actorService.AwaitedService, identifier.CreatePermanent()),
            IdentifierType.Npc   => CreateNpcs(_actorService.AwaitedService, identifier),
            _                    => Array.Empty<ActorIdentifier>(),
        };
    }

    internal bool Add(ActorIdentifier[] identifiers, ModCollection collection)
    {
        if (identifiers.Length == 0 || !identifiers[0].IsValid)
            return false;

        var name = DisplayString(identifiers[0]);
        return Add(name, identifiers, collection);
    }

    private bool Add(string displayName, ActorIdentifier[] identifiers, ModCollection collection)
    {
        if (CanAdd(identifiers) != AddResult.Valid
         || displayName.Length == 0
         || _assignments.Any(a => a.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
            return false;

        for (var i = 0; i < identifiers.Length; ++i)
        {
            identifiers[i] = identifiers[i].CreatePermanent();
            lock (_individuals)
            {
                _individuals.Add(identifiers[i], collection);
            }
        }

        _assignments.Add(new IndividualAssignment(displayName, identifiers, collection));

        return true;
    }

    internal bool ChangeCollection(ActorIdentifier identifier, ModCollection newCollection)
        => ChangeCollection(DisplayString(identifier), newCollection);

    internal bool ChangeCollection(string displayName, ModCollection newCollection)
        => ChangeCollection(_assignments.FindIndex(t => t.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase)), newCollection);

    internal bool ChangeCollection(int displayIndex, ModCollection newCollection)
    {
        if (displayIndex < 0 || displayIndex >= _assignments.Count || _assignments[displayIndex].Collection == newCollection)
            return false;

        _assignments[displayIndex] = _assignments[displayIndex] with { Collection = newCollection };
        lock (_individuals)
        {
            foreach (var identifier in _assignments[displayIndex].Identifiers)
                _individuals[identifier] = newCollection;
        }

        return true;
    }

    internal bool Delete(ActorIdentifier identifier)
        => Delete(Index(identifier));

    internal bool Delete(string displayName)
        => Delete(Index(displayName));

    internal bool Delete(int displayIndex)
    {
        if (displayIndex < 0 || displayIndex >= _assignments.Count)
            return false;

        var (name, identifiers, _) = _assignments[displayIndex];
        _assignments.RemoveAt(displayIndex);
        lock (_individuals)
        {
            foreach (var identifier in identifiers)
                _individuals.Remove(identifier);
        }

        return true;
    }

    internal bool Move(int from, int to)
        => _assignments.Move(from, to);

    internal int Index(string displayName)
        => _assignments.FindIndex(t => t.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

    internal int Index(ActorIdentifier identifier)
        => identifier.IsValid ? Index(DisplayString(identifier)) : -1;

    private string DisplayString(ActorIdentifier identifier)
    {
        return identifier.Type switch
        {
            IdentifierType.Player => $"{identifier.PlayerName} ({_actorService.AwaitedService.Data.ToWorldName(identifier.HomeWorld)})",
            IdentifierType.Retainer => $"{identifier.PlayerName} (Retainer)",
            IdentifierType.Owned =>
                $"{identifier.PlayerName} ({_actorService.AwaitedService.Data.ToWorldName(identifier.HomeWorld)})'s {_actorService.AwaitedService.Data.ToName(identifier.Kind, identifier.DataId)}",
            IdentifierType.Npc =>
                $"{_actorService.AwaitedService.Data.ToName(identifier.Kind, identifier.DataId)} ({identifier.Kind.ToName()})",
            _ => string.Empty,
        };
    }
}
