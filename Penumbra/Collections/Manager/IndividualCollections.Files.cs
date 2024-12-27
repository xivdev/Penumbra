using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.ImGuiNotification;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using Penumbra.GameData.Actors;
using Penumbra.GameData.DataContainers.Bases;
using Penumbra.GameData.Structs;
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
            tmp.Add("Collection", collection.Identity.Id);
            tmp.Add("Display",    name);
            ret.Add(tmp);
        }

        return ret;
    }

    public bool ReadJObject(SaveService saver, ActiveCollections parent, JArray? obj, CollectionStorage storage, int version)
    {
        if (_actors.Awaiter.IsCompletedSuccessfully)
        {
            var ret = version switch
            {
                1 => ReadJObjectInternalV1(obj, storage),
                2 => ReadJObjectInternalV2(obj, storage),
                _ => true,
            };
            return ret;
        }

        Penumbra.Log.Debug("[Collections] Delayed reading individual assignments until actor service is ready...");
        _actors.Awaiter.ContinueWith(_ =>
        {
            if (version switch
                {
                    1 => ReadJObjectInternalV1(obj, storage),
                    2 => ReadJObjectInternalV2(obj, storage),
                    _ => true,
                })
                saver.ImmediateSave(parent);
            IsLoaded = true;
            Loaded.Invoke();
        }, TaskScheduler.Default);
        return false;
    }

    private bool ReadJObjectInternalV1(JArray? obj, CollectionStorage storage)
    {
        Penumbra.Log.Debug("[Collections] Reading individual assignments...");
        if (obj == null)
        {
            Penumbra.Log.Debug($"[Collections] Finished reading {Count} individual assignments...");
            return true;
        }

        foreach (var data in obj)
        {
            try
            {
                var identifier = _actors.FromJson(data as JObject);
                var group      = GetGroup(identifier);
                if (group.Length == 0 || group.Any(i => !i.IsValid))
                {
                    Penumbra.Messager.NotificationMessage("Could not load an unknown individual collection, removed.",
                        NotificationType.Error);
                    continue;
                }

                var collectionName = data["Collection"]?.ToObject<string>() ?? string.Empty;
                if (collectionName.Length == 0 || !storage.ByName(collectionName, out var collection))
                {
                    Penumbra.Messager.NotificationMessage(
                        $"Could not load the collection \"{collectionName}\" as individual collection for {identifier}, set to None.",
                        NotificationType.Warning);
                    continue;
                }

                if (!Add(group, collection))
                {
                    Penumbra.Messager.NotificationMessage($"Could not add an individual collection for {identifier}, removed.",
                        NotificationType.Warning);
                }
            }
            catch (Exception e)
            {
                Penumbra.Messager.NotificationMessage(e, $"Could not load an unknown individual collection, removed.", NotificationType.Error);
            }
        }

        Penumbra.Log.Debug($"Finished reading {Count} individual assignments...");

        return true;
    }

    private bool ReadJObjectInternalV2(JArray? obj, CollectionStorage storage)
    {
        Penumbra.Log.Debug("[Collections] Reading individual assignments...");
        if (obj == null)
        {
            Penumbra.Log.Debug($"[Collections] Finished reading {Count} individual assignments...");
            return true;
        }

        var changes = false;
        foreach (var data in obj)
        {
            try
            {
                var identifier = _actors.FromJson(data as JObject);
                var group      = GetGroup(identifier);
                if (group.Length == 0 || group.Any(i => !i.IsValid))
                {
                    changes = true;
                    Penumbra.Messager.NotificationMessage("Could not load an unknown individual collection, removed assignment.",
                        NotificationType.Error);
                    continue;
                }

                var collectionId = data["Collection"]?.ToObject<Guid>();
                if (!collectionId.HasValue || !storage.ById(collectionId.Value, out var collection))
                {
                    changes = true;
                    Penumbra.Messager.NotificationMessage(
                        $"Could not load the collection {collectionId} as individual collection for {identifier}, removed assignment.",
                        NotificationType.Warning);
                    continue;
                }

                if (!Add(group, collection))
                {
                    changes = true;
                    Penumbra.Messager.NotificationMessage($"Could not add an individual collection for {identifier}, removed assignment.",
                        NotificationType.Warning);
                }
            }
            catch (Exception e)
            {
                changes = true;
                Penumbra.Messager.NotificationMessage(e, $"Could not load an unknown individual collection, removed assignment.", NotificationType.Error);
            }
        }

        Penumbra.Log.Debug($"Finished reading {Count} individual assignments...");

        return changes;
    }

    internal void Migrate0To1(Dictionary<string, ModCollection> old)
    {
        foreach (var (name, collection) in old)
        {
            var kind      = ObjectKind.None;
            var lowerName = name.ToLowerInvariant();
            // Prefer matching NPC names, fewer false positives than preferring players.
            if (FindDataId(lowerName, _actors.Data.Companions, out var dataId))
                kind = ObjectKind.Companion;
            else if (FindDataId(lowerName, _actors.Data.Mounts, out dataId))
                kind = ObjectKind.MountType;
            else if (FindDataId(lowerName, _actors.Data.BNpcs, out dataId))
                kind = ObjectKind.BattleNpc;
            else if (FindDataId(lowerName, _actors.Data.ENpcs, out dataId))
                kind = ObjectKind.EventNpc;

            var identifier = _actors.CreateNpc(kind, dataId);
            if (identifier.IsValid)
            {
                // If the name corresponds to a valid npc, add it as a group. If this fails, notify users.
                var group = GetGroup(identifier);
                var ids   = string.Join(", ", group.Select(i => i.DataId.ToString()));
                if (Add($"{_actors.Data.ToName(kind, dataId)} ({kind.ToName()})", group, collection))
                    Penumbra.Log.Information($"Migrated {name} ({kind.ToName()}) to NPC Identifiers [{ids}].");
                else
                    Penumbra.Messager.NotificationMessage(
                        $"Could not migrate {name} ({collection.Identity.AnonymizedName}) which was assumed to be a {kind.ToName()} with IDs [{ids}], please look through your individual collections.",
                        NotificationType.Error);
            }
            // If it is not a valid NPC name, check if it can be a player name.
            else if (ActorIdentifierFactory.VerifyPlayerName(name))
            {
                identifier = _actors.CreatePlayer(ByteString.FromStringUnsafe(name, false), ushort.MaxValue);
                var shortName = string.Join(" ", name.Split().Select(n => $"{n[0]}."));
                // Try to migrate the player name without logging full names.
                if (Add($"{name} ({_actors.Data.ToWorldName(identifier.HomeWorld)})", [identifier], collection))
                    Penumbra.Log.Information($"Migrated {shortName} ({collection.Identity.AnonymizedName}) to Player Identifier.");
                else
                    Penumbra.Messager.NotificationMessage(
                        $"Could not migrate {shortName} ({collection.Identity.AnonymizedName}), please look through your individual collections.",
                        NotificationType.Error);
            }
            else
            {
                Penumbra.Messager.NotificationMessage(
                    $"Could not migrate {name} ({collection.Identity.AnonymizedName}), which can not be a player name nor is it a known NPC name, please look through your individual collections.",
                    NotificationType.Error);
            }
        }

        return;

        static bool FindDataId(string name, NameDictionary data, out NpcId dataId)
        {
            var kvp = data.FirstOrDefault(kvp => kvp.Value.Equals(name, StringComparison.OrdinalIgnoreCase),
                new KeyValuePair<NpcId, string>(uint.MaxValue, string.Empty));
            dataId = kvp.Key;
            return kvp.Value.Length > 0;
        }
    }
}
