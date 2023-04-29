using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Data;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

internal class TreeBuildCache
{
    private readonly DataManager                     _dataManager;
    private readonly Dictionary<FullPath, MtrlFile?> _materials      = new();
    private readonly Dictionary<FullPath, ShpkFile?> _shaderPackages = new();
    public readonly  List<Character>                 Characters;
    public readonly  Dictionary<uint, Character>     CharactersById;

    public TreeBuildCache(ObjectTable objects, DataManager dataManager)
    {
        _dataManager = dataManager;
        Characters   = objects.Where(c => c is Character ch && ch.IsValid()).Cast<Character>().ToList();
        CharactersById = Characters
            .Where(c => c.ObjectId != GameObject.InvalidGameObjectId)
            .GroupBy(c => c.ObjectId)
            .ToDictionary(c => c.Key, c => c.First());
    }

    /// <summary> Try to read a material file from the given path and cache it on success. </summary>
    public MtrlFile? ReadMaterial(FullPath path)
        => ReadFile(_dataManager, path, _materials, bytes => new MtrlFile(bytes));

    /// <summary> Try to read a shpk file from the given path and cache it on success. </summary>
    public ShpkFile? ReadShaderPackage(FullPath path)
        => ReadFile(_dataManager, path, _shaderPackages, bytes => new ShpkFile(bytes));

    private static T? ReadFile<T>(DataManager dataManager, FullPath path, Dictionary<FullPath, T?> cache, Func<byte[], T> parseFile)
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
