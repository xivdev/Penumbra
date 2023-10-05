using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using Penumbra.Services;

namespace Penumbra.Collections.Manager;

public static class ActiveCollectionMigration
{
    /// <summary> Migrate ungendered collections to Male and Female for 0.5.9.0. </summary>
    public static void MigrateUngenderedCollections(FilenameService fileNames)
    {
        if (!ActiveCollections.Load(fileNames, out var jObject))
            return;

        foreach (var (type, _, _) in CollectionTypeExtensions.Special.Where(t => t.Item2.StartsWith("Male ")))
        {
            var oldName = type.ToString()[4..];
            var value   = jObject[oldName];
            if (value == null)
                continue;

            jObject.Remove(oldName);
            jObject.Add("Male" + oldName,   value);
            jObject.Add("Female" + oldName, value);
        }

        using var stream = File.Open(fileNames.ActiveCollectionsFile, FileMode.Truncate);
        using var writer = new StreamWriter(stream);
        using var j      = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        jObject.WriteTo(j);
    }

    /// <summary> Migrate individual collections to Identifiers for 0.6.0. </summary>
    public static bool MigrateIndividualCollections(CollectionStorage storage, IndividualCollections individuals, JObject jObject)
    {
        var version = jObject[nameof(Version)]?.Value<int>() ?? 0;
        if (version > 0)
            return false;

        // Load character collections. If a player name comes up multiple times, the last one is applied.
        var characters = jObject["Characters"]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        var dict       = new Dictionary<string, ModCollection>(characters.Count);
        foreach (var (player, collectionName) in characters)
        {
            if (!storage.ByName(collectionName, out var collection))
            {
                Penumbra.Messager.NotificationMessage(
                    $"Last choice of <{player}>'s Collection {collectionName} is not available, reset to {ModCollection.Empty.Name}.", NotificationType.Warning);
                dict.Add(player, ModCollection.Empty);
            }
            else
            {
                dict.Add(player, collection);
            }
        }

        individuals.Migrate0To1(dict);
        return true;
    }
}
