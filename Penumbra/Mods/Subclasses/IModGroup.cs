using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

public interface ITexToolsGroup
{
    public IReadOnlyList<IModDataOption> OptionData { get; }
}

public interface IModGroup
{
    public const int MaxMultiOptions = 63;

    public Mod         Mod             { get; }
    public string      Name            { get; }
    public string      Description     { get; }
    public GroupType   Type            { get; }
    public ModPriority Priority        { get; set; }
    public Setting     DefaultSettings { get; set; }

    public FullPath? FindBestMatch(Utf8GamePath gamePath);
    public int       AddOption(Mod mod, string name, string description = "");

    public IReadOnlyList<IModOption>        Options        { get; }
    public IReadOnlyList<IModDataContainer> DataContainers { get; }
    public bool                             IsOption       { get; }

    public IModGroup Convert(GroupType type);
    public bool      MoveOption(int optionIdxFrom, int optionIdxTo);

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

    public static void WriteJsonBase(JsonTextWriter jWriter, IModGroup group)
    {
        jWriter.WriteStartObject();
        jWriter.WritePropertyName(nameof(group.Name));
        jWriter.WriteValue(group!.Name);
        jWriter.WritePropertyName(nameof(group.Description));
        jWriter.WriteValue(group.Description);
        jWriter.WritePropertyName(nameof(group.Priority));
        jWriter.WriteValue(group.Priority.Value);
        jWriter.WritePropertyName(nameof(group.Type));
        jWriter.WriteValue(group.Type.ToString());
        jWriter.WritePropertyName(nameof(group.DefaultSettings));
        jWriter.WriteValue(group.DefaultSettings.Value);
    }

    public (int Redirections, int Swaps, int Manips) GetCounts();

    public static (int Redirections, int Swaps, int Manips) GetCountsBase(IModGroup group)
    {
        var redirectionCount = 0;
        var swapCount        = 0;
        var manipCount       = 0;
        foreach (var option in group.DataContainers)
        {
            redirectionCount += option.Files.Count;
            swapCount        += option.FileSwaps.Count;
            manipCount       += option.Manipulations.Count;
        }

        return (redirectionCount, swapCount, manipCount);
    }
}

public readonly struct ModSaveGroup : ISavable
{
    private readonly DirectoryInfo  _basePath;
    private readonly IModGroup?     _group;
    private readonly int            _groupIdx;
    private readonly DefaultSubMod? _defaultMod;
    private readonly bool           _onlyAscii;

    public ModSaveGroup(Mod mod, int groupIdx, bool onlyAscii)
    {
        _basePath = mod.ModPath;
        _groupIdx = groupIdx;
        if (_groupIdx < 0)
            _defaultMod = mod.Default;
        else
            _group = mod.Groups[_groupIdx];
        _onlyAscii = onlyAscii;
    }

    public ModSaveGroup(DirectoryInfo basePath, IModGroup group, int groupIdx, bool onlyAscii)
    {
        _basePath  = basePath;
        _group     = group;
        _groupIdx  = groupIdx;
        _onlyAscii = onlyAscii;
    }

    public ModSaveGroup(DirectoryInfo basePath, DefaultSubMod @default, bool onlyAscii)
    {
        _basePath   = basePath;
        _groupIdx   = -1;
        _defaultMod = @default;
        _onlyAscii  = onlyAscii;
    }

    public string ToFilename(FilenameService fileNames)
        => fileNames.OptionGroupFile(_basePath.FullName, _groupIdx, _group?.Name ?? string.Empty, _onlyAscii);

    public void Save(StreamWriter writer)
    {
        using var j = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer { Formatting = Formatting.Indented };
        j.WriteStartObject();
        if (_groupIdx >= 0)
            _group!.WriteJson(j, serializer);
        else
            IModDataContainer.WriteModData(j, serializer, _defaultMod!, _basePath);
        j.WriteEndObject();
    }
}
