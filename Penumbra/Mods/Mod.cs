using Luna;
using OtterGui.Classes;
using Penumbra.GameData.Data;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

[Flags]
public enum FeatureFlags : ulong
{
    None    = 0,
    Atch    = 1ul << 0,
    Shp     = 1ul << 1,
    Atr     = 1ul << 2,
    Invalid = 1ul << 62,
}

public sealed class Mod : IMod
{
    public static readonly TemporaryMod ForcedFiles = new()
    {
        Name     = "Forced Files",
        Index    = -1,
        Priority = ModPriority.MaxValue,
    };

    // Main Data
    public DirectoryInfo ModPath { get; internal set; }

    public string Identifier
        => Index >= 0 ? ModPath.Name : Name;

    public int Index { get; internal set; } = -1;

    public bool IsTemporary
        => Index < 0;

    /// <summary>Unused if Index is less than 0 but used for special temporary mods.</summary>
    public ModPriority Priority
        => ModPriority.Default;

    IReadOnlyList<IModGroup> IMod.Groups
        => Groups;

    internal Mod(DirectoryInfo modPath)
    {
        ModPath = modPath;
        Default = new DefaultSubMod(this);
    }

    public override string ToString()
        => Name.Text;

    // Meta Data
    public LowerString           Name                  { get; internal set; } = "New Mod";
    public LowerString           Author                { get; internal set; } = LowerString.Empty;
    public string                Description           { get; internal set; } = string.Empty;
    public string                Version               { get; internal set; } = string.Empty;
    public string                Website               { get; internal set; } = string.Empty;
    public string                Image                 { get; internal set; } = string.Empty;
    public IReadOnlyList<string> ModTags               { get; internal set; } = [];
    public HashSet<CustomItemId> DefaultPreferredItems { get; internal set; } = [];
    public FeatureFlags          RequiredFeatures      { get; internal set; } = 0;


    // Local Data
    public long                  ImportDate            { get; internal set; } = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();
    public IReadOnlyList<string> LocalTags             { get; internal set; } = [];
    public string                Note                  { get; internal set; } = string.Empty;
    public HashSet<CustomItemId> PreferredChangedItems { get; internal set; } = [];
    public bool                  Favorite              { get; internal set; }

    // Options
    public readonly DefaultSubMod   Default;
    public readonly List<IModGroup> Groups = [];

    /// <summary> Compute the required feature flags for this mod. </summary>
    public FeatureFlags ComputeRequiredFeatures()
    {
        var flags = FeatureFlags.None;
        foreach (var option in AllDataContainers)
        {
            if (option.Manipulations.Atch.Count > 0)
                flags |= FeatureFlags.Atch;
            if (option.Manipulations.Atr.Count > 0)
                flags |= FeatureFlags.Atr;
            if (option.Manipulations.Shp.Count > 0)
                flags |= FeatureFlags.Shp;
        }

        return flags;
    }

    public AppliedModData GetData(ModSettings? settings = null)
    {
        if (settings is not { Enabled: true })
            return AppliedModData.Empty;

        var dictRedirections = new Dictionary<Utf8GamePath, FullPath>(TotalFileCount);
        var setManips        = new MetaDictionary();
        foreach (var (groupIndex, group) in Groups.Index().Reverse().OrderByDescending(g => g.Item.Priority))
        {
            var config = settings.Settings[groupIndex];
            group.AddData(config, dictRedirections, setManips);
        }

        Default.AddTo(dictRedirections, setManips);
        return new AppliedModData(dictRedirections, setManips);
    }

    public IEnumerable<IModDataContainer> AllDataContainers
        => Groups.SelectMany(o => o.DataContainers).Prepend(Default);

    public List<FullPath> FindUnusedFiles()
    {
        var modFiles = AllDataContainers.SelectMany(o => o.Files)
            .Select(p => p.Value)
            .ToHashSet();
        return ModPath.EnumerateDirectories()
            .Where(d => !d.IsHidden())
            .SelectMany(FileExtensions.EnumerateNonHiddenFiles)
            .Select(f => new FullPath(f))
            .Where(f => !modFiles.Contains(f))
            .ToList();
    }

    // Cache
    public readonly SortedList<string, IIdentifiedObjectData> ChangedItems = new();

    public string LowerChangedItemsString { get; internal set; } = string.Empty;
    public string AllTagsLower            { get; internal set; } = string.Empty;

    public int    TotalFileCount         { get; internal set; }
    public int    TotalSwapCount         { get; internal set; }
    public int    TotalManipulations     { get; internal set; }
    public ushort LastChangedItemsUpdate { get; internal set; }
    public bool   HasOptions             { get; internal set; }
}
