using OtterGui;
using OtterGui.Classes;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Mods;

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
    public LowerString           Name        { get; internal set; } = "New Mod";
    public LowerString           Author      { get; internal set; } = LowerString.Empty;
    public string                Description { get; internal set; } = string.Empty;
    public string                Version     { get; internal set; } = string.Empty;
    public string                Website     { get; internal set; } = string.Empty;
    public IReadOnlyList<string> ModTags     { get; internal set; } = [];


    // Local Data
    public long                  ImportDate { get; internal set; } = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();
    public IReadOnlyList<string> LocalTags  { get; internal set; } = [];
    public string                Note       { get; internal set; } = string.Empty;
    public bool                  Favorite   { get; internal set; } = false;


    // Options
    public readonly DefaultSubMod   Default;
    public readonly List<IModGroup> Groups = [];

    public AppliedModData GetData(ModSettings? settings = null)
    {
        if (settings is not { Enabled: true })
            return AppliedModData.Empty;

        var dictRedirections = new Dictionary<Utf8GamePath, FullPath>(TotalFileCount);
        var setManips        = new HashSet<MetaManipulation>(TotalManipulations);
        foreach (var (group, groupIndex) in Groups.WithIndex().OrderByDescending(g => g.Value.Priority))
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
    public readonly SortedList<string, object?> ChangedItems = new();

    public string LowerChangedItemsString { get; internal set; } = string.Empty;
    public string AllTagsLower            { get; internal set; } = string.Empty;

    public int  TotalFileCount     { get; internal set; }
    public int  TotalSwapCount     { get; internal set; }
    public int  TotalManipulations { get; internal set; }
    public bool HasOptions         { get; internal set; }
}
