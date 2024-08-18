using System.IO.MemoryMappedFiles;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.ShaderStructs;
using Penumbra.GameData.Files.Utility;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

internal readonly struct TreeBuildCache(ObjectManager objects, IDataManager dataManager, ActorManager actors)
{
    private readonly Dictionary<FullPath, IReadOnlyDictionary<uint, Name>?> _shaderPackageNames = [];

    public unsafe bool IsLocalPlayerRelated(ICharacter character)
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
            < (int)ScreenActor.CutsceneStart => gameObject->OwnerId == player.EntityId,
            _                                => false,
        };
    }

    public IEnumerable<ICharacter> GetCharacters()
        => objects.Objects.OfType<ICharacter>();

    public IEnumerable<ICharacter> GetLocalPlayerRelatedCharacters()
    {
        var player = objects.GetDalamudObject(0);
        if (player == null)
            yield break;

        yield return (ICharacter)player;

        var minion = objects.GetDalamudObject(1);
        if (minion != null)
            yield return (ICharacter)minion;

        var playerId = player.EntityId;
        for (var i = 2; i < ObjectIndex.CutsceneStart.Index; i += 2)
        {
            if (objects.GetDalamudObject(i) is ICharacter owned && owned.OwnerId == playerId)
                yield return owned;
        }

        for (var i = ObjectIndex.CutsceneStart.Index; i < ObjectIndex.CharacterScreen.Index; ++i)
        {
            var character = objects.GetDalamudObject((int) i) as ICharacter;
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
    public IReadOnlyDictionary<uint, Name>? ReadShaderPackageNames(FullPath path)
        => ReadFile(dataManager, path, _shaderPackageNames, bytes => ShpkFile.FastExtractNames(bytes.Span));

    private static T? ReadFile<T>(IDataManager dataManager, FullPath path, Dictionary<FullPath, T?> cache, Func<ReadOnlyMemory<byte>, T> parseFile)
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
                using var mmFile = MmioMemoryManager.CreateFromFile(pathStr, access: MemoryMappedFileAccess.Read);
                parsed = parseFile(mmFile.Memory);
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
