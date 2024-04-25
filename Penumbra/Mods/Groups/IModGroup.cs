using Newtonsoft.Json;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Groups;

public interface ITexToolsGroup
{
    public IReadOnlyList<IModDataOption> OptionData { get; }
}

public interface IModGroup
{
    public const int MaxMultiOptions = 63;

    public Mod Mod { get; }
    public string Name { get; }
    public string Description { get; }
    public GroupType Type { get; }
    public ModPriority Priority { get; set; }
    public Setting DefaultSettings { get; set; }

    public FullPath? FindBestMatch(Utf8GamePath gamePath);
    public int AddOption(Mod mod, string name, string description = "");

    public IReadOnlyList<IModOption> Options { get; }
    public IReadOnlyList<IModDataContainer> DataContainers { get; }
    public bool IsOption { get; }

    public IModGroup Convert(GroupType type);
    public bool MoveOption(int optionIdxFrom, int optionIdxTo);

    public int GetIndex();

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations);

    /// <summary> Ensure that a value is valid for a group. </summary>
    public Setting FixSetting(Setting setting);

    public void WriteJson(JsonTextWriter jWriter, JsonSerializer serializer, DirectoryInfo? basePath = null);

    public bool ChangeOptionDescription(int optionIndex, string newDescription)
    {
        if (optionIndex < 0 || optionIndex >= Options.Count)
            return false;

        var option = Options[optionIndex];
        if (option.Description == newDescription)
            return false;

        option.Description = newDescription;
        return true;
    }

    public bool ChangeOptionName(int optionIndex, string newName)
    {
        if (optionIndex < 0 || optionIndex >= Options.Count)
            return false;

        var option = Options[optionIndex];
        if (option.Name == newName)
            return false;

        option.Name = newName;
        return true;
    }

    public (int Redirections, int Swaps, int Manips) GetCounts();

    public static (int Redirections, int Swaps, int Manips) GetCountsBase(IModGroup group)
    {
        var redirectionCount = 0;
        var swapCount = 0;
        var manipCount = 0;
        foreach (var option in group.DataContainers)
        {
            redirectionCount += option.Files.Count;
            swapCount += option.FileSwaps.Count;
            manipCount += option.Manipulations.Count;
        }

        return (redirectionCount, swapCount, manipCount);
    }
}
