using Newtonsoft.Json.Linq;
using Penumbra.Import;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Subclasses;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

/// <summary>
/// A sub mod is a collection of
/// - file replacements
///   - file swaps
///   - meta manipulations
/// that can be used either as an option or as the default data for a mod.
/// It can be loaded and reloaded from Json.
/// Nothing is checked for existence or validity when loading.
/// Objects are also not checked for uniqueness, the first appearance of a game path or meta path decides.
/// </summary>
public sealed class SubMod : ISubMod
{
    public string Name { get; set; } = "Default";

    public string FullName
        => GroupIdx < 0 ? "Default Option" : $"{ParentMod.Groups[GroupIdx].Name}: {Name}";

    public string Description { get; set; } = string.Empty;

    internal IMod ParentMod { get; private init; }
    internal int  GroupIdx  { get; private set; }
    internal int  OptionIdx { get; private set; }

    public bool IsDefault
        => GroupIdx < 0;

    public Dictionary<Utf8GamePath, FullPath> FileData         = new();
    public Dictionary<Utf8GamePath, FullPath> FileSwapData     = new();
    public HashSet<MetaManipulation>          ManipulationData = new();

    public SubMod(IMod parentMod)
        => ParentMod = parentMod;

    public IReadOnlyDictionary<Utf8GamePath, FullPath> Files
        => FileData;

    public IReadOnlyDictionary<Utf8GamePath, FullPath> FileSwaps
        => FileSwapData;

    public IReadOnlySet<MetaManipulation> Manipulations
        => ManipulationData;

    public void SetPosition(int groupIdx, int optionIdx)
    {
        GroupIdx  = groupIdx;
        OptionIdx = optionIdx;
    }

    public void Load(DirectoryInfo basePath, JToken json, out int priority)
    {
        FileData.Clear();
        FileSwapData.Clear();
        ManipulationData.Clear();

        // Every option has a name, but priorities are only relevant for multi group options.
        Name        = json[nameof(ISubMod.Name)]?.ToObject<string>() ?? string.Empty;
        Description = json[nameof(ISubMod.Description)]?.ToObject<string>() ?? string.Empty;
        priority    = json[nameof(IModGroup.Priority)]?.ToObject<int>() ?? 0;

        var files = (JObject?)json[nameof(Files)];
        if (files != null)
            foreach (var property in files.Properties())
            {
                if (Utf8GamePath.FromString(property.Name, out var p, true))
                    FileData.TryAdd(p, new FullPath(basePath, property.Value.ToObject<Utf8RelPath>()));
            }

        var swaps = (JObject?)json[nameof(FileSwaps)];
        if (swaps != null)
            foreach (var property in swaps.Properties())
            {
                if (Utf8GamePath.FromString(property.Name, out var p, true))
                    FileSwapData.TryAdd(p, new FullPath(property.Value.ToObject<string>()!));
            }

        var manips = json[nameof(Manipulations)];
        if (manips != null)
            foreach (var s in manips.Children().Select(c => c.ToObject<MetaManipulation>())
                         .Where(m => m.Validate()))
                ManipulationData.Add(s);
    }

    internal static void DeleteDeleteList(IEnumerable<string> deleteList, bool delete)
    {
        if (!delete)
            return;

        foreach (var file in deleteList)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not delete incorporated meta file {file}:\n{e}");
            }
        }
    }
}
