using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

internal readonly struct TreeBuildCache(ObjectManager objects, IDataManager dataManager, ActorManager actors)
{
    private readonly Dictionary<FullPath, ShpkFile?> _shaderPackages = [];

    public unsafe bool IsLocalPlayerRelated(Character character)
    {
        var player = objects.GetDalamudObject(0);
        if (player == null)
            return false;

        var gameObject  = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)character.Address;
        var parent      = actors.ToCutsceneParent(gameObject->ObjectIndex);
        var actualIndex = parent >= 0 ? (ushort)parent : gameObject->ObjectIndex;
        return actualIndex switch
        {
            < 2                              => true,
            < (int)ScreenActor.CutsceneStart => gameObject->OwnerID == player.ObjectId,
            _                                => false,
        };
    }

    public IEnumerable<Character> GetCharacters()
        => objects.Objects.OfType<Character>();

    public IEnumerable<Character> GetLocalPlayerRelatedCharacters()
    {
        var player = objects.GetDalamudObject(0);
        if (player == null)
            yield break;

        yield return (Character)player;

        var minion = objects.GetDalamudObject(1);
        if (minion != null)
            yield return (Character)minion;

        var playerId = player.ObjectId;
        for (var i = 2; i < ObjectIndex.CutsceneStart.Index; i += 2)
        {
            if (objects.GetDalamudObject(i) is Character owned && owned.OwnerId == playerId)
                yield return owned;
        }

        for (var i = ObjectIndex.CutsceneStart.Index; i < ObjectIndex.CharacterScreen.Index; ++i)
        {
            var character = objects.GetDalamudObject((int) i) as Character;
            if (character == null)
                continue;

            var parent = actors.ToCutsceneParent(i);
            if (parent < 0)
                continue;

            if (parent is 0 or 1 || objects.GetDalamudObject(parent)?.OwnerId == playerId)
                yield return character;
        }
    }

    /// <summary> Try to read a shpk file from the given path and cache it on success. </summary>
    public ShpkFile? ReadShaderPackage(FullPath path)
        => ReadFile(dataManager, path, _shaderPackages, bytes => new ShpkFile(bytes));

    private static T? ReadFile<T>(IDataManager dataManager, FullPath path, Dictionary<FullPath, T?> cache, Func<byte[], T> parseFile)
        where T : class
    {
        if (path.FullName.Length == 0)
            return null;

        if (cache.TryGetValue(path, out var cached))
            return cached;

        var pathStr = path.ToPath();
        T?  parsed;
        try
        {
            if (path.IsRooted)
            {
                parsed = parseFile(File.ReadAllBytes(pathStr));
            }
            else
            {
                var bytes = dataManager.GetFile(pathStr)?.Data;
                parsed = bytes != null ? parseFile(bytes) : null;
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not read file {pathStr}:\n{e}");
            parsed = null;
        }

        cache.Add(path, parsed);

        return parsed;
    }
}
