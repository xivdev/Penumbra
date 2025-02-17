using OtterGui.Log;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.String.Classes;

namespace Penumbra.Api.Api;

public class TemporaryApi(
    TempCollectionManager tempCollections,
    ObjectManager objects,
    ActorManager actors,
    CollectionManager collectionManager,
    TempModManager tempMods,
    ApiHelpers apiHelpers,
    ModManager modManager) : IPenumbraApiTemporary, IApiService
{
    public Guid CreateTemporaryCollection(string name)
        => tempCollections.CreateTemporaryCollection(name);

    public PenumbraApiEc DeleteTemporaryCollection(Guid collectionId)
        => tempCollections.RemoveTemporaryCollection(collectionId)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.CollectionMissing;

    public PenumbraApiEc AssignTemporaryCollection(Guid collectionId, int actorIndex, bool forceAssignment)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ActorIndex", actorIndex, "Forced", forceAssignment);
        if (actorIndex < 0 || actorIndex >= objects.TotalCount)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        var identifier = actors.FromObject(objects[actorIndex], out _, false, false, true);
        if (!identifier.IsValid)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        if (!tempCollections.CollectionById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (forceAssignment)
        {
            if (tempCollections.Collections.ContainsKey(identifier) && !tempCollections.Collections.Delete(identifier))
                return ApiHelpers.Return(PenumbraApiEc.AssignmentDeletionFailed, args);
        }
        else if (tempCollections.Collections.ContainsKey(identifier)
              || collectionManager.Active.Individuals.ContainsKey(identifier))
        {
            return ApiHelpers.Return(PenumbraApiEc.CharacterCollectionExists, args);
        }

        var group = tempCollections.Collections.GetGroup(identifier);
        var ret = tempCollections.AddIdentifier(collection, group)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.UnknownError;
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc AddTemporaryModAll(string tag, Dictionary<string, string> paths, string manipString, int priority)
    {
        var args = ApiHelpers.Args("Tag", tag, "#Paths", paths.Count, "ManipString", manipString, "Priority", priority);
        if (!ConvertPaths(paths, out var p))
            return ApiHelpers.Return(PenumbraApiEc.InvalidGamePath, args);

        if (!MetaApi.ConvertManips(manipString, out var m, out _))
            return ApiHelpers.Return(PenumbraApiEc.InvalidManipulation, args);

        var ret = tempMods.Register(tag, null, p, m, new ModPriority(priority)) switch
        {
            RedirectResult.Success => PenumbraApiEc.Success,
            _                      => PenumbraApiEc.UnknownError,
        };
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc AddTemporaryMod(string tag, Guid collectionId, Dictionary<string, string> paths, string manipString, int priority)
    {
        var args = ApiHelpers.Args("Tag", tag, "CollectionId", collectionId, "#Paths", paths.Count, "ManipString",
            manipString, "Priority", priority);

        if (collectionId == Guid.Empty)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        if (!tempCollections.CollectionById(collectionId, out var collection)
         && !collectionManager.Storage.ById(collectionId, out collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (!ConvertPaths(paths, out var p))
            return ApiHelpers.Return(PenumbraApiEc.InvalidGamePath, args);

        if (!MetaApi.ConvertManips(manipString, out var m, out _))
            return ApiHelpers.Return(PenumbraApiEc.InvalidManipulation, args);

        var ret = tempMods.Register(tag, collection, p, m, new ModPriority(priority)) switch
        {
            RedirectResult.Success => PenumbraApiEc.Success,
            _                      => PenumbraApiEc.UnknownError,
        };
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc RemoveTemporaryModAll(string tag, int priority)
    {
        var ret = tempMods.Unregister(tag, null, new ModPriority(priority)) switch
        {
            RedirectResult.Success       => PenumbraApiEc.Success,
            RedirectResult.NotRegistered => PenumbraApiEc.NothingChanged,
            _                            => PenumbraApiEc.UnknownError,
        };
        return ApiHelpers.Return(ret, ApiHelpers.Args("Tag", tag, "Priority", priority));
    }

    public PenumbraApiEc RemoveTemporaryMod(string tag, Guid collectionId, int priority)
    {
        var args = ApiHelpers.Args("Tag", tag, "CollectionId", collectionId, "Priority", priority);

        if (!tempCollections.CollectionById(collectionId, out var collection)
         && !collectionManager.Storage.ById(collectionId, out collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        var ret = tempMods.Unregister(tag, collection, new ModPriority(priority)) switch
        {
            RedirectResult.Success       => PenumbraApiEc.Success,
            RedirectResult.NotRegistered => PenumbraApiEc.NothingChanged,
            _                            => PenumbraApiEc.UnknownError,
        };
        return ApiHelpers.Return(ret, args);
    }

    public (PenumbraApiEc, (bool, bool, int, Dictionary<string, List<string>>)?, string) QueryTemporaryModSettings(Guid collectionId,
        string modDirectory, string modName, int key)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", modDirectory, "ModName", modName);
        if (!collectionManager.Storage.ById(collectionId, out var collection))
            return (ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args), null, string.Empty);

        return QueryTemporaryModSettings(args, collection, modDirectory, modName, key);
    }

    public (PenumbraApiEc ErrorCode, (bool, bool, int, Dictionary<string, List<string>>)? Settings, string Source)
        QueryTemporaryModSettingsPlayer(int objectIndex,
            string modDirectory, string modName, int key)
    {
        var args = ApiHelpers.Args("ObjectIndex", objectIndex, "ModDirectory", modDirectory, "ModName", modName);
        if (!apiHelpers.AssociatedCollection(objectIndex, out var collection))
            return (ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args), null, string.Empty);

        return QueryTemporaryModSettings(args, collection, modDirectory, modName, key);
    }

    private (PenumbraApiEc ErrorCode, (bool, bool, int, Dictionary<string, List<string>>)? Settings, string Source) QueryTemporaryModSettings(
        in LazyString args, ModCollection collection, string modDirectory, string modName, int key)
    {
        if (!modManager.TryGetMod(modDirectory, modName, out var mod))
            return (ApiHelpers.Return(PenumbraApiEc.ModMissing, args), null, string.Empty);

        if (collection.Identity.Index <= 0)
            return (ApiHelpers.Return(PenumbraApiEc.Success, args), null, string.Empty);

        var settings = collection.GetTempSettings(mod.Index);
        if (settings == null)
            return (ApiHelpers.Return(PenumbraApiEc.Success, args), null, string.Empty);

        if (settings.Lock > 0 && settings.Lock != key)
            return (ApiHelpers.Return(PenumbraApiEc.TemporarySettingDisallowed, args), null, settings.Source);

        return (ApiHelpers.Return(PenumbraApiEc.Success, args),
            (settings.ForceInherit, settings.Enabled, settings.Priority.Value, settings.ConvertToShareable(mod).Settings), settings.Source);
    }


    public PenumbraApiEc SetTemporaryModSettings(Guid collectionId, string modDirectory, string modName, bool inherit, bool enabled,
        int priority,
        IReadOnlyDictionary<string, IReadOnlyList<string>> options, string source, int key)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", modDirectory, "ModName", modName, "Inherit", inherit,
            "Enabled", enabled,
            "Priority", priority, "Options", options, "Source", source, "Key", key);
        if (!collectionManager.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        return SetTemporaryModSettings(args, collection, modDirectory, modName, inherit, enabled, priority, options, source, key);
    }

    public PenumbraApiEc SetTemporaryModSettingsPlayer(int objectIndex, string modDirectory, string modName, bool inherit, bool enabled,
        int priority,
        IReadOnlyDictionary<string, IReadOnlyList<string>> options, string source, int key)
    {
        var args = ApiHelpers.Args("ObjectIndex", objectIndex, "ModDirectory", modDirectory, "ModName", modName, "Inherit", inherit, "Enabled",
            enabled,
            "Priority", priority, "Options", options, "Source", source, "Key", key);
        if (!apiHelpers.AssociatedCollection(objectIndex, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        return SetTemporaryModSettings(args, collection, modDirectory, modName, inherit, enabled, priority, options, source, key);
    }

    private PenumbraApiEc SetTemporaryModSettings(in LazyString args, ModCollection collection, string modDirectory, string modName,
        bool inherit, bool enabled, int priority, IReadOnlyDictionary<string, IReadOnlyList<string>> options, string source, int key)
    {
        if (collection.Identity.Index <= 0)
            return ApiHelpers.Return(PenumbraApiEc.TemporarySettingImpossible, args);

        if (!modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        if (!collectionManager.Editor.CanSetTemporarySettings(collection, mod, key))
            if (collection.GetTempSettings(mod.Index) is { Lock: > 0 } oldSettings && oldSettings.Lock != key)
                return ApiHelpers.Return(PenumbraApiEc.TemporarySettingDisallowed, args);

        var newSettings = new TemporaryModSettings()
        {
            ForceInherit = inherit,
            Enabled      = enabled,
            Priority     = new ModPriority(priority),
            Lock         = key,
            Source       = source,
            Settings     = SettingList.Default(mod),
        };


        foreach (var (groupName, optionNames) in options)
        {
            var ec = ModSettingsApi.ConvertModSetting(mod, groupName, optionNames, out var groupIdx, out var setting);
            if (ec != PenumbraApiEc.Success)
                return ApiHelpers.Return(ec, args);

            newSettings.Settings[groupIdx] = setting;
        }

        if (collectionManager.Editor.SetTemporarySettings(collection, mod, newSettings, key))
            return ApiHelpers.Return(PenumbraApiEc.Success, args);

        // This should not happen since all error cases had been checked before.
        return ApiHelpers.Return(PenumbraApiEc.UnknownError, args);
    }

    public PenumbraApiEc RemoveTemporaryModSettings(Guid collectionId, string modDirectory, string modName, int key)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ModDirectory", modDirectory, "ModName", modName, "Key", key);
        if (!collectionManager.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        return RemoveTemporaryModSettings(args, collection, modDirectory, modName, key);
    }

    public PenumbraApiEc RemoveTemporaryModSettingsPlayer(int objectIndex, string modDirectory, string modName, int key)
    {
        var args = ApiHelpers.Args("ObjectIndex", objectIndex, "ModDirectory", modDirectory, "ModName", modName, "Key", key);
        if (!apiHelpers.AssociatedCollection(objectIndex, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        return RemoveTemporaryModSettings(args, collection, modDirectory, modName, key);
    }

    private PenumbraApiEc RemoveTemporaryModSettings(in LazyString args, ModCollection collection, string modDirectory, string modName, int key)
    {
        if (collection.Identity.Index <= 0)
            return ApiHelpers.Return(PenumbraApiEc.NothingChanged, args);

        if (!modManager.TryGetMod(modDirectory, modName, out var mod))
            return ApiHelpers.Return(PenumbraApiEc.ModMissing, args);

        if (collection.GetTempSettings(mod.Index) is null)
            return ApiHelpers.Return(PenumbraApiEc.NothingChanged, args);

        if (!collectionManager.Editor.SetTemporarySettings(collection, mod, null, key))
            return ApiHelpers.Return(PenumbraApiEc.TemporarySettingDisallowed, args);

        return ApiHelpers.Return(PenumbraApiEc.Success, args);
    }

    public PenumbraApiEc RemoveAllTemporaryModSettings(Guid collectionId, int key)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "Key", key);
        if (!collectionManager.Storage.ById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        return RemoveAllTemporaryModSettings(args, collection, key);
    }

    public PenumbraApiEc RemoveAllTemporaryModSettingsPlayer(int objectIndex, int key)
    {
        var args = ApiHelpers.Args("ObjectIndex", objectIndex, "Key", key);
        if (!apiHelpers.AssociatedCollection(objectIndex, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        return RemoveAllTemporaryModSettings(args, collection, key);
    }

    private PenumbraApiEc RemoveAllTemporaryModSettings(in LazyString args, ModCollection collection, int key)
    {
        if (collection.Identity.Index <= 0)
            return ApiHelpers.Return(PenumbraApiEc.NothingChanged, args);

        var numRemoved = collectionManager.Editor.ClearTemporarySettings(collection, key);
        return ApiHelpers.Return(numRemoved > 0 ? PenumbraApiEc.Success : PenumbraApiEc.NothingChanged, args);
    }


    /// <summary>
    /// Convert a dictionary of strings to a dictionary of game paths to full paths.
    /// Only returns true if all paths can successfully be converted and added.
    /// </summary>
    private static bool ConvertPaths(IReadOnlyDictionary<string, string> redirections,
        [NotNullWhen(true)] out Dictionary<Utf8GamePath, FullPath>? paths)
    {
        paths = new Dictionary<Utf8GamePath, FullPath>(redirections.Count);
        foreach (var (gString, fString) in redirections)
        {
            if (!Utf8GamePath.FromString(gString, out var path))
            {
                paths = null;
                return false;
            }

            var fullPath = new FullPath(fString);
            if (!paths.TryAdd(path, fullPath))
            {
                paths = null;
                return false;
            }
        }

        return true;
    }
}
