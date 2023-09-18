using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Penumbra.GameData.Files;
using Penumbra.Interop.Services;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

internal class TreeBuildCache
{
    private readonly IDataManager                    _dataManager;
    private readonly ActorService                    _actors;
    private readonly Dictionary<FullPath, ShpkFile?> _shaderPackages = new();
    private readonly uint                            _localPlayerId;
    public readonly  List<Character>                 Characters;
    public readonly  Dictionary<uint, Character>     CharactersById;

    public TreeBuildCache(IObjectTable objects, IDataManager dataManager, ActorService actors, bool withCharacters)
    {
        _dataManager   = dataManager;
        _actors        = actors;
        _localPlayerId = objects[0]?.ObjectId ?? GameObject.InvalidGameObjectId;
        if (withCharacters)
        {
            Characters     = objects.OfType<Character>().Where(ch => ch.IsValid()).ToList();
            CharactersById = Characters
                .Where(c => c.ObjectId != GameObject.InvalidGameObjectId)
                .GroupBy(c => c.ObjectId)
                .ToDictionary(c => c.Key, c => c.First());
        }
        else
        {
            Characters     = new();
            CharactersById = new();
        }
    }

    public unsafe bool IsLocalPlayerRelated(Character character)
    {
        if (_localPlayerId == GameObject.InvalidGameObjectId)
            return false;

        // Index 0 is the local player, index 1 is the mount/minion/accessory.
        if (character.ObjectIndex < 2 || character.ObjectIndex == RedrawService.GPosePlayerIdx)
            return true;

        if (!_actors.AwaitedService.FromObject(character, out var owner, true, false, false).IsValid)
            return false;

        // Check for SMN/SCH pet, chocobo and other owned NPCs.
        return owner != null && owner->ObjectID == _localPlayerId;
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
