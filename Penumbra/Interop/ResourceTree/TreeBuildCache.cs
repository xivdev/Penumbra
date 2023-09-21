using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

internal readonly struct TreeBuildCache
{
    private readonly IDataManager                    _dataManager;
    private readonly ActorService                    _actors;
    private readonly Dictionary<FullPath, ShpkFile?> _shaderPackages = new();
    private readonly IObjectTable                    _objects;

    public TreeBuildCache(IObjectTable objects, IDataManager dataManager, ActorService actors)
    {
        _dataManager = dataManager;
        _objects     = objects;
        _actors      = actors;
    }

    public unsafe bool IsLocalPlayerRelated(Character character)
    {
        var player = _objects[0];
        if (player == null)
            return false;

        var gameObject  = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)character.Address;
        var parent      = _actors.AwaitedService.ToCutsceneParent(gameObject->ObjectIndex);
        var actualIndex = parent >= 0 ? (ushort)parent : gameObject->ObjectIndex;
        return actualIndex switch
        {
            < 2                              => true,
            < (int)ScreenActor.CutsceneStart => gameObject->OwnerID == player.ObjectId,
            _                                => false,
        };
    }

    public IEnumerable<Character> GetCharacters()
        => _objects.OfType<Character>();

    public IEnumerable<Character> GetLocalPlayerRelatedCharacters()
    {
        var player = _objects[0];
        if (player == null)
            yield break;

        yield return (Character)player;

        var minion = _objects[1];
        if (minion != null)
            yield return (Character)minion;

        var playerId = player.ObjectId;
        for (var i = 2; i < ObjectIndex.CutsceneStart.Index; i += 2)
        {
            if (_objects[i] is Character owned && owned.OwnerId == playerId)
                yield return owned;
        }

        for (var i = ObjectIndex.CutsceneStart.Index; i < ObjectIndex.CharacterScreen.Index; ++i)
        {
            var character = _objects[i] as Character;
            if (character == null)
                continue;

            var parent = _actors.AwaitedService.ToCutsceneParent(i);
            if (parent < 0)
                continue;

            if (parent is 0 or 1 || _objects[parent]?.OwnerId == playerId)
                yield return character;
        }
    }

    private unsafe ByteString GetPlayerName(GameObject player)
    {
        var gameObject = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)player.Address;
        return new ByteString(gameObject->Name);
    }

    private unsafe bool GetOwnedId(ByteString playerName, uint playerId, int idx, [NotNullWhen(true)] out Character? character)
    {
        character = _objects[idx] as Character;
        if (character == null)
            return false;

        var actorId = _actors.AwaitedService.FromObject(character, out var owner, true, true, true);
        if (!actorId.IsValid)
            return false;
        if (owner != null && owner->OwnerID != playerId)
            return false;
        if (actorId.Type is not IdentifierType.Player || !actorId.PlayerName.Equals(playerName))
            return false;

        return true;
    }

    /// <summary> Try to read a shpk file from the given path and cache it on success. </summary>
    public ShpkFile? ReadShaderPackage(FullPath path)
        => ReadFile(_dataManager, path, _shaderPackages, bytes => new ShpkFile(bytes));

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
