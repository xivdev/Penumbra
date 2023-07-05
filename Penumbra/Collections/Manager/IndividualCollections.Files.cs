using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Actors;
using Penumbra.Services;
using Penumbra.String;

namespace Penumbra.Collections.Manager;

public partial class IndividualCollections
{
    public JArray ToJObject()
    {
        var ret = new JArray();
        foreach (var (name, identifiers, collection) in Assignments)
        {
            var tmp = identifiers[0].ToJson();
            tmp.Add("Collection", collection.Name);
            tmp.Add("Display",    name);
            ret.Add(tmp);
        }

        return ret;
    }

    public bool ReadJObject(SaveService saver, ActiveCollections parent, JArray? obj, CollectionStorage storage)
    {
        if (_actorService.Valid)
            return ReadJObjectInternal(obj, storage);
        void Func()
        {
            if (ReadJObjectInternal(obj, storage))
                saver.ImmediateSave(parent);
            IsLoaded = true;
            Loaded.Invoke();
            _actorService.FinishedCreation -= Func;
        }
        _actorService.FinishedCreation += Func;
        return false;
    }

    private bool ReadJObjectInternal(JArray? obj, CollectionStorage storage)
    {
        if (obj == null)
            return true;

        var changes = false;
        foreach (var data in obj)
        {
            try
            {
                var identifier = _actorService.AwaitedService.FromJson(data as JObject);
                var group      = GetGroup(identifier);
                if (group.Length == 0 || group.Any(i => !i.IsValid))
                {
                    changes = true;
                    Penumbra.Chat.NotificationMessage("Could not load an unknown individual collection, removed.", "Load Failure",
                        NotificationType.Warning);
                    continue;
                }

                var collectionName = data["Collection"]?.ToObject<string>() ?? string.Empty;
                if (collectionName.Length == 0 || !storage.ByName(collectionName, out var collection))
                {
                    changes = true;
                    Penumbra.Chat.NotificationMessage(
                        $"Could not load the collection \"{collectionName}\" as individual collection for {identifier}, set to None.",
                        "Load Failure",
                        NotificationType.Warning);
                    continue;
                }

                if (!Add(group, collection))
                {
                    changes = true;
                    Penumbra.Chat.NotificationMessage($"Could not add an individual collection for {identifier}, removed.",
                        "Load Failure",
                        NotificationType.Warning);
                }
            }
            catch (Exception e)
            {
                changes = true;
                Penumbra.Chat.NotificationMessage($"Could not load an unknown individual collection, removed:\n{e}", "Load Failure",
                    NotificationType.Error);
            }
        }
        return changes;
    }

    internal void Migrate0To1(Dictionary<string, ModCollection> old)
    {
        static bool FindDataId(string name, IReadOnlyDictionary<uint, string> data, out uint dataId)
        {
            var kvp = data.FirstOrDefault(kvp => kvp.Value.Equals(name, StringComparison.OrdinalIgnoreCase),
                new KeyValuePair<uint, string>(uint.MaxValue, string.Empty));
            dataId = kvp.Key;
            return kvp.Value.Length > 0;
        }

        foreach (var (name, collection) in old)
        {
            var kind      = ObjectKind.None;
            var lowerName = name.ToLowerInvariant();
            // Prefer matching NPC names, fewer false positives than preferring players.
            if (FindDataId(lowerName, _actorService.AwaitedService.Data.Companions, out var dataId))
                kind = ObjectKind.Companion;
            else if (FindDataId(lowerName, _actorService.AwaitedService.Data.Mounts, out dataId))
                kind = ObjectKind.MountType;
            else if (FindDataId(lowerName, _actorService.AwaitedService.Data.BNpcs, out dataId))
                kind = ObjectKind.BattleNpc;
            else if (FindDataId(lowerName, _actorService.AwaitedService.Data.ENpcs, out dataId))
                kind = ObjectKind.EventNpc;

            var identifier = _actorService.AwaitedService.CreateNpc(kind, dataId);
            if (identifier.IsValid)
            {
                // If the name corresponds to a valid npc, add it as a group. If this fails, notify users.
                var group = GetGroup(identifier);
                var ids   = string.Join(", ", group.Select(i => i.DataId.ToString()));
                if (Add($"{_actorService.AwaitedService.Data.ToName(kind, dataId)} ({kind.ToName()})", group, collection))
                    Penumbra.Log.Information($"Migrated {name} ({kind.ToName()}) to NPC Identifiers [{ids}].");
                else
                    Penumbra.Chat.NotificationMessage(
                        $"Could not migrate {name} ({collection.AnonymizedName}) which was assumed to be a {kind.ToName()} with IDs [{ids}], please look through your individual collections.",
                        "Migration Failure", NotificationType.Error);
            }
            // If it is not a valid NPC name, check if it can be a player name.
            else if (ActorManager.VerifyPlayerName(name))
            {
                identifier = _actorService.AwaitedService.CreatePlayer(ByteString.FromStringUnsafe(name, false), ushort.MaxValue);
                var shortName = string.Join(" ", name.Split().Select(n => $"{n[0]}."));
                // Try to migrate the player name without logging full names.
                if (Add($"{name} ({_actorService.AwaitedService.Data.ToWorldName(identifier.HomeWorld)})", new[]
                    {
                        identifier,
                    }, collection))
                    Penumbra.Log.Information($"Migrated {shortName} ({collection.AnonymizedName}) to Player Identifier.");
                else
                    Penumbra.Chat.NotificationMessage(
                        $"Could not migrate {shortName} ({collection.AnonymizedName}), please look through your individual collections.",
                        "Migration Failure", NotificationType.Error);
            }
            else
            {
                Penumbra.Chat.NotificationMessage(
                    $"Could not migrate {name} ({collection.AnonymizedName}), which can not be a player name nor is it a known NPC name, please look through your individual collections.",
                    "Migration Failure", NotificationType.Error);
            }
        }
    }
}
